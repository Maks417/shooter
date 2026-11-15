#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Game.Contracts;

namespace Shooter.Presentation.Audio
{
    /// <summary>
    /// Consumes ordered read-only product audio cues once (combat event IDs + footstep strides + UI).
    /// Presentation-only; never mutates authority. Clears on session detach so stale sounds stop.
    /// </summary>
    public sealed class ProductAudioEventRouter
    {
        public const float FootstepStrideMeters = 0.75f;
        /// <summary>Legacy gunfire ceiling; prefer <see cref="CombatAudioDistance.MaxAudibleMeters"/>.</summary>
        public const float MaxAudibleDistanceMeters = CombatAudioDistance.GunfireMaxMeters;
        /// <summary>Gameplay pose region 1 = head (DamageMath / hit volumes).</summary>
        public const ushort HeadRegionId = 1;
        public const string RifleWeaponId = "weapons/rifle";
        public const string PistolWeaponId = "weapons/pistol";
        public const string KnifeWeaponId = "weapons/knife";
        public const string GrenadeWeaponId = "weapons/grenade";

        private readonly SynthAudioMixer mixer;
        private readonly HashSet<string> consumed = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<ulong, float> strideAccum = new Dictionary<ulong, float>();
        private readonly Dictionary<ulong, Float3> lastFeet = new Dictionary<ulong, Float3>();
        private ulong attachedSessionId;
        private bool attached;
        private ushort uiOrdinal;

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
            mixer.EnsureAmbient(authoritySessionId);
        }

        public void DetachSession()
        {
            attached = false;
            attachedSessionId = 0;
            consumed.Clear();
            strideAccum.Clear();
            lastFeet.Clear();
            uiOrdinal = 0;
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

                if (!TryMapCombat(ev, localEntity, out var voice)) continue;
                Float3 pos = EventPosition(ev, listenerFeet);
                float distance = DistanceMeters(listenerFeet, pos);
                float max = CombatAudioDistance.MaxAudibleMeters(voice);
                if (max > 0f && distance > max) continue;
                float pan = ComputePan(listenerFeet, pos);
                ulong seed = ev.Id.Tick ^ ((ulong)ev.Id.Ordinal << 16) ^ ev.Id.EntityId.Value;
                if (mixer.TryPlay(voice, distance, pan, seed)) played++;
            }
            return played;
        }

        public int TickFootsteps(IReadOnlyList<ActorLifecycleState> living, Float3 listenerFeet, uint completedTick)
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
                float step = DistanceMeters(prev, actor.Feet);
                lastFeet[id] = actor.Feet;
                if (step < 1e-4f) continue;
                float accum = (strideAccum.TryGetValue(id, out var a) ? a : 0f) + step;
                while (accum >= FootstepStrideMeters)
                {
                    accum -= FootstepStrideMeters;
                    string key = "fs:" + attachedSessionId + ":" + completedTick + ":" + id + ":" + consumed.Count;
                    if (!consumed.Add(key)) continue;
                    float distance = DistanceMeters(listenerFeet, actor.Feet);
                    if (distance > CombatAudioDistance.FootstepMaxMeters) continue;
                    float pan = ComputePan(listenerFeet, actor.Feet);
                    ulong seed = attachedSessionId ^ id ^ completedTick;
                    if (mixer.TryPlay(SynthVoiceKind.Footstep, distance, pan, seed)) played++;
                }
                strideAccum[id] = accum;
            }
            return played;
        }

        public bool PlayUi(ulong seed)
        {
            if (!attached) return false;
            ushort ordinal = uiOrdinal;
            if (uiOrdinal < ushort.MaxValue) uiOrdinal++;
            string key = "ui:" + attachedSessionId + ":" + ordinal;
            if (!consumed.Add(key)) return false;
            return mixer.TryPlay(SynthVoiceKind.Ui, 0f, 0f, seed ^ ordinal);
        }

        private static bool TryMapCombat(CombatEvent ev, EntityId localEntity, out SynthVoiceKind voice)
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
                    voice = IsHeadRegion(ev) ? SynthVoiceKind.Headshot : SynthVoiceKind.Impact;
                    return true;
                case CombatEventKind.DamageApplied:
                    // Punchier hit report; head uses a brighter crack than body hits.
                    _ = localEntity;
                    voice = IsHeadRegion(ev) ? SynthVoiceKind.Headshot : SynthVoiceKind.DamageTaken;
                    return true;
                case CombatEventKind.ReloadStarted:
                    voice = SynthVoiceKind.ReloadStart;
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

        private static SynthVoiceKind MapShotVoice(CombatEvent ev)
        {
            if (ev.Payload is ShotPayload shot)
            {
                string id = shot.WeaponId.ToString();
                if (string.Equals(id, PistolWeaponId, StringComparison.Ordinal))
                    return SynthVoiceKind.PistolShot;
                if (string.Equals(id, KnifeWeaponId, StringComparison.Ordinal))
                    return SynthVoiceKind.KnifeSwing;
                if (string.Equals(id, GrenadeWeaponId, StringComparison.Ordinal))
                    // Cooked in-hand boom is emitted as Impact/GrenadeExplode; ShotAccepted for grenade is unused.
                    return SynthVoiceKind.GrenadeThrow;
            }
            return SynthVoiceKind.RifleShot;
        }

        /// <summary>
        /// Authority grenade blast Impact uses invalid target, upward normal, surfaceTag=1, region=0
        /// (see CombatHarnessSimulation.ResolveExplosion).
        /// </summary>
        private static bool IsGrenadeBlast(CombatEvent ev)
        {
            if (ev.Kind != CombatEventKind.Impact || ev.Target.IsValid) return false;
            if (ev.Payload is not ImpactPayload impact) return false;
            if (impact.SurfaceTag != 1 || impact.RegionId != 0) return false;
            return Math.Abs(impact.Normal.X) < 1e-3f
                && impact.Normal.Y > 0.95f
                && Math.Abs(impact.Normal.Z) < 1e-3f;
        }

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
                    return feet;
            }
            if (ev.Kind == CombatEventKind.Death && ev.Target.IsValid && lastFeet.TryGetValue(ev.Target.Value, out var victim))
                return victim;
            if (ev.Kind == CombatEventKind.DamageApplied && ev.Target.IsValid && lastFeet.TryGetValue(ev.Target.Value, out var damaged))
                return damaged;
            return fallback;
        }

        private static float ComputePan(Float3 listener, Float3 source)
        {
            float dx = source.X - listener.X;
            float dz = source.Z - listener.Z;
            float horiz = (float)Math.Sqrt(dx * dx + dz * dz);
            if (horiz < 1e-4f) return 0f;
            return Math.Clamp(dx / horiz, -1f, 1f);
        }

        private static string EventKey(EventId id) =>
            id.AuthoritySessionId + ":" + id.Tick + ":" + id.EntityId.Value + ":" + id.Ordinal;

        private static float DistanceMeters(Float3 a, Float3 b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}
