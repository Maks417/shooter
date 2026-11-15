#nullable enable
using UnityEngine;

namespace Shooter.Presentation.Audio
{
    /// <summary>
    /// Actual Unity device path: AudioSource + OnAudioFilterRead feeding device-rate sample-bank PCM (ADR-055).
    /// Callback only mixes pre-queued voices; never touches authority or allocates.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnitySynthAudioOutput : MonoBehaviour
    {
        private SynthAudioMixer? mixer;
        private AudioSource? source;
        private bool deviceWarned;
        private bool outputEnabled = true;
        private volatile bool callbackArmed;
        private volatile int deviceSampleRate = 48000;

        public SynthAudioMixer? Mixer => mixer;
        public bool IsOutputEnabled => outputEnabled;
        public bool DeviceWarningRaised => deviceWarned;
        public bool CallbackArmed => callbackArmed;

        public static UnitySynthAudioOutput Attach(GameObject host, SynthAudioMixer mixer)
        {
            if (host == null) throw new System.ArgumentNullException(nameof(host));
            if (mixer == null) throw new System.ArgumentNullException(nameof(mixer));
            var existing = host.GetComponent<UnitySynthAudioOutput>();
            var output = existing != null ? existing : host.AddComponent<UnitySynthAudioOutput>();
            output.Bind(mixer);
            return output;
        }

        public void Bind(SynthAudioMixer mixer)
        {
            this.mixer = mixer ?? throw new System.ArgumentNullException(nameof(mixer));
            EnsureSource();
            ArmOutput();
        }

        public void ArmOutput()
        {
            EnsureSource();
            if (source == null) return;
            outputEnabled = true;
            callbackArmed = true;
            if (!source.isPlaying) source.Play();
            ProbeDevice();
        }

        public void Detach()
        {
            callbackArmed = false;
            outputEnabled = false;
            mixer?.StopAll();
            if (source != null && source.isPlaying) source.Stop();
        }

        private void EnsureSource()
        {
            if (source != null) return;
            source = gameObject.GetComponent<AudioSource>();
            if (source == null) source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 1f;
            source.mute = false;
            source.priority = 0;
            // Silence clip keeps the filter callback pumping; audible content comes from the mixer bank.
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            deviceSampleRate = rate;
            var clip = AudioClip.Create("ShooterSynthSilence", rate, 1, rate, false);
            var zeros = new float[rate];
            clip.SetData(zeros, 0);
            source.clip = clip;
        }

        private void ProbeDevice()
        {
            if (deviceWarned) return;
            var config = AudioSettings.GetConfiguration();
            if (config.sampleRate <= 0 || AudioSettings.outputSampleRate <= 0)
            {
                deviceWarned = true;
                Debug.LogWarning("SHOOTER_AUDIO_DEVICE_MISSING recoverable; play continues without claiming audible verification.");
            }
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!callbackArmed || !outputEnabled || mixer == null || data == null || channels < 1)
            {
                if (data != null)
                    System.Array.Clear(data, 0, data.Length);
                return;
            }
            // Configuration is cached on the main thread; callback invokes no Unity API.
            mixer.Fill(data, channels, deviceSampleRate);
        }

        private void RefreshDeviceRate(bool deviceChanged)
        {
            int rate = AudioSettings.outputSampleRate;
            deviceSampleRate = rate >= 8000 ? rate : 48000;
        }

        private void OnEnable()
        {
            AudioSettings.OnAudioConfigurationChanged += RefreshDeviceRate;
            RefreshDeviceRate(false);
            callbackArmed = outputEnabled && mixer != null;
            if (callbackArmed) ArmOutput();
        }

        private void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged -= RefreshDeviceRate;
            callbackArmed = false;
        }

        private void OnDestroy()
        {
            Detach();
            if (source != null && source.clip != null)
            {
                var clip = source.clip;
                source.clip = null;
                Destroy(clip);
            }
        }
    }
}
