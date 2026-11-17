#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Shooter.Presentation.Audio
{
    /// <summary>
    /// Music path (ADR-096): two 2D <see cref="AudioSource"/>s crossfading between <see cref="MusicDirector"/> cues.
    /// OGG tracks from <c>Content/Audio/music</c> stay Vorbis-compressed in memory and decode during playback,
    /// so music never passes through the 24-voice SFX mixer. A missing folder or bad hash means silence plus
    /// one warning, never a failure.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnityMusicOutput : MonoBehaviour
    {
        private sealed class Load
        {
            public UnityWebRequest? Request;
            public AudioClip? Clip;
            public bool Failed;
        }

        private readonly Dictionary<MusicCue, Load> loads = new Dictionary<MusicCue, Load>();
        private AudioSource? a;
        private AudioSource? b;
        private AudioSource? active;
        private MusicManifest? manifest;
        private string folder = "";
        private MusicCue playingCue = MusicCue.None;
        private MusicCue wantedCue = MusicCue.None;
        private bool wantedLoop;
        private float wantedGain;
        private float crossfadeSeconds = 1f;
        private float fadeIn = 1f;
        private float channelGain = 0.6f;
        private bool cueStarted;
        private bool warned;

        public MusicCue PlayingCue => playingCue;
        public bool HasManifest => manifest != null;
        /// <summary>True once a non-looping cue has played through to its end.</summary>
        public bool CurrentCueEnded { get; private set; }

        public static UnityMusicOutput Create(Transform parent, string? musicFolder = null)
        {
            var go = new GameObject("Music output");
            go.transform.SetParent(parent, false);
            var output = go.AddComponent<UnityMusicOutput>();
            output.Initialize(musicFolder);
            return output;
        }

        private void Initialize(string? musicFolder)
        {
            EnsureListener();
            a = MakeSource();
            b = MakeSource();
            active = a;
            try
            {
                string? resolved = MusicManifest.ResolveFolder(musicFolder);
                if (resolved == null)
                {
                    Warn("SHOOTER_MUSIC_MISSING no Content/Audio/music folder; menu and match music are silent.");
                    return;
                }
                var problems = new List<string>();
                manifest = MusicManifest.LoadVerified(resolved, problems);
                folder = resolved;
                if (manifest == null) Warn("SHOOTER_MUSIC_MISSING manifest.json absent in " + resolved);
                else if (problems.Count > 0) Warn("SHOOTER_MUSIC_SKIPPED " + string.Join("; ", problems));
                else Debug.Log("SHOOTER_MUSIC bank=" + manifest.BankId + " tracks=" + manifest.Tracks.Count);
            }
            catch (Exception ex)
            {
                manifest = null;
                Warn("SHOOTER_MUSIC_INVALID " + ex.Message);
            }
        }

        /// <summary>Master × music from user settings; 0 mutes.</summary>
        public void SetChannelGain(float gain) => channelGain = Mathf.Clamp01(gain);

        /// <summary>Apply the director's command for this frame.</summary>
        public void Apply(in MusicCommand command)
        {
            wantedGain = command.Gain;
            wantedLoop = command.Loop;
            if (command.Cue == wantedCue) return;
            wantedCue = command.Cue;
            crossfadeSeconds = Mathf.Max(0.05f, command.CrossfadeSeconds);
            CurrentCueEnded = false;
            if (wantedCue != MusicCue.None) BeginLoad(wantedCue);
            // Preload the likely successors so the switch does not wait on decoding.
            if (wantedCue == MusicCue.MatchCalm) BeginLoad(MusicCue.MatchTension);
        }

        private void Update()
        {
            if (a == null || b == null || active == null) return;
            float dt = Time.unscaledDeltaTime;

            if (playingCue != wantedCue)
            {
                AudioClip? clip = null;
                bool ready = wantedCue == MusicCue.None || TryGetClip(wantedCue, out clip);
                // Start only once the clip's audio data is resident; Play() on an unloaded clip is silent.
                if (ready && clip != null && clip.loadState != AudioDataLoadState.Loaded)
                {
                    if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
                    if (clip.loadState == AudioDataLoadState.Failed)
                    {
                        Warn("SHOOTER_MUSIC_DECODE " + wantedCue + ": audio data failed to load");
                        clip = null;
                    }
                    else ready = false;
                }
                if (ready)
                {
                    var next = active == a ? b : a;
                    next.Stop();
                    next.clip = clip;
                    next.loop = wantedLoop;
                    next.volume = 0f;
                    if (clip != null)
                    {
                        next.Play();
                        Debug.Log("SHOOTER_MUSIC_PLAY cue=" + wantedCue + " loadType=" + clip.loadType +
                                  " length=" + clip.length.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) +
                                  " state=" + clip.loadState + " playing=" + (next.isPlaying ? 1 : 0) +
                                  " gain=" + (wantedGain * channelGain).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
                    }
                    active = next;
                    playingCue = wantedCue;
                    fadeIn = 0f;
                    cueStarted = clip != null;
                }
            }

            fadeIn = Mathf.Min(1f, fadeIn + dt / crossfadeSeconds);
            var other = active == a ? b : a;
            float target = wantedGain * channelGain;
            active.volume = target * fadeIn;
            active.loop = wantedLoop;
            other.volume = Mathf.Max(0f, other.volume - dt / crossfadeSeconds * Mathf.Max(0.05f, other.volume));
            if (other.volume <= 0.001f && other.isPlaying) other.Stop();
            ApplyLoopRegion(active);

            if (cueStarted && !active.loop && !active.isPlaying && Application.isFocused) CurrentCueEnded = true;
        }

        private void ApplyLoopRegion(AudioSource source)
        {
            if (manifest == null || !source.loop || source.clip == null || !source.isPlaying) return;
            if (!manifest.TryGet(playingCue, out var track) || track.LoopEndSeconds <= 0f) return;
            if (source.time >= track.LoopEndSeconds) source.time = track.LoopStartSeconds + (source.time - track.LoopEndSeconds);
        }

        private bool TryGetClip(MusicCue cue, out AudioClip? clip)
        {
            clip = null;
            if (!loads.TryGetValue(cue, out var load))
            {
                // Not in the manifest: treat as silence rather than waiting forever.
                return manifest == null || !manifest.TryGet(cue, out _);
            }
            if (load.Failed) return true;
            if (load.Clip != null) { clip = load.Clip; return true; }
            var request = load.Request;
            if (request == null || !request.isDone) return false;
            if (request.result != UnityWebRequest.Result.Success)
            {
                load.Failed = true;
                Warn("SHOOTER_MUSIC_DECODE " + cue + ": " + request.error);
            }
            else
            {
                load.Clip = DownloadHandlerAudioClip.GetContent(request);
                load.Clip.name = "Music " + cue;
                clip = load.Clip;
                // Keep the request (and its download handler) alive for the clip's lifetime; OnDestroy disposes it.
                return true;
            }
            request.Dispose();
            load.Request = null;
            return true;
        }

        private void BeginLoad(MusicCue cue)
        {
            if (manifest == null || loads.ContainsKey(cue) || !manifest.TryGet(cue, out var track)) return;
            string path = System.IO.Path.Combine(folder, track.File);
            var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, AudioType.OGGVORBIS);
            if (request.downloadHandler is DownloadHandlerAudioClip handler)
            {
                // Keep Vorbis in memory and decode while playing: a few MB per track instead of tens of MB of PCM.
                // Streaming must be switched off first: Unity rejects `compressed` while streamAudio is on
                // ("Audio compression is not valid as streamAudio is set to true").
                handler.streamAudio = false;
                handler.compressed = true;
            }
            request.SendWebRequest();
            loads[cue] = new Load { Request = request };
        }

        /// <summary>
        /// Clip playback needs an <see cref="AudioListener"/> and the code-built scene has none. All product sources
        /// are 2D, so the listener lives on this long-lived object (menu cameras come and go) unless one exists.
        /// </summary>
        private void EnsureListener()
        {
            if (FindAnyObjectByType<AudioListener>() != null) return;
            gameObject.AddComponent<AudioListener>();
            Debug.Log("SHOOTER_AUDIO_LISTENER host=" + gameObject.name);
        }

        private AudioSource MakeSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.priority = 0;
            source.volume = 0f;
            source.bypassReverbZones = true;
            source.ignoreListenerPause = true;
            return source;
        }

        private void Warn(string message)
        {
            if (warned) return;
            warned = true;
            Debug.LogWarning(message);
        }

        private void OnDestroy()
        {
            foreach (var load in loads.Values)
            {
                load.Request?.Abort();
                load.Request?.Dispose();
                if (load.Clip != null) Destroy(load.Clip);
            }
            loads.Clear();
        }
    }
}
