#nullable enable
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Game.Session;
using Shooter.Generation;
using Shooter.Generation.Environment;
using Shooter.Presentation.Audio;
using Shooter.Presentation.Fx;
using Shooter.Presentation.Hud;
using Shooter.Presentation.Input;
using Shooter.Presentation.Menus;
using Shooter.Presentation.Rendering;
using Shooter.Presentation.Rig;
using Shooter.Presentation.View;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using EntityId = Shooter.Contracts.EntityId;

namespace Shooter.Presentation.Session
{
    /// <summary>Combat feedback from authority events: impact FX, movement dust, hit feedback, damage sources and gunfire reveals.</summary>
    public sealed partial class ProductSessionView : MonoBehaviour
    {
        // ADR-127: the other pellets of a shotgun blast each draw a tracer from the muzzle the shot was seen from.
        private readonly PelletTracerTracker pelletTracers = new PelletTracerTracker();
        private Float3 pelletTracerMuzzle;
        private float pelletTracerPush;

        /// <summary>
        /// Spawn short-lived analytic mesh FX from authority combat events already leased for audio/HUD.
        /// ReducedFlash suppresses muzzle mesh + overlay only; impact/smoke proxies remain.
        /// </summary>
        private void ObserveCombatFx(CombatEvent ev)
        {
            if(ev.Kind==CombatEventKind.Impact && ev.Payload is ImpactPayload mark && mark.RegionId==0)
                mapView?.Decals?.Impact(mark.Position,mark.Normal,ProductAudioEventRouter.IsGrenadeBlastImpact(ev));
            if (ev.Kind == CombatEventKind.ProjectileSpawned && ev.Payload is ProjectilePayload projectile)
            {
                grenadeFlight.TrySpawn(projectile.ProjectileId, projectile.Origin, projectile.Velocity);
                if (session != null && ev.Instigator.Equals(session.HumanEntityId))
                    input.NotifyGrenadeSpent();
                return;
            }

            // Cooked-off in-hand boom also spends the only grenade this life.
            if (ev.Kind == CombatEventKind.Impact
                && session != null
                && ev.Instigator.Equals(session.HumanEntityId)
                && ProductAudioEventRouter.IsGrenadeBlastImpact(ev))
            {
                input.NotifyGrenadeSpent();
            }

            if (ev.Kind == CombatEventKind.ShotAccepted && ev.Payload is ShotPayload shot)
            {
                // Knife melee: no muzzle flash / tracer / casing — slash is the FP viewmodel.
                if (IsKnifeDefinition(shot.WeaponId)) { pelletTracers.ObserveShot(ev, drawsTracers: false); return; }
                // ADR-061: dual flame lobes + tracer + casing. ReducedFlash hides muzzle+tracer only.
                var profile=WeaponVisualProfiles.Require(shot.WeaponId);
                // The flash sits on the muzzle the viewer sees (FP view model or held weapon), not on the
                // gameplay shot origin, which floats beside the handguard. Falls back when no socket is shown.
                Float3 visibleMuzzle=default;
                bool anchored=actors!=null&&actors.TryGetMuzzleWorld(ev.Instigator,out visibleMuzzle);
                Float3 flashOrigin=anchored?visibleMuzzle:shot.Origin;
                // ADR-102: each gun sizes its own flash; a muzzle brake adds side jets.
                if(profile.MuzzleFlashVariant!=0)analyticFxPool.SpawnGunshotFeedback(flashOrigin, shot.UnitDirection, reducedFlash,
                    profile.MuzzleFlashVariant,
                    anchored?AnalyticFxPool.MuzzleSocketPushMeters:AnalyticFxPool.MuzzleFlashForwardPushMeters, profile.MuzzleFlashSideJets);
                analyticFxPool.TrySpawn(FxKind.SmokeProxy,flashOrigin,shot.UnitDirection);
                pelletTracers.ObserveShot(ev, drawsTracers: profile.MuzzleFlashVariant!=0);
                pelletTracerMuzzle=flashOrigin;
                pelletTracerPush=anchored?AnalyticFxPool.MuzzleSocketPushMeters:AnalyticFxPool.MuzzleFlashForwardPushMeters;
                return;
            }

            if (ev.Kind == CombatEventKind.Impact && ev.Payload is ImpactPayload impact)
            {
                // ADR-127: ReducedFlash hides pellet tracers like the primary tracer (ADR-061).
                if(pelletTracers.TryPelletImpact(ev,out var pelletPoint)&&!reducedFlash)
                    analyticFxPool.SpawnPelletTracer(pelletTracerMuzzle,pelletPoint,pelletTracerPush);
                grenadeFlight.ObserveImpact(impact.Position);
                bool blast=ProductAudioEventRouter.IsGrenadeBlastImpact(ev);
                if(blast)
                {
                    // ADR-111: layered blast; ReducedFlash drops the bright layers inside the pool recipe.
                    analyticFxPool.SpawnGrenadeBlast(impact.Position,impact.Normal,reducedFlash,QualitySettings.GetQualityLevel());
                    if(!reducedCameraMotion&&!reducedFlash&&camera!=null)
                    {
                        float shake=AnalyticFxPool.BlastShakeScale(Vector3.Distance(camera.transform.position,new Vector3(impact.Position.X,impact.Position.Y,impact.Position.Z)));
                        if(shake>0f)
                        {
                            // A nearer blast during an older shake takes over; a farther one cannot weaken it.
                            float now=Time.unscaledTime,left=Mathf.Max(0f,explosionShakeUntil-now)/AnalyticFxPool.BlastShakeSeconds*explosionShakeScale;
                            if(shake>=left){explosionShakeScale=shake;explosionShakeUntil=now+AnalyticFxPool.BlastShakeSeconds;}
                        }
                    }
                }
                else if(impact.RegionId==0)
                {
                    ImpactSurfaceFx.Spawn(analyticFxPool,ResolveImpactSurface(impact),impact.Position,impact.Normal);
                }
            }
        }

        /// <summary>
        /// Presentation-only surface lookup against the verified packaged collision boxes: the hit box's packaged
        /// material role (same classifier as the solids mesh) decides metal; floor hits use the registry ground.
        /// </summary>
        private ImpactSurface ResolveImpactSurface(ImpactPayload impact)
        {
            bool metal=impact.SurfaceTag==ArenaBoxBuilder.TagCover,onSolid=false;
            var p=impact.Position;
            for(int i=0;i<hudBoxes.Count&&i<impactMetalBoxes.Length;i++)
            {
                var box=hudBoxes[i];
                if(System.Math.Abs(p.X-box.Center.X)>box.Size.X*.5f+ImpactBoxSlack||System.Math.Abs(p.Y-box.Center.Y)>box.Size.Y*.5f+ImpactBoxSlack||System.Math.Abs(p.Z-box.Center.Z)>box.Size.Z*.5f+ImpactBoxSlack)continue;
                onSolid=true;if(impactMetalBoxes[i]){metal=true;break;}
            }
            return ImpactSurfaceFx.Resolve(metal,onSolid,p,impact.Normal,impactAsphaltGround);
        }

        /// <summary>ADR-126: the registered map plan's vehicles, any of which may burn (cosmetic only).</summary>
        private static IReadOnlyList<AmbientFireSite> AmbientFireSites(string? mapId)
        {
            var sites=new List<AmbientFireSite>();
            if(string.IsNullOrEmpty(mapId)||!MapRegistry.TryGet(mapId!,out _))return sites;
            foreach(var f in ProductMapPlanner.Generate(mapId!).Features)
                if(f.Kind==ArenaFeatureKind.Truck||f.Kind==ArenaFeatureKind.Tanker||f.Kind==ArenaFeatureKind.Trailer||f.Kind==ArenaFeatureKind.Tank)
                    sites.Add(new AmbientFireSite(f.Center,f.Size,f.Facing));
            return sites;
        }

        private static bool[] ClassifyImpactMetal(IReadOnlyList<CollisionBox> boxes,string mapId)
        {
            var result=new bool[boxes.Count];bool oil=string.Equals(mapId,OilDepotDressing.MapId,System.StringComparison.Ordinal);
            for(int i=0;i<boxes.Count;i++)
            {
                ushort role=MapSolidsMesh.Classify(boxes[i]);
                result[i]=oil&&OilDepotDressing.ReplacedByDressing(boxes[i])||role!=(ushort)MapSolidsMaterialRole.StructureConcrete&&role!=(ushort)MapSolidsMaterialRole.Trim&&role!=(ushort)MapSolidsMaterialRole.Foundation;
            }
            return result;
        }

        private void ObserveMovementDust()
        {
            if(session==null)return;var snapshot=session.TryAcquireSnapshot();if(snapshot==null)return;
            for(int i=0;i<snapshot.Participants.Count&&i<dustLife.Length;i++)
            {
                var participant=snapshot.Participants[i];
                if(!participant.Alive||!session.TryGetMovementView(participant.EntityId,out var movement)||!session.TryGetActorState(participant.EntityId,out var state))continue;
                if(dustLife[i]!=participant.EntityId.Value){dustLife[i]=participant.EntityId.Value;dustDistance[i]=movement.LocomotionDistanceMeters;continue;}
                float interval=movement.Sliding?.5f:1.4f;
                if(movement.LocomotionDistanceMeters-dustDistance[i]<interval)continue;
                dustDistance[i]=movement.LocomotionDistanceMeters;
                var ground=actors?.CollisionGround;if(ground==null)continue;ground.ReferenceHeight=state.Feet.Y;
                if(!ground.TrySample(state.Feet.X,state.Feet.Z,out float floor)||Mathf.Abs(floor-state.Feet.Y)>.08f)continue;
                analyticFxPool.TrySpawn(FxKind.Dust,new Float3(state.Feet.X,floor+.05f,state.Feet.Z),new Float3(0,1,0));
            }
        }

        private static bool IsKnifeWeapon(CombatEvent ev) =>
            ev.Kind == CombatEventKind.ShotAccepted && ev.Payload is ShotPayload shot && IsKnifeDefinition(shot.WeaponId);

        private static bool IsKnifeDefinition(DefinitionId weaponId) =>
            string.Equals(weaponId.ToString(), "weapons/knife", System.StringComparison.Ordinal);

        /// <summary>Authority-only damage feedback for local player (taken/dealt). No invented hits.</summary>
        private void ObserveHitFeedback(CombatEvent ev)
        {
            if (session == null || !session.HumanEntityId.IsValid) return;
            var local = session.HumanEntityId;
            if (FirstPersonHitFeedback.IsDamageTaken(ev.Kind, ev.Target, local))
            {
                float now = Time.unscaledTime;
                damagePulseUntil = now + CombatHudVisual.DamagePulseSeconds;
                damageFlashUntil = now + FirstPersonHitFeedback.DamageFlashSeconds;
                hitGradeUntil = now + CombatHudVisual.HitGradeSeconds;
                bool headshot = false;
                lastDamageAmount = 25;
                if (ev.Payload is DamagePayload dmg)
                {
                    lastSeenHealth = dmg.RemainingHealth;
                    lastDamageAmount = dmg.Amount;
                    headshot = dmg.RegionId == 1;
                }
                RecordDamageSource(ev.Instigator, now);
                damagePunch.Kick(lastDamageAmount, headshot, AttackerBearing(ev.Instigator), reducedCameraMotion);
                audioOutput?.Fx.Hit(lastDamageAmount);
            }
            else if (FirstPersonHitFeedback.IsDamageDealt(ev.Kind, ev.Instigator, local))
            {
                hitMarkerUntil = Time.unscaledTime + FirstPersonHitFeedback.HitMarkerSeconds;
                hitMarkerHeadshot = ev.Payload is DamagePayload dealt && dealt.RegionId == 1;
            }
        }

        /// <summary>Attacker bearing relative to the local view (radians, positive = right); 0 if unknown.</summary>
        private float AttackerBearing(EntityId instigator)
        {
            if (session == null || !instigator.IsValid) return 0f;
            if (!session.TryGetActorState(instigator, out var attacker)) return 0f;
            if (!session.TryGetActorState(session.HumanEntityId, out var self)) return 0f;
            if (instigator.Equals(self.EntityId)) return 0f; // own grenade: straight flinch
            return CombatHudVisual.DamageIndicatorAngle(self.AimYaw, self.Feet.X, self.Feet.Z, attacker.Feet.X, attacker.Feet.Z);
        }

        private void RecordDamageSource(EntityId instigator, float now)
        {
            if (!instigator.IsValid || session == null) return;
            if (!session.TryGetActorState(instigator, out var attacker)) return;
            int slot = 0;
            for (int i = 0; i < damageSourceIds.Length; i++)
            {
                if (damageSourceIds[i] == instigator.Value && damageSourceUntil[i] > now) { slot = i; break; }
                if (damageSourceUntil[i] < damageSourceUntil[slot]) slot = i;
            }
            damageSourceIds[slot] = instigator.Value;
            damageSourceX[slot] = attacker.Feet.X;
            damageSourceZ[slot] = attacker.Feet.Z;
            damageSourceUntil[slot] = now + CombatHudVisual.DamageIndicatorSeconds;
        }

        /// <summary>
        /// Presentation-only: enemy ShotAccepted briefly marks last-known shot origin on the tactical map.
        /// Does not stream live poses or change AI sensing.
        /// </summary>
        private void ObserveGunfireReveal(CombatEvent ev)
        {
            if (ev.Kind != CombatEventKind.ShotAccepted || ev.Payload is not ShotPayload shot) return;
            // Melee is not gunfire; keep the map reveal for firearms only.
            if (IsKnifeDefinition(shot.WeaponId)) return;
            if (session == null || !session.HumanEntityId.IsValid) return;
            if (!ev.Instigator.IsValid) return;
            bool isLocal = ev.Instigator == session.HumanEntityId;
            if (!session.TryGetActorState(session.HumanEntityId, out var human)) return;
            if (!session.TryGetActorState(ev.Instigator, out var shooter)) return;
            bool enemy = shooter.Team != human.Team;
            if (!CombatHudVisual.ShouldRevealGunfire(true, isLocal, enemy)) return;

            float now = Time.unscaledTime;
            int slot = CombatHudVisual.PickGunfireRevealSlot(
                gunfireRevealIds, gunfireRevealUntil, ev.Instigator.Value, now, CombatHudVisual.MaxGunfireReveals);
            gunfireRevealIds[slot] = ev.Instigator.Value;
            gunfireRevealX[slot] = shot.Origin.X;
            gunfireRevealZ[slot] = shot.Origin.Z;
            gunfireRevealUntil[slot] = now + CombatHudVisual.GunfireRevealSeconds;
        }

        private void ClearGunfireReveals()
        {
            for (int i = 0; i < gunfireRevealUntil.Length; i++)
            {
                gunfireRevealIds[i] = 0;
                gunfireRevealX[i] = 0;
                gunfireRevealZ[i] = 0;
                gunfireRevealUntil[i] = 0;
            }
        }
    }
}
