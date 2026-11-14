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
        public const float MaxAudibleDistanceMeters = 80f;

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

        public int ConsumeCombatEvents(IReadOnlyList<CombatEvent> events, Float3 listenerFeet)
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

                if (!TryMapCombat(ev.Kind, out var voice)) continue;
                float distance = DistanceMeters(listenerFeet, EventPosition(ev, listenerFeet));
                if (distance > MaxAudibleDistanceMeters) continue;
                ulong seed = ev.Id.Tick ^ ((ulong)ev.Id.Ordinal << 16) ^ ev.Id.EntityId.Value;
                if (mixer.TryPlay(voice, distance, seed)) played++;
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
                    if (distance > MaxAudibleDistanceMeters) continue;
                    ulong seed = attachedSessionId ^ id ^ completedTick;
                    if (mixer.TryPlay(SynthVoiceKind.Footstep, distance, seed)) played++;
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
            return mixer.TryPlay(SynthVoiceKind.Ui, 0f, seed ^ ordinal);
        }

        private static bool TryMapCombat(CombatEventKind kind, out SynthVoiceKind voice)
        {
            switch (kind)
            {
                case CombatEventKind.ShotAccepted:
                    voice = SynthVoiceKind.Shot;
                    return true;
                case CombatEventKind.Impact:
                case CombatEventKind.DamageApplied:
                    voice = SynthVoiceKind.Impact;
                    return true;
                default:
                    voice = default;
                    return false;
            }
        }

        private static Float3 EventPosition(CombatEvent ev, Float3 fallback)
        {
            if (ev.Payload is ShotPayload shot) return shot.Origin;
            if (ev.Payload is ImpactPayload impact) return impact.Position;
            return fallback;
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
