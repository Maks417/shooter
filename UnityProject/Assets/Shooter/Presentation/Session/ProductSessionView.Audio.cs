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
    /// <summary>Match audio: bank and music resolution, per-frame combat audio pump, acoustics probes and UI cues.</summary>
    public sealed partial class ProductSessionView : MonoBehaviour
    {
        private static string? ResolveCombatAudioRoot()
        {
            try
            {
                string installRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                    Application.dataPath, Application.isEditor ? "../.." : ".."));
                string content = System.IO.Path.Combine(installRoot, "Content", "Audio", "combat");
                if (System.IO.Directory.Exists(content)
                    && System.IO.File.Exists(System.IO.Path.Combine(content, CombatAudioSamplePaths.ManifestFileName)))
                    return content;
                string streaming = System.IO.Path.Combine(Application.streamingAssetsPath, "Audio", "combat");
                if (System.IO.Directory.Exists(streaming)
                    && System.IO.File.Exists(System.IO.Path.Combine(streaming, CombatAudioSamplePaths.ManifestFileName)))
                    return streaming;
            }
            catch (System.Exception)
            {
                // Fall through to BCL path walk / generated bank.
            }
            return CombatAudioSamplePaths.ResolveCombatSampleRoot();
        }

        private static string? ResolveMusicFolder()
        {
            string? combat = ResolveCombatAudioRoot();
            if (combat == null) return null;
            string? parent = System.IO.Path.GetDirectoryName(combat);
            return parent == null ? null : System.IO.Path.Combine(parent, "music");
        }

        /// <summary>Menu/shell UI cue on the session-independent UI mixer (hover, click, back).</summary>
        public bool PlayShellUi(SynthVoiceKind kind)
        {
            if (shellUiMixer == null || !SynthAudioMixer.IsUiKind(kind)) return false;
            shellUiOrdinal++;
            return shellUiMixer.TryPlay(kind, 0f, 0f, shellUiOrdinal * 0x9E3779B97F4A7C15UL);
        }

        /// <summary>Advance the music director for this frame (shell-owned only). Never touches authority.</summary>
        public MusicCue UpdateMusic(MusicContext context, bool tension, LocalMatchOutcome outcome, bool paused)
        {
            if (musicOutput == null) return MusicCue.None;
            float duck = mixer != null ? mixer.LoudCueLevel : 0f;
            var command = musicDirector.Update(new MusicInput(context, tension, outcome, paused, duck, musicOutput.CurrentCueEnded));
            musicOutput.Apply(command);
            return command.Cue;
        }

        /// <summary>Presentation-originated local cue (grenade pin, dry fire, weapon switch) while a match is attached.</summary>
        public bool PlayLocalCue(SynthVoiceKind kind, ulong seed) => audioAttached && EnsureAudio().audio.PlayLocal(kind, seed);

        private void PumpProductAudio(Float3 listenerFeet, IReadOnlyList<CombatEvent>? combatEvents)
        {
            if (session == null) return;
            if (session.Phase == GameMatchPhase.Stopped || session.Phase == GameMatchPhase.Loading)
            {
                DetachAudioSession();
                return;
            }

            ulong sid = session.AuthoritySessionId;
            if (sid == 0)
            {
                DetachAudioSession();
                return;
            }
            var router = EnsureAudio().audio;
            if (!audioAttached || audioSessionId != sid)
            {
                router.AttachSession(sid);
                audioSessionId = sid;
                audioAttached = true;
                audioOutput?.ArmOutput();
            }

            UpdateAudioAcoustics(router, listenerFeet);
            if (session.Phase == GameMatchPhase.Playing && session.PauseState == SessionPauseState.Running)
            {
                float dt = Time.unscaledDeltaTime;
                router.TickDistantBattle(dt);
                router.TickScheduled(dt, listenerFeet);
                if (session.TryGetActorState(session.HumanEntityId, out var localHuman))
                {
                    bool firePressed = cursorLocked && input.Bindings.WasPressedThisFrame("Fire");
                    int cues = localWeaponCues.Step(localHuman, firePressed, dt, localCueScratch);
                    for (int c = 0; c < cues; c++)
                        router.PlayLocal(localCueScratch[c], session.CompletedTick ^ ((ulong)c << 8));
                }
            }

            if (combatEvents != null)
            {
                router.ConsumeCombatEvents(combatEvents, listenerFeet, session.HumanEntityId);
                for (int i = 0; i < combatEvents.Count; i++)
                {
                    var ev = combatEvents[i];
                    string key = ev.Id.AuthoritySessionId + ":" + ev.Id.Tick + ":" + ev.Id.EntityId + ":" + ev.Id.Ordinal;
                    if (!accessibilityCueKeys.Add(key)) continue;
                    NotifyAccessibilityCue(ev);
                    ObserveCombatFx(ev);
                    ObserveHitFeedback(ev);
                    ObserveGunfireReveal(ev);
                }
            }

            footstepScratch.Clear();
            var snap = session.TryAcquireSnapshot();
            if (snap != null)
            {
                for (int i = 0; i < snap.Participants.Count; i++)
                {
                    var p = snap.Participants[i];
                    if (!p.Alive || !p.EntityId.IsValid) continue;
                    if (!session.TryGetActorState(p.EntityId, out var state) || !state.Alive) continue;
                    footstepScratch.Add(state);
                }
            }
            router.TickFootsteps(footstepScratch, listenerFeet, session.CompletedTick, session.HumanEntityId);
        }

        private void UpdateAudioAcoustics(ProductAudioEventRouter router, Float3 listenerFeet)
        {
            audioProbesRemaining = AudioProbeBudget;
            Vector3 ears = new Vector3(listenerFeet.X, listenerFeet.Y + 1.35f, listenerFeet.Z);
            if ((ears - acousticListenerPosition).sqrMagnitude > 1f)
            {
                audioObstructions.Clear();
                nextAcousticProbe = 0f;
                acousticListenerPosition = ears;
            }
            if (Time.unscaledTime >= nextAcousticProbe)
            {
                nextAcousticProbe = Time.unscaledTime + 0.2f;
                // Authoritative map solids only (layer 8); ignore actors, triggers and cosmetic meshes.
                // Fixed ray count and cadence are independent of graphics quality.
                int nearby = 0;
                if (Physics.Raycast(ears, Vector3.up, 7f, 1 << 8, QueryTriggerInteraction.Ignore)) nearby += 2;
                if (Physics.Raycast(ears, Vector3.right, 10f, 1 << 8, QueryTriggerInteraction.Ignore)) nearby++;
                if (Physics.Raycast(ears, Vector3.left, 10f, 1 << 8, QueryTriggerInteraction.Ignore)) nearby++;
                audioProbesRemaining -= 3;
                audioReflection = nearby / 4f;
            }
            float yaw = camera != null ? camera.transform.eulerAngles.y * Mathf.Deg2Rad : 0f;
            router.SetListenerAcoustics(yaw, audioReflection, listenerFeet);
        }

        private float ProbeAudioObstruction(Float3 listener, Float3 source)
        {
            var point = new Vector3(source.X, source.Y, source.Z);
            var key = new Vector3Int(Mathf.FloorToInt(point.x * 2f), Mathf.FloorToInt(point.y * 2f), Mathf.FloorToInt(point.z * 2f));
            if (audioObstructions.TryGetValue(key, out var cached) && cached.expires > Time.unscaledTime)
                return cached.value;
            if (audioProbesRemaining <= 0) return 0f;
            audioProbesRemaining--;
            Vector3 start = new Vector3(listener.X, listener.Y, listener.Z);
            Vector3 delta = point - start;
            float distance = delta.magnitude;
            float obstruction = distance > 0.5f && Physics.Raycast(start, delta / distance,
                distance - 0.15f, 1 << 8, QueryTriggerInteraction.Ignore) ? 1f : 0f;
            if (audioObstructions.Count >= 128) audioObstructions.Clear();
            audioObstructions[key] = (obstruction, Time.unscaledTime + 0.15f);
            return obstruction;
        }

        private void DetachAudioSession()
        {
            var router = audio;
            if (!audioAttached && (router == null || !router.IsAttached)) return;
            router?.DetachSession();
            audioObstructions.Clear();
            nextAcousticProbe = 0f;
            audioReflection = 0f;
            audioAttached = false;
            audioSessionId = 0;
            localWeaponCues.Reset();
            accessibilityCueKeys.Clear();
            ClearGunfireReveals();
            audioOutput?.Detach();
        }

        public bool PlayUiCue(ulong seed)
        {
            if (subtitlesEnabled) ShowSubtitle("UI");
            return EnsureAudio().audio.PlayUi(seed);
        }
    }
}
