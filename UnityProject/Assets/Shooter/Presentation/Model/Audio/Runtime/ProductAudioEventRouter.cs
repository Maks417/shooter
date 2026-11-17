#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Presentation.View;

namespace Shooter.Presentation.Audio
{
    /// <summary>
    /// Consumes ordered read-only product audio cues once (combat event IDs + footstep strides + UI).
    /// Presentation-only; never mutates authority. Clears on session detach so stale sounds stop.
    /// ADR-096 adds weapon-specific reload Foley fitted to the authority reload window, knife hits,
    /// grenade far layer, local hit/kill confirmation, bullet flybys and a distant-battle bed.
    /// </summary>
    public sealed class ProductAudioEventRouter
    {
        public const float FootstepStrideMeters = 0.75f;
        /// <summary>Legacy gunfire ceiling; prefer <see cref="CombatAudioDistance.MaxAudibleMeters"/>.</summary>
        public const float MaxAudibleDistanceMeters = CombatAudioDistance.GunfireMaxMeters;
        /// <summary>Gameplay pose region 1 = head (DamageMath / hit volumes).</summary>
        public const ushort HeadRegionId = 1;
        /// <summary>Authority tick rate used to turn reload windows into seconds.</summary>
        public const float TicksPerSecond = 60f;
        /// <summary>Reload Foley may be sped up or slowed by at most this fraction to fit the reload window.</summary>
        public const float ReloadFitTolerance = 0.12f;
        /// <summary>A round passing closer than this to the listener's head plays a flyby crack.</summary>
        public const float FlybyRadiusMeters = 2.5f;
        public const float DistantBattleMinSeconds = 6f;
        public const float DistantBattleMaxSeconds = 20f;

        private readonly SynthAudioMixer mixer;
        private readonly HashSet<string> consumed = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<ulong, float> strideAccum = new Dictionary<ulong, float>();
        private readonly Dictionary<ulong, Float3> lastFeet = new Dictionary<ulong, Float3>();
        private readonly Dictionary<ulong, uint> lastKnifeTick = new Dictionary<ulong, uint>();
        private ulong attachedSessionId;
        private bool attached;
        private ushort uiOrdinal;
        private uint localOrdinal;
        private float listenerYaw;
        private float reflections;
        private float distantBattleTimer;
        private uint distantRng;
        private readonly List<(ulong owner, float due, Float3 position, ulong seed)> pendingBounces =
            new List<(ulong owner, float due, Float3 position, ulong seed)>();
        public const int MaxPendingBounces = 16;
        /// <summary>ADR-102: bolt-action rounds are followed by the bolt being worked inside the fire interval.</summary>
        private readonly List<(ulong owner, float due, Float3 position, ulong seed, bool local)> pendingCycles =
            new List<(ulong owner, float due, Float3 position, ulong seed, bool local)>();
        /// <summary>Last primary and sidearm each shooter fired: reload Foley follows the gun, not the slot.</summary>
        private readonly Dictionary<ulong, (DefinitionId primary, DefinitionId sidearm)> lastGuns =
            new Dictionary<ulong, (DefinitionId primary, DefinitionId sidearm)>();
        /// <summary>Second, shorter hop after the first ground contact.</summary>
        public const float SecondBounceDelaySeconds = 0.32f;
        /// <summary>Main-thread presentation query only; never invoked by the audio callback.</summary>
        public Func<Float3, Float3, float>? ObstructionProbe { get; set; }

        public void SetListenerAcoustics(float yawRadians, float reflectionAmount, Float3 listenerFeet = default)
        {
            listenerYaw = yawRadians;
            reflections = Math.Clamp(reflectionAmount, 0f, 1f);
            mixer.UpdateListener(yawRadians, listenerFeet.X, listenerFeet.Z);
            // Ambience may yield its voice during combat; restore it once capacity returns.
            if (attached) mixer.EnsureAmbient(attachedSessionId);
        }

        public ProductAudioEventRouter(SynthAudioMixer mixer)
        {
            this.mixer = mixer ?? throw new ArgumentNullException(nameof(mixer));
        }

        public SynthAudioMixer Mixer => mixer;
        public int ConsumedEventCount => consumed.Count;
        public bool IsAttached => attached;
        public ulong AttachedSessionId => attachedSessionId;

        public void AttachSession(ulong authoritySessionId)
        {
            if (authoritySessionId == 0) throw new ArgumentOutOfRangeException(nameof(authoritySessionId));
            if (attached && attachedSessionId == authoritySessionId) return;
            DetachSession();
            attachedSessionId = authoritySessionId;
            attached = true;
            distantRng = (uint)(authoritySessionId ^ (authoritySessionId >> 32)) | 1u;
            distantBattleTimer = NextDistantInterval();
            mixer.EnsureAmbient(authoritySessionId);
        }

        public void DetachSession()
        {
            attached = false;
            attachedSessionId = 0;
            consumed.Clear();
            strideAccum.Clear();
            lastFeet.Clear();
            lastKnifeTick.Clear();
            pendingBounces.Clear();
            pendingCycles.Clear();
            lastGuns.Clear();
            uiOrdinal = 0;
            localOrdinal = 0;
            mixer.StopAll();
        }

        public int ConsumeCombatEvents(IReadOnlyList<CombatEvent> events, Float3 listenerFeet) =>
            ConsumeCombatEvents(events, listenerFeet, default);

        public int ConsumeCombatEvents(IReadOnlyList<CombatEvent> events, Float3 listenerFeet, EntityId localEntity)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (!attached) return 0;
            int played = 0;
            for (int i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                if (ev.Id.AuthoritySessionId != attachedSessionId) continue;
                string key = EventKey(ev.Id);
                if (!consumed.Add(key)) continue;

                ObserveCancellation(ev);
                ScheduleGrenadeBounces(ev);
                bool knifeHit = IsKnifeFollowUp(ev);
                if (ev.Kind == CombatEventKind.ShotAccepted && ev.Payload is ShotPayload knifeShot && IsKnife(knifeShot.WeaponId))
                    lastKnifeTick[ev.Instigator.Value] = ev.Id.Tick;

                RememberGun(ev);
                if (TryMapCombat(ev, knifeHit, out var voice))
                {
                    if (ev.Kind == CombatEventKind.ReloadStarted) voice = ReloadVoiceFor(ev);
                    Float3 pos = EventPosition(ev, listenerFeet);
                    bool local = IsLocalEmitter(ev, localEntity);
                    ScheduleBoltCycle(ev, pos, local);
                    float rate = ReloadPlaybackRate(ev, voice);
                    if (PlayAt(voice, ev, pos, listenerFeet, local, rate)) played++;
                    // Distant explosion layer: rolls in past ~30 m where the close blast thins out.
                    if (voice == SynthVoiceKind.GrenadeExplode && PlayAt(SynthVoiceKind.GrenadeExplodeFar, ev, pos, listenerFeet, local, 1f)) played++;
                }

                played += PlayLocalFeedback(ev, localEntity);
                if (TryFlyby(ev, listenerFeet, localEntity, out float missMeters, out Float3 closest)
                    && mixer.TryPlay(SynthVoiceKind.BulletFlyby, missMeters, ComputePan(listenerFeet, closest, listenerYaw),
                        Seed(ev) ^ 0xF1Bu, false, 0f, 0f, closest.X, closest.Z))
                    played++;
            }
            return played;
        }

        private bool PlayAt(SynthVoiceKind voice, CombatEvent ev, Float3 pos, Float3 listenerFeet, bool local, float rate)
        {
            float distance = DistanceMeters(listenerFeet, pos);
            float max = CombatAudioDistance.MaxAudibleMeters(voice);
            if (max > 0f && distance > max) return false;
            float pan = local ? 0f : ComputePan(listenerFeet, pos, listenerYaw);
            float obstruction = local ? 0f : ObstructionProbe?.Invoke(EarHeight(listenerFeet), pos) ?? 0f;
            ulong owner = ev.Instigator.IsValid ? ev.Instigator.Value : 0UL;
            ulong seed = CueSeed(ev, voice);
            return mixer.TryPlay(voice, distance, pan, seed, local, obstruction, reflections, pos.X, pos.Z, owner, rate);
        }

        /// <summary>Local-only confirmation layered over the world cue: hit tick and kill thump.</summary>
        private int PlayLocalFeedback(CombatEvent ev, EntityId localEntity)
        {
            if (!localEntity.IsValid || ev.Instigator != localEntity || ev.Target == localEntity || !ev.Target.IsValid) return 0;
            SynthVoiceKind kind;
            if (ev.Kind == CombatEventKind.DamageApplied) kind = SynthVoiceKind.HitMarker;
            else if (ev.Kind == CombatEventKind.Death) kind = SynthVoiceKind.KillConfirm;
            else return 0;
            return mixer.TryPlay(kind, 0f, 0f, Seed(ev) ^ 0x417u, true, 0f, 0f) ? 1 : 0;
        }

        /// <summary>A reload that ends without completing (death) or is interrupted by firing cuts its Foley.</summary>
        private void ObserveCancellation(CombatEvent ev)
        {
            ulong id = ev.Kind == CombatEventKind.Death ? ev.Target.Value
                : ev.Kind == CombatEventKind.ShotAccepted ? ev.Instigator.Value : 0UL;
            if (id == 0) return;
            mixer.StopOwner(id, SynthVoiceKind.ReloadStart);
            mixer.StopOwner(id, SynthVoiceKind.ReloadPistol);
            mixer.StopOwner(id, SynthVoiceKind.ReloadAk);
            mixer.StopOwner(id, SynthVoiceKind.ReloadBolt);
            if (ev.Kind == CombatEventKind.Death) pendingCycles.RemoveAll(c => c.owner == id);
        }

        /// <summary>
        /// The cosmetic flight has no world queries, so ground contact is estimated analytically: the ballistic arc
        /// (shared gravity) meets the thrower's feet height. Two hops are scheduled; a blast first cancels them.
        /// </summary>
        private void ScheduleGrenadeBounces(CombatEvent ev)
        {
            ulong owner = ev.Instigator.IsValid ? ev.Instigator.Value : 0UL;
            if (ev.Kind == CombatEventKind.Impact && IsGrenadeBlast(ev))
            {
                pendingBounces.RemoveAll(b => b.owner == owner);
                return;
            }
            if (ev.Kind != CombatEventKind.ProjectileSpawned || ev.Payload is not ProjectilePayload projectile) return;
            float groundY = owner != 0 && lastFeet.TryGetValue(owner, out var feet) ? feet.Y : projectile.Origin.Y - 1.5f;
            if (!TryGroundContact(projectile.Origin, projectile.Velocity, groundY, out float seconds, out Float3 landing)) return;
            if (pendingBounces.Count + 2 > MaxPendingBounces) pendingBounces.RemoveRange(0, 2);
            ulong seed = Seed(ev);
            pendingBounces.Add((owner, seconds, landing, seed));
            float hx = projectile.Velocity.X, hz = projectile.Velocity.Z;
            float h = (float)Math.Sqrt(hx * hx + hz * hz);
            var hop = h > 1e-3f ? new Float3(landing.X + hx / h * 0.9f, landing.Y, landing.Z + hz / h * 0.9f) : landing;
            pendingBounces.Add((owner, seconds + SecondBounceDelaySeconds, hop, seed ^ 0xB0UL));
        }

        /// <summary>Time and point where a ballistic arc under the cosmetic gravity reaches <paramref name="groundY"/>.</summary>
        public static bool TryGroundContact(Float3 origin, Float3 velocity, float groundY, out float seconds, out Float3 landing)
        {
            float g = -Shooter.Presentation.Fx.CosmeticGrenadeFlight.GravityY;
            float drop = origin.Y - groundY;
            float disc = velocity.Y * velocity.Y + 2f * g * drop;
            seconds = 0f;
            landing = origin;
            if (g <= 0f || disc < 0f) return false;
            seconds = (velocity.Y + (float)Math.Sqrt(disc)) / g;
            if (!(seconds > 0.1f) || seconds > 4f) return false;
            landing = new Float3(origin.X + velocity.X * seconds, groundY, origin.Z + velocity.Z * seconds);
            return true;
        }

        /// <summary>Advance presentation-scheduled cues (grenade bounces). Call once per presentation frame.</summary>
        public int TickScheduled(float dtSeconds, Float3 listenerFeet)
        {
            if (!attached || dtSeconds <= 0f || pendingBounces.Count + pendingCycles.Count == 0) return 0;
            int played = 0;
            for (int i = pendingCycles.Count - 1; i >= 0; i--)
            {
                var c = pendingCycles[i];
                c.due -= dtSeconds;
                if (c.due > 0f) { pendingCycles[i] = c; continue; }
                pendingCycles.RemoveAt(i);
                if (c.local ? mixer.TryPlay(SynthVoiceKind.BoltCycle, 0f, 0f, c.seed, true, 0f, 0f)
                    : PlayWorld(SynthVoiceKind.BoltCycle, c.position, listenerFeet, c.seed)) played++;
            }
            for (int i = pendingBounces.Count - 1; i >= 0; i--)
            {
                var b = pendingBounces[i];
                b.due -= dtSeconds;
                if (b.due > 0f)
                {
                    pendingBounces[i] = b;
                    continue;
                }
                pendingBounces.RemoveAt(i);
                if (PlayWorld(SynthVoiceKind.GrenadeBounce, b.position, listenerFeet, b.seed)) played++;
            }
            return played;
        }

        public int PendingBounceCount => pendingBounces.Count;
        public int PendingBoltCycleCount => pendingCycles.Count;

        private void RememberGun(CombatEvent ev)
        {
            if (ev.Kind != CombatEventKind.ShotAccepted || ev.Payload is not ShotPayload shot || !ev.Instigator.IsValid) return;
            if (!WeaponVisualProfiles.TryGet(shot.WeaponId, out var profile)) return;
            lastGuns.TryGetValue(ev.Instigator.Value, out var guns);
            if (profile.Family == WeaponVisualFamily.PrimaryGun) guns.primary = shot.WeaponId;
            else if (profile.Family == WeaponVisualFamily.Sidearm) guns.sidearm = shot.WeaponId;
            else return;
            lastGuns[ev.Instigator.Value] = guns;
        }

        private SynthVoiceKind ReloadVoiceFor(CombatEvent ev)
        {
            if (ev.Payload is not ActionPayload action) return SynthVoiceKind.ReloadStart;
            DefinitionId id = default;
            if (lastGuns.TryGetValue(ev.Instigator.Value, out var guns))
                id = action.Slot == 0 ? guns.primary : action.Slot == 1 ? guns.sidearm : default;
            return ReloadVoiceForStyle(WeaponVisualProfiles.Resolve(id, action.Slot).HandlingStyle);
        }

        public static SynthVoiceKind ReloadVoiceForStyle(WeaponHandlingStyle style) => style switch
        {
            WeaponHandlingStyle.Pistol => SynthVoiceKind.ReloadPistol,
            WeaponHandlingStyle.Ak => SynthVoiceKind.ReloadAk,
            WeaponHandlingStyle.Bolt => SynthVoiceKind.ReloadBolt,
            _ => SynthVoiceKind.ReloadStart
        };

        private void ScheduleBoltCycle(CombatEvent ev, Float3 position, bool local)
        {
            if (ev.Kind != CombatEventKind.ShotAccepted || ev.Payload is not ShotPayload shot) return;
            if (!WeaponVisualProfiles.TryGet(shot.WeaponId, out var profile) || profile.HandlingStyle != WeaponHandlingStyle.Bolt) return;
            float interval = profile.FireIntervalSeconds;
            if (interval <= 0f) return;
            if (pendingCycles.Count >= MaxPendingBounces) pendingCycles.RemoveAt(0);
            // Matches the first-person bolt pull (ProceduralViewModel: open from 34 % of 85 % of the interval).
            pendingCycles.Add((ev.Instigator.Value, interval * 0.85f * 0.34f, position, Seed(ev) ^ 0xB017UL, local));
        }

        private bool IsKnifeFollowUp(CombatEvent ev)
        {
            if (ev.Kind != CombatEventKind.DamageApplied && ev.Kind != CombatEventKind.Impact) return false;
            if (!ev.Instigator.IsValid) return false;
            return lastKnifeTick.TryGetValue(ev.Instigator.Value, out uint tick) && tick == ev.Id.Tick;
        }

        private static bool IsLocalEmitter(CombatEvent ev, EntityId localEntity) =>
            localEntity.IsValid &&
            ((ev.Instigator == localEntity && (ev.Kind == CombatEventKind.ShotAccepted
                || ev.Kind == CombatEventKind.ReloadStarted || ev.Kind == CombatEventKind.ReloadCompleted
                || ev.Kind == CombatEventKind.ProjectileSpawned))
             || (ev.Target == localEntity && ev.Kind == CombatEventKind.DamageApplied));

        /// <summary>Speed the reload Foley up or down (bounded) so magazine-in lands inside the authority window.</summary>
        private float ReloadPlaybackRate(CombatEvent ev, SynthVoiceKind voice)
        {
            if (ev.Kind != CombatEventKind.ReloadStarted || ev.Payload is not ActionPayload action) return 1f;
            if (action.EndTick <= action.StartTick) return 1f;
            float window = (action.EndTick - action.StartTick) / TicksPerSecond;
            if (!mixer.Bank.TryGetClip(voice, CueSeed(ev, voice), out var clip)) return 1f;
            float clipSeconds = clip.Length / (float)CombatAudioBank.Channels / mixer.Bank.SampleRate;
            return FitRate(clipSeconds, window);
        }

        /// <summary>Rate that makes <paramref name="clipSeconds"/> last <paramref name="windowSeconds"/>, within tolerance.</summary>
        public static float FitRate(float clipSeconds, float windowSeconds)
        {
            if (clipSeconds <= 0f || windowSeconds <= 0f) return 1f;
            return Math.Clamp(clipSeconds / windowSeconds, 1f - ReloadFitTolerance, 1f + ReloadFitTolerance);
        }

        /// <summary>Enemy round passing close by: perpendicular distance from the shot ray to the listener's head.</summary>
        private static bool TryFlyby(CombatEvent ev, Float3 listenerFeet, EntityId localEntity, out float missMeters, out Float3 closest)
        {
            missMeters = 0f;
            closest = default;
            if (ev.Kind != CombatEventKind.ShotAccepted || ev.Payload is not ShotPayload shot) return false;
            if (localEntity.IsValid && ev.Instigator == localEntity) return false;
            var voice = MapShotVoice(ev);
            if (!SynthAudioMixer.IsGunShotKind(voice)) return false;
            return FlybyGeometry(shot.Origin, shot.UnitDirection, EarHeight(listenerFeet), out missMeters, out closest);
        }

        public static bool FlybyGeometry(Float3 origin, Float3 unitDirection, Float3 ears, out float missMeters, out Float3 closest)
        {
            float lx = ears.X - origin.X, ly = ears.Y - origin.Y, lz = ears.Z - origin.Z;
            float along = lx * unitDirection.X + ly * unitDirection.Y + lz * unitDirection.Z;
            closest = new Float3(origin.X + unitDirection.X * along, origin.Y + unitDirection.Y * along, origin.Z + unitDirection.Z * along);
            missMeters = DistanceMeters(ears, closest);
            // Ignore the shooter's own vicinity (muzzle blast covers it) and rounds that never reach the listener.
            return along > 4f && along < CombatAudioDistance.GunfireMaxMeters && missMeters < FlybyRadiusMeters;
        }

        public int TickFootsteps(IReadOnlyList<ActorLifecycleState> living, Float3 listenerFeet, uint completedTick, EntityId localEntity = default)
        {
            if (living == null) throw new ArgumentNullException(nameof(living));
            if (!attached) return 0;
            int played = 0;
            for (int i = 0; i < living.Count; i++)
            {
                var actor = living[i];
                if (!actor.Alive || !actor.EntityId.IsValid) continue;
                ulong id = actor.EntityId.Value;
                if (!lastFeet.TryGetValue(id, out var prev))
                {
                    lastFeet[id] = actor.Feet;
                    strideAccum[id] = 0f;
                    continue;
                }
                float dx = actor.Feet.X - prev.X, dz = actor.Feet.Z - prev.Z;
                float step = (float)Math.Sqrt(dx * dx + dz * dz);
                lastFeet[id] = actor.Feet;
                // Respawn/teleport must not generate a burst of dozens of footsteps.
                if (step > 3f) { strideAccum[id] = 0f; continue; }
                if (step < 1e-4f) continue;
                float accum = (strideAccum.TryGetValue(id, out var a) ? a : 0f) + step;
                while (accum >= FootstepStrideMeters)
                {
                    accum -= FootstepStrideMeters;
                    string key = "fs:" + attachedSessionId + ":" + completedTick + ":" + id + ":" + consumed.Count;
                    if (!consumed.Add(key)) continue;
                    float distance = DistanceMeters(listenerFeet, actor.Feet);
                    if (distance > CombatAudioDistance.FootstepMaxMeters) continue;
                    bool local = localEntity.IsValid && actor.EntityId == localEntity;
                    float pan = local ? 0f : ComputePan(listenerFeet, actor.Feet, listenerYaw);
                    float obstruction = local ? 0f : ObstructionProbe?.Invoke(EarHeight(listenerFeet),
                        new Float3(actor.Feet.X, actor.Feet.Y + 0.2f, actor.Feet.Z)) ?? 0f;
                    ulong seed = attachedSessionId ^ id ^ completedTick;
                    if (mixer.TryPlay(SynthVoiceKind.Footstep, distance, pan, seed, local, obstruction, 0f, actor.Feet.X, actor.Feet.Z)) played++;
                }
                strideAccum[id] = accum;
            }
            return played;
        }

        /// <summary>
        /// Occasional far-off gunfire or explosions for battlefield atmosphere (ADR-096). Random interval and
        /// side from a session-seeded stream; purely cosmetic and never tied to real actors.
        /// </summary>
        public bool TickDistantBattle(float dtSeconds)
        {
            if (!attached || dtSeconds <= 0f) return false;
            distantBattleTimer -= dtSeconds;
            if (distantBattleTimer > 0f) return false;
            distantBattleTimer = NextDistantInterval();
            float pan = NextUnit(ref distantRng) * 1.6f - 0.8f;
            return mixer.TryPlay(SynthVoiceKind.DistantBattle, 0f, pan, NextRaw(ref distantRng));
        }

        public float DistantBattleSecondsRemaining => distantBattleTimer;

        private float NextDistantInterval() =>
            DistantBattleMinSeconds + NextUnit(ref distantRng) * (DistantBattleMaxSeconds - DistantBattleMinSeconds);

        public bool PlayUi(ulong seed) => PlayUi(SynthVoiceKind.Ui, seed);

        public bool PlayUi(SynthVoiceKind kind, ulong seed)
        {
            if (!attached) return false;
            ushort ordinal = uiOrdinal;
            if (uiOrdinal < ushort.MaxValue) uiOrdinal++;
            string key = "ui:" + attachedSessionId + ":" + ordinal;
            if (!consumed.Add(key)) return false;
            return mixer.TryPlay(kind, 0f, 0f, seed ^ ordinal);
        }

        /// <summary>
        /// Presentation-originated local cue with no authority event (grenade pin, dry fire, weapon switch).
        /// Centered and full width like the player's own weapon.
        /// </summary>
        public bool PlayLocal(SynthVoiceKind kind, ulong seed)
        {
            if (!attached) return false;
            localOrdinal++;
            return mixer.TryPlay(kind, 0f, 0f, seed ^ ((ulong)localOrdinal << 20), true, 0f, 0f);
        }

        /// <summary>Presentation-originated world cue at a position (cosmetic grenade bounce).</summary>
        public bool PlayWorld(SynthVoiceKind kind, Float3 position, Float3 listenerFeet, ulong seed)
        {
            if (!attached) return false;
            float distance = DistanceMeters(listenerFeet, position);
            float max = CombatAudioDistance.MaxAudibleMeters(kind);
            if (max > 0f && distance > max) return false;
            float pan = ComputePan(listenerFeet, position, listenerYaw);
            float obstruction = ObstructionProbe?.Invoke(EarHeight(listenerFeet), position) ?? 0f;
            localOrdinal++;
            return mixer.TryPlay(kind, distance, pan, seed ^ ((ulong)localOrdinal << 20), false, obstruction, 0f, position.X, position.Z);
        }

        private static bool TryMapCombat(CombatEvent ev, bool knifeHit, out SynthVoiceKind voice)
        {
            switch (ev.Kind)
            {
                case CombatEventKind.ShotAccepted:
                    voice = MapShotVoice(ev);
                    return true;
                case CombatEventKind.ProjectileSpawned:
                    voice = SynthVoiceKind.GrenadeThrow;
                    return true;
                case CombatEventKind.Impact:
                    if (IsGrenadeBlast(ev))
                    {
                        voice = SynthVoiceKind.GrenadeExplode;
                        return true;
                    }
                    if (knifeHit)
                    {
                        // Flesh is voiced by the paired DamageApplied; only a blade on hard surface plays here.
                        voice = SynthVoiceKind.KnifeHitHard;
                        return !ev.Target.IsValid;
                    }
                    voice = IsHeadRegion(ev) ? SynthVoiceKind.Headshot : SynthVoiceKind.Impact;
                    return true;
                case CombatEventKind.DamageApplied:
                    if (knifeHit)
                    {
                        voice = SynthVoiceKind.KnifeHitFlesh;
                        return true;
                    }
                    // Punchier hit report; head uses a brighter crack than body hits.
                    voice = IsHeadRegion(ev) ? SynthVoiceKind.Headshot : SynthVoiceKind.DamageTaken;
                    return true;
                case CombatEventKind.ReloadStarted:
                    voice = MapReloadVoice(ev);
                    return true;
                case CombatEventKind.ReloadCompleted:
                    voice = SynthVoiceKind.ReloadComplete;
                    return true;
                case CombatEventKind.Death:
                    voice = SynthVoiceKind.Death;
                    return true;
                default:
                    voice = default;
                    return false;
            }
        }

        /// <summary>Shot voice from the weapon's visual profile (ADR-077 <c>audio.shot</c>); unknown ids use the rifle voice.</summary>
        public static SynthVoiceKind MapShotVoice(CombatEvent ev)
        {
            if (ev.Payload is ShotPayload shot
                && WeaponVisualProfiles.TryGet(shot.WeaponId, out var profile))
                return profile.ShotVoice;
            return SynthVoiceKind.RifleShot;
        }

        /// <summary>Reload Foley set by the reloading slot's weapon family: sidearm vs long gun.</summary>
        public static SynthVoiceKind MapReloadVoice(CombatEvent ev)
        {
            if (ev.Payload is ActionPayload action
                && WeaponVisualProfiles.TryGet(WeaponVisualProfiles.DefaultIdForSlot(action.Slot), out var profile)
                && profile.Family == WeaponVisualFamily.Sidearm)
                return SynthVoiceKind.ReloadPistol;
            return SynthVoiceKind.ReloadStart;
        }

        private static bool IsKnife(DefinitionId weaponId) =>
            WeaponVisualProfiles.TryGet(weaponId, out var profile) && profile.Family == WeaponVisualFamily.Melee;

        /// <summary>
        /// Authority grenade blast Impact uses invalid target, upward normal, surfaceTag=1, region=0
        /// (see CombatHarnessSimulation.ResolveExplosion).
        /// </summary>
        public static bool IsGrenadeBlastImpact(CombatEvent ev)
        {
            if (ev.Kind != CombatEventKind.Impact || ev.Target.IsValid) return false;
            if (ev.Payload is not ImpactPayload impact) return false;
            if (impact.SurfaceTag != 1 || impact.RegionId != 0) return false;
            return Math.Abs(impact.Normal.X) < 1e-3f
                && impact.Normal.Y > 0.95f
                && Math.Abs(impact.Normal.Z) < 1e-3f;
        }

        private static bool IsGrenadeBlast(CombatEvent ev) => IsGrenadeBlastImpact(ev);

        private static bool IsHeadRegion(CombatEvent ev)
        {
            if (ev.Payload is ImpactPayload impact) return impact.RegionId == HeadRegionId;
            if (ev.Payload is DamagePayload damage) return damage.RegionId == HeadRegionId;
            return false;
        }

        private Float3 EventPosition(CombatEvent ev, Float3 fallback)
        {
            if (ev.Payload is ShotPayload shot) return shot.Origin;
            if (ev.Payload is ProjectilePayload projectile) return projectile.Origin;
            if (ev.Payload is ImpactPayload impact) return impact.Position;
            if (ev.Kind == CombatEventKind.ReloadStarted || ev.Kind == CombatEventKind.ReloadCompleted)
            {
                if (ev.Instigator.IsValid && lastFeet.TryGetValue(ev.Instigator.Value, out var feet))
                    return EarHeight(feet);
            }
            if (ev.Kind == CombatEventKind.Death && ev.Target.IsValid && lastFeet.TryGetValue(ev.Target.Value, out var victim))
                return EarHeight(victim);
            if (ev.Kind == CombatEventKind.DamageApplied && ev.Target.IsValid && lastFeet.TryGetValue(ev.Target.Value, out var damaged))
                return EarHeight(damaged);
            return EarHeight(fallback);
        }

        private static Float3 EarHeight(Float3 feet) => new Float3(feet.X, feet.Y + 1.35f, feet.Z);

        public static float ComputePan(Float3 listener, Float3 source, float yawRadians)
        {
            float dx = source.X - listener.X;
            float dz = source.Z - listener.Z;
            float horiz = (float)Math.Sqrt(dx * dx + dz * dz);
            if (horiz < 1e-4f) return 0f;
            return Math.Clamp((dx * (float)Math.Cos(yawRadians) - dz * (float)Math.Sin(yawRadians)) / horiz, -1f, 1f);
        }

        private static ulong Seed(CombatEvent ev) => ev.Id.Tick ^ ((ulong)ev.Id.Ordinal << 16) ^ ev.Id.EntityId.Value;

        /// <summary>Variation seed; the primary cue keeps the pre-ADR-096 seed, secondary layers decorrelate.</summary>
        private static ulong CueSeed(CombatEvent ev, SynthVoiceKind voice) =>
            Seed(ev) ^ (voice == SynthVoiceKind.GrenadeExplodeFar ? 0x5EEDUL : 0UL);

        private static string EventKey(EventId id) =>
            id.AuthoritySessionId + ":" + id.Tick + ":" + id.EntityId.Value + ":" + id.Ordinal;

        private static float DistanceMeters(Float3 a, Float3 b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static uint NextRaw(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }

        private static float NextUnit(ref uint state) => (NextRaw(ref state) & 0xFFFFFF) / (float)0x1000000;
    }
}
