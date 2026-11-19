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
    /// <summary>Camera and user settings: field of view, ADS scope picture-in-picture, visual review mode.</summary>
    public sealed partial class ProductSessionView : MonoBehaviour
    {
        /// <summary>
        /// T062 capture/overlay on the existing T056 presentation path. Enable with
        /// <c>-shooterVisualReview 1</c> and optional <c>-shooterVisualReviewOutput &lt;Artifacts/...&gt;</c>.
        /// Does not reopen P05 clearance search.
        /// </summary>
        public ProductVisualReviewOverlay EnableVisualReview(string? outputDirectory = null)
        {
            if (actors == null || camera == null)
                throw new System.InvalidOperationException("Product presentation is not initialized.");
            ProductVisualDefectCatalog.ValidateImmutableP05Fail();
            visualReview = ProductVisualReviewOverlay.Attach(transform, actors, camera, outputDirectory ?? "");
            visualReview.SetContext(boundMapId, QualitySettings.names[QualitySettings.GetQualityLevel()], 0);
            return visualReview;
        }

        private void TryEnableVisualReviewFromCommandLine()
        {
            string enabled = ReadCommandLine("-shooterVisualReview", "0");
            if (enabled != "1" && !string.Equals(enabled, "true", System.StringComparison.OrdinalIgnoreCase))
                return;
            string output = ReadCommandLine("-shooterVisualReviewOutput", "");
            EnableVisualReview(output);
        }

        private static string ReadCommandLine(string name, string fallback)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] != name) continue;
                if (i + 1 >= args.Length) throw new System.ArgumentException("Missing value for " + name);
                return args[i + 1];
            }
            return fallback;
        }

        /// <summary>
        /// Apply non-display UserSettings (audio/FOV/input/HUD/bindings/accessibility).
        /// Display surface is owned by <see cref="ProductClientSettingsApplier"/>.
        /// </summary>
        public void ApplyUserSettings(UserSettings settings)
        {
            if (settings == null) throw new System.ArgumentNullException(nameof(settings));
            UserSettingsAudioBinding.ApplyGains(settings, EnsureAudio().mixer);
            damageAudioGain = Mathf.Clamp01(settings.MasterVolume) * Mathf.Clamp01(settings.EffectsVolume);
            if (shellUiMixer != null) UserSettingsAudioBinding.ApplyGains(settings, shellUiMixer);
            musicOutput?.SetChannelGain(UserSettingsAudioBinding.MusicGain(settings));
            string qualityName = settings.QualityTier < QualitySettings.names.Length
                ? QualitySettings.names[settings.QualityTier]
                : "Low";
            hud.ApplySettings(new HudSettings(
                settings.UiScale,
                settings.HipFovDegrees,
                qualityName,
                settings.ReducedCameraMotion,
                settings.Contrast));
            adsFovDegrees = AdsOpticVisual.AdsFieldOfViewDegrees(
                settings.HipFovDegrees, ProductAuthorityActionView.RifleSlot);
            input.Sensitivity = settings.MouseSensitivity;
            input.InvertY = settings.InvertY;
            input.ApplyBindings(settings.Bindings);
            reducedCameraMotion = settings.ReducedCameraMotion;
            reducedFlash = settings.ReducedFlash;
            subtitlesEnabled = settings.Subtitles;
            if (!subtitlesEnabled)
            {
                subtitleText = "";
                subtitleRemaining = 0f;
            }
            if (reducedFlash)
            {
                muzzleFlashRemaining = 0f;
                analyticFxPresenter?.Sync(suppressMuzzleFlash: true);
            }
            if (camera != null && !IsAiming())
                camera.fieldOfView = settings.HipFovDegrees;
        }

        private bool IsAiming() => input.AimHeld && cursorLocked;

        private void ApplyPresentationFov(float adsPresentationBlend, byte weaponSlot, DefinitionId weaponId = default)
        {
            if (camera == null) return;
            // ADR-107: the mounted sight decides optic mode and ADS framing for the gun actually shown.
            var profile = actors != null && actors.LocalVisibleWeaponId.Equals(weaponId) && actors.LocalVisibleWeaponSlot == weaponSlot
                ? actors.LocalVisibleProfile
                : WeaponVisualProfiles.Resolve(weaponId, weaponSlot);
            float hip = hud.Settings.HipFov;
            float ads = AdsOpticVisual.AdsFieldOfViewDegrees(hip, profile);
            adsFovDegrees = ads;
            // Scoped PiP keeps hip FOV; digital zoom lives in AdsScopePipPresenter.
            // Reduced camera motion keeps hip FOV for every weapon.
            if (reducedCameraMotion || AdsOpticVisual.KeepsHipCameraFov(profile))
                camera.fieldOfView = hip;
            else
                camera.fieldOfView = AdsOpticVisual.BlendedFieldOfViewDegrees(hip, ads, adsPresentationBlend);
            ApplyScopePip(adsPresentationBlend, profile);
        }

        private void ApplyScopePip(float adsPresentationBlend, in WeaponVisualProfile profile)
        {
            if (scopePip == null) return;
            float hip = hud.Settings.HipFov;
            if (!AdsOpticVisual.UsesScopedOptic(profile))
            {
                scopePip.Configure(0f, 1f, AdsOpticVisual.LensRadiusFraction);
                return;
            }
            float strength = AdsOpticVisual.PipStrength(adsPresentationBlend, reducedCameraMotion);
            float zoom = reducedCameraMotion
                ? 1f
                : AdsOpticVisual.LensUvZoom(hip, profile, adsPresentationBlend);
            // ADR-103: real optical lens camera once the rifle mesh is hidden; same framing as the digital zoom.
            bool optical = AdsOpticVisual.UsesLensCamera(profile, adsPresentationBlend, reducedCameraMotion);
            float lensFov = AdsOpticVisual.LensCameraFovDegrees(hip, AdsOpticVisual.LensRadiusFraction, zoom);
            scopePip.Configure(strength, zoom, AdsOpticVisual.LensRadiusFraction, optical, lensFov);
        }
    }
}
