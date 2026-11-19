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
using Shooter.Presentation.Style;
using Shooter.Presentation.View;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using EntityId = Shooter.Contracts.EntityId;

namespace Shooter.Presentation.Session
{
    /// <summary>In-match IMGUI HUD: reticle, ammo, health and armor, minimap, kill and score feeds, damage indicators, accessibility overlays.</summary>
    public sealed partial class ProductSessionView : MonoBehaviour
    {
        private void TickAccessibilityPresentation(float dt)
        {
            if (dt < 0f) dt = 0f;
            if (muzzleFlashRemaining > 0f)
            {
                muzzleFlashRemaining -= dt;
                if (muzzleFlashRemaining < 0f) muzzleFlashRemaining = 0f;
            }
            if (subtitleRemaining > 0f)
            {
                subtitleRemaining -= dt;
                if (subtitleRemaining <= 0f)
                {
                    subtitleRemaining = 0f;
                    subtitleText = "";
                }
            }
            ObserveMovementDust();
            analyticFxPool.Tick(dt);
            grenadeFlight.Tick(dt);
            analyticFxPresenter?.Sync(suppressMuzzleFlash: reducedFlash);
            grenadePresenter?.Sync();
        }

        private void NotifyAccessibilityCue(CombatEvent ev)
        {
            EntityId local = session != null && session.HumanEntityId.IsValid ? session.HumanEntityId : default;
            if (FirstPersonHitFeedback.IsLocalShot(ev.Kind, ev.Instigator, local)
                && !IsKnifeWeapon(ev))
            {
                // Cosmetic local view flash only; bot/remote ShotAccepted must not flash the screen.
                // ReducedFlash suppresses the overlay while preserving shot audio and world muzzle proxies.
                // Knife melee must not flash as a gunshot.
                if (!reducedFlash) muzzleFlashRemaining = 0.09f;
                if (subtitlesEnabled) ShowSubtitle("Shot");
            }
            else if (FirstPersonHitFeedback.IsLocalShot(ev.Kind, ev.Instigator, local) && IsKnifeWeapon(ev))
            {
                if (subtitlesEnabled) ShowSubtitle("Melee");
            }
            else if (ev.Kind == CombatEventKind.Impact || ev.Kind == CombatEventKind.DamageApplied)
            {
                if (subtitlesEnabled) ShowSubtitle(ev.Kind == CombatEventKind.Impact ? "Impact" : "Hit");
            }
            else if (ev.Kind == CombatEventKind.ReloadStarted && subtitlesEnabled)
                ShowSubtitle("Reloading");
            else if (ev.Kind == CombatEventKind.ReloadCompleted && subtitlesEnabled)
                ShowSubtitle("Reload complete");
        }

        /// <summary>Red arc segments around the reticle pointing at whoever just hit the local player.</summary>
        private void DrawDamageIndicators(ActorLifecycleState human)
        {
            float now = Time.unscaledTime;
            float cx = combatUi.Width * .5f, cy = combatUi.Height * .5f;
            for (int i = 0; i < damageSourceUntil.Length; i++)
            {
                float alpha = CombatHudVisual.DamageIndicatorAlpha(damageSourceUntil[i] - now);
                if (alpha <= 0.02f) continue;
                float angle = CombatHudVisual.DamageIndicatorAngle(human.AimYaw, human.Feet.X, human.Feet.Z, damageSourceX[i], damageSourceZ[i]);
                TacticalUi.DamageArc(cx, cy, angle, alpha * (reducedFlash ? .5f : 1f));
            }
        }

        private void ShowSubtitle(string text)
        {
            subtitleText = text ?? "";
            subtitleRemaining = 1.6f;
        }

        private void OnGUI()
        {
            DrawAccessibilityOverlays();
            DrawHitboxOverlay();
            // Shell host draws menus/scoreboard/results; keep a minimal combat HUD only while playing under shell.
            if (session == null) return;
            if (shellOwned)
            {
                if (!cursorLocked || session.Phase != GameMatchPhase.Playing || session.PauseState != SessionPauseState.Running) return;
                // Holding Tab shows the translucent live scoreboard; the combat HUD would paint across it.
                if (input.ScoreboardHeld) return;
                DrawCombatHud();
                return;
            }

            var snap = session.TryAcquireSnapshot();
            string line1 = "Shooter offline TDM";
            string line2 = session.Phase + " pause=" + session.PauseState;
            if (snap != null)
                line2 = "T1 " + snap.Team1Kills + "  T2 " + snap.Team2Kills + "  rem=" + snap.RemainingActiveTicks +
                        "  phase=" + snap.Phase;
            if (session.TryGetActorState(session.HumanEntityId, out var human))
            {
                GUI.Label(new Rect(24, Screen.height - 72, 640, 24),
                    "HP " + human.Health + "  Ammo " + human.Magazine + "/" + human.Reserve + "  slot " + human.ActiveWeaponSlot,
                    GuiStyle());
            }
            GUI.Label(new Rect(24, 24, 800, 24), line1, GuiStyle());
            GUI.Label(new Rect(24, 48, 800, 24), line2, GuiStyle());
            if (mapView != null && mapView.IsInstalled)
                GUI.Label(new Rect(24, 72, 900, 24),
                    "map " + boundMapId + " solids=" + mapView.SolidCount + " covers=" + mapView.CoverCount +
                    " actors=" + (actors?.LiveAvatarCount ?? 0) + " corpses=" + (actors?.CorpseAvatarCount ?? 0),
                    GuiStyle());
            if (session.PauseState == SessionPauseState.Paused)
                GUI.Label(new Rect(Screen.width * 0.5f - 80, Screen.height * 0.5f - 20, 200, 40), "PAUSED (Esc)", GuiStyle());
            var results = session.TryAcquireResults();
            if (results != null)
                GUI.Label(new Rect(24, 104, 900, 24),
                    "Result " + results.Outcome + " / " + results.Reason + "  " + results.Team1Kills + "-" + results.Team2Kills,
                    GuiStyle());
        }

        private void DrawCombatHud()
        {
            if (session == null) return;
            combatUi.ReducedMotion = reducedCameraMotion;
            combatUi.ReducedFlash = reducedFlash;
            combatUi.Begin(hud.Settings.UiScale, "combat", keyboardNavigation: false);
            try
            {
                Rect a = combatUi.Safe;
                var snap = session.TryAcquireSnapshot();
                bool alive = session.TryGetActorState(session.HumanEntityId, out var state) && state.Alive;
                if (!alive)
                {
                    lastSeenHealth = 100;
                    damagePulseUntil = 0f;
                    damageFlashUntil = 0f;
                    hitMarkerUntil = 0f;
                    cameraBob.Reset();
                    string respawn = ShellUiCopy.AwaitingSpawn;
                    if (snap != null)
                        for (int i = 0; i < snap.Participants.Count; i++)
                        {
                            var participant = snap.Participants[i];
                            if (!participant.IsHuman) continue;
                            uint due = participant.RespawnDueActiveTick;
                            uint remaining = Serial32.IsNewer(due, snap.ActiveTicksElapsed) ? Serial32.ForwardDistance(due, snap.ActiveTicksElapsed) : 0;
                            if (remaining > 0) respawn = ShellUiCopy.ReturningIn(remaining / 60f);
                        }
                    // Cinematic KIA: soft letterbox + centered status plate (code-only fills).
                    if (!reducedFlash)
                    {
                        TacticalUi.Fill(new Rect(0, 0, combatUi.Width, combatUi.Height * .16f), UiInk.Shade(.72f));
                        TacticalUi.Fill(new Rect(0, combatUi.Height * .84f, combatUi.Width, combatUi.Height * .16f), UiInk.Shade(.72f));
                    }
                    Rect panel = new Rect(combatUi.Width * .5f - 210, combatUi.Height * .5f - 58, 420, 116);
                    TacticalUi.CombatPlate(panel, strong: true);
                    TacticalUi.Fill(new Rect(panel.x, panel.y, 3, panel.height), TacticalUi.Enemy);
                    TacticalUi.Fill(new Rect(panel.x + 3, panel.y, panel.width - 3, 1), UiInk.Alpha(TacticalUi.Enemy, .5f));
                    combatUi.Label(new Rect(panel.x + 18, panel.y + 14, panel.width - 32, 16), ShellUiCopy.StatusLabel, 11, TacticalUi.Muted, TextAnchor.UpperLeft);
                    combatUi.Label(new Rect(panel.x + 18, panel.y + 34, panel.width - 32, 32), ShellUiCopy.StatusKilled, 26, TacticalUi.Enemy, TextAnchor.UpperLeft);
                    TacticalUi.Fill(new Rect(panel.x + 18, panel.y + 74, 42, 1), TacticalUi.Enemy);
                    combatUi.Label(new Rect(panel.x + 18, panel.y + 82, panel.width - 32, 22), respawn, 15, TacticalUi.Text, TextAnchor.UpperLeft);
                    return;
                }
                // Read the current life again for definite assignment; never retain an authority-owned object.
                if (!session.TryGetActorState(session.HumanEntityId, out var human)) return;
                if (human.Health < lastSeenHealth) damagePulseUntil = Time.unscaledTime + CombatHudVisual.DamagePulseSeconds;
                lastSeenHealth = human.Health;
                // ADR-103: damage-scaled edge flash on every hit, under the direction arcs and health vignette.
                float flash = FirstPersonHitFeedback.DamageFlashStrength(damageFlashUntil - Time.unscaledTime, reducedFlash, lastDamageAmount);
                if (flash > 0.01f) TacticalUi.EdgeVignette(combatUi.Width, combatUi.Height, flash * 1.6f, TacticalUi.Enemy);
                DrawHealthVignette(human.Health);
                DrawDamageIndicators(human);
                DrawTacticalMap(a, human, snap);
                DrawAmmoPanel(a, human, snap);
                DrawPickupPrompt();
                DrawAmmoScorePopup(a);
                DrawKillFeed(a);
                DrawScoreFeed(a);
                DrawScoreRibbon(a);
                float cx = combatUi.Width * .5f, cy = combatUi.Height * .5f;
                float adsPresent = actors != null ? actors.LocalAdsPresentationBlend : (input.AimHeld ? 1f : 0f);
                byte opticSlot = actors != null ? actors.LocalVisibleWeaponSlot : human.ActiveWeaponSlot;
                DefinitionId opticId = actors != null ? actors.LocalVisibleWeaponId : human.ActiveWeaponDefinition;
                var opticProfile = actors != null ? actors.LocalVisibleProfile : WeaponVisualProfiles.Resolve(opticId, opticSlot);
                if (AdsOpticVisual.UsesScopedOptic(opticProfile))
                    TacticalUi.AdsScope(combatUi.Width, combatUi.Height, AdsOpticVisual.ScopeMaskOpacity(adsPresent));
                else if (AdsOpticVisual.UsesIronSightOptic(opticProfile))
                    TacticalUi.AdsIronVignette(combatUi.Width, combatUi.Height, AdsOpticVisual.IronVignetteOpacity(adsPresent));
                else if (AdsOpticVisual.UsesCollimatorOptic(opticProfile))
                    TacticalUi.CollimatorDot(cx, cy, AdsOpticVisual.CollimatorDotRadiusPixels(combatUi.Height),
                        AdsOpticVisual.CollimatorDotOpacity(opticProfile, adsPresent));
                // Hip reticle yields once the optic/irons take the aim picture.
                if (!AdsOpticVisual.HideHipReticle(adsPresent))
                {
                    // ADR-112: the gap is the real hip-fire cone edge, so a shot that lands off the crosshair is explainable.
                    const float arm = 6f, w = 1.5f;
                    float gap = HipReticleGap(opticId, combatUi.Height);
                    TacticalUi.ReticleArm(new Rect(cx - gap - arm, cy - w * .5f, arm, w));
                    TacticalUi.ReticleArm(new Rect(cx + gap, cy - w * .5f, arm, w));
                    TacticalUi.ReticleArm(new Rect(cx - w * .5f, cy - gap - arm, w, arm));
                    TacticalUi.ReticleArm(new Rect(cx - w * .5f, cy + gap, w, arm));
                }
                float markerAlpha = FirstPersonHitFeedback.HitMarkerAlpha(hitMarkerUntil - Time.unscaledTime);
                if (markerAlpha > 0.02f) TacticalUi.HitMarker(cx, cy, markerAlpha,
                    CombatHudVisual.HitScale(FirstPersonHitFeedback.HitMarkerSeconds - (hitMarkerUntil - Time.unscaledTime),
                        hitMarkerHeadshot, reducedCameraMotion), hitMarkerHeadshot);
            }
            finally { combatUi.End(); }
        }

        /// <summary>
        /// ADR-112 development overlay: outlines every authoritative hit capsule of the last tick in screen space (head
        /// red, body yellow) so hit registration can be checked against the drawn soldiers. Debug builds only.
        /// </summary>
        private void DrawHitboxOverlay()
        {
            if (!hitboxOverlay || !Debug.isDebugBuild || session == null || camera == null || Event.current.type != EventType.Repaint) return;
            if (session.CopyHitVolumes(overlayVolumes) == 0) return;
            var saved = GUI.matrix; var savedColor = GUI.color;
            GUI.matrix = Matrix4x4.identity;
            float focal = Screen.height * .5f / Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad);
            var human = session.HumanEntityId;
            for (int i = 0; i < overlayVolumes.Count; i++)
            {
                var volume = overlayVolumes[i];
                if (volume.EntityId.Equals(human)) continue;
                GUI.color = volume.RegionId == 1 ? UiInk.Of(UiTokens.DebugKill, .9f) : UiInk.Of(UiTokens.DebugWarn, .75f);
                var a = volume.Capsule.BottomSphereCenter; var b = volume.Capsule.TopSphereCenter;
                var sa = camera.WorldToScreenPoint(new Vector3(a.X, a.Y, a.Z));
                var sb = camera.WorldToScreenPoint(new Vector3(b.X, b.Y, b.Z));
                if (sa.z <= .1f || sb.z <= .1f) continue;
                float ra = volume.Capsule.Radius * focal / sa.z, rb = volume.Capsule.Radius * focal / sb.z;
                OverlayCircle(sa, ra);
                if ((sa - sb).sqrMagnitude < 1f) continue;
                OverlayCircle(sb, rb);
                Vector2 axis = new Vector2(sb.x - sa.x, sb.y - sa.y).normalized;
                Vector2 side = new Vector2(-axis.y, axis.x);
                OverlayLine(new Vector2(sa.x, sa.y) + side * ra, new Vector2(sb.x, sb.y) + side * rb);
                OverlayLine(new Vector2(sa.x, sa.y) - side * ra, new Vector2(sb.x, sb.y) - side * rb);
            }
            GUI.matrix = saved; GUI.color = savedColor;
        }

        private static void OverlayCircle(Vector3 screen, float radius)
        {
            int steps = Mathf.Clamp((int)(radius * .8f), 12, 48);
            Vector2 c = new Vector2(screen.x, screen.y);
            for (int i = 0; i < steps; i++)
            {
                float t0 = i * Mathf.PI * 2f / steps, t1 = (i + 1) * Mathf.PI * 2f / steps;
                OverlayLine(c + new Vector2(Mathf.Cos(t0), Mathf.Sin(t0)) * radius, c + new Vector2(Mathf.Cos(t1), Mathf.Sin(t1)) * radius);
            }
        }

        /// <summary>Screen-space segment (Unity screen y-up), drawn as a rotated 1.5 px bar.</summary>
        private static void OverlayLine(Vector2 from, Vector2 to)
        {
            from.y = Screen.height - from.y; to.y = Screen.height - to.y;
            Vector2 d = to - from; float length = d.magnitude;
            if (length < .5f) return;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, from);
            GUI.DrawTexture(new Rect(from.x, from.y - .75f, length, 1.5f), Texture2D.whiteTexture);
            GUI.matrix = matrix;
        }

        /// <summary>ADR-112: logical-pixel radius of the weapon's hip-fire cone at the current vertical field of view.</summary>
        private float HipReticleGap(DefinitionId weapon, float logicalHeight)
        {
            const float minimum = 3.5f;
            float spread = session != null ? session.HumanHipSpreadRadians(weapon) : 0f;
            if (spread <= 0f || camera == null) return minimum;
            float halfFov = camera.fieldOfView * .5f * Mathf.Deg2Rad;
            float gap = Mathf.Tan(spread) / Mathf.Tan(halfFov) * logicalHeight * .5f;
            return Mathf.Clamp(gap, minimum, logicalHeight * .25f);
        }

        /// <summary>ADR-105: "[G] TAKE AK-47" / "[G] TAKE AMMO" below the reticle while a dropped gun is in reach.</summary>
        private void DrawPickupPrompt()
        {
            if (session == null || !session.TryGetHumanPickupPrompt(out var dropped, out bool ammoTopUp)) return;
            if (!input.Bindings.TryGetControl("Pickup", out string control)
                || string.Equals(control, "Unbound", System.StringComparison.OrdinalIgnoreCase)) return;
            var profile = WeaponVisualProfiles.Resolve(dropped.WeaponId, 0);
            string name = string.IsNullOrEmpty(profile.DisplayLabel) ? ShellUiCopy.Rifle : profile.DisplayLabel.ToUpperInvariant();
            string text = ammoTopUp ? ShellUiCopy.PickupAmmo : ShellUiCopy.PickupWeapon(name);
            const float w = 210f, h = 26f;
            float x = combatUi.Width * .5f - w * .5f, y = combatUi.Height * .5f + 64f;
            TacticalUi.CombatPlate(new Rect(x, y, w, h));
            combatUi.Keycap(new Rect(x + 8, y + 6, 26, 14), TacticalUi.ControlLabel(control), TacticalUi.Accent);
            combatUi.Label(new Rect(x + 40, y + 3, w - 48, 20), text, 12, TacticalUi.Text, TextAnchor.MiddleLeft);
        }

        private void DrawAmmoPanel(Rect safe, ActorLifecycleState human, MatchViewSnapshot? snapshot)
        {
            const float w = 152f, h = 62f;
            float x = safe.xMax - w, y = safe.yMax - h;
            TacticalUi.CombatPlate(new Rect(x, y, w, h), accentBottom: true);
            // ADR-085: mesh-traced silhouette of the weapon actually shown in the hands.
            var shownProfile = WeaponVisualProfiles.Resolve(
                actors != null ? actors.LocalVisibleWeaponId : human.ActiveWeaponDefinition,
                actors != null ? actors.LocalVisibleWeaponSlot : human.ActiveWeaponSlot);
            // ADR-102: guns show their own name (AK-47, G17, ...); knife and grenade keep the shell copy.
            string weapon = human.ActiveWeaponSlot switch
            {
                2 => ShellUiCopy.Knife,
                3 => ShellUiCopy.Grenade,
                _ => string.IsNullOrEmpty(shownProfile.DisplayLabel)
                    ? (human.ActiveWeaponSlot == 1 ? ShellUiCopy.Sidearm : ShellUiCopy.Rifle)
                    : shownProfile.DisplayLabel.ToUpperInvariant()
            };
            combatUi.Label(new Rect(x + 8, y + 4, w - 16, 11), weapon, 9, TacticalUi.Muted, TextAnchor.UpperRight);
            TacticalUi.Icon(new Rect(x + 8, y + 3, 64, 13), TacticalUi.WeaponIconName(shownProfile.KillFeedGlyph), TacticalUi.Muted);
            bool knife = human.ActiveWeaponSlot == 2;
            bool grenade = human.ActiveWeaponSlot == 3;
            Color magColor = !knife && human.Magazine == 0 ? TacticalUi.Enemy : TacticalUi.Text;
            // Mag dominates; reserve stays quieter (BFV/Warface hierarchy). Knife shows an em dash; grenade is 1/0.
            string magText = knife ? "--" : (grenade ? human.Magazine.ToString() : human.Magazine.ToString("00"));
            string reserveText = knife ? "--" : (grenade ? human.Reserve.ToString() : human.Reserve.ToString("000"));
            combatUi.Label(new Rect(x + 28, y + 14, 50, 32), magText, 30, magColor, TextAnchor.MiddleRight);
            combatUi.Label(new Rect(x + 80, y + 20, 12, 14), "/", 11, TacticalUi.Muted, TextAnchor.MiddleCenter);
            combatUi.Label(new Rect(x + 92, y + 28, 50, 16), reserveText, 13, TacticalUi.Muted, TextAnchor.MiddleLeft);
            bool reloading = !knife && !grenade && actors != null && actors.Actions.IsReloading(session!.HumanEntityId);
            float trackX = x + 8, trackW = w - 16;
            if (!knife && !grenade)
                TacticalUi.AmmoRounds(new Rect(trackX, y + h - 13, trackW, 8), human.Magazine, magColor);
            if (reloading)
            {
                float phase = Mathf.Clamp01(actors!.Actions.ReloadPhaseAtTick(session.HumanEntityId, snapshot?.CompletedSimulationTick ?? 0));
                combatUi.Label(new Rect(x, y - 14, w, 14), ShellUiCopy.Reloading, 10, TacticalUi.Accent, TextAnchor.MiddleRight);
                Rect ring = new Rect(x + 7, y + 23, 20, 20);
                UiSdf.Arc(ring, 0, Mathf.PI * 2, 2, UiSdf.Style.Solid(TacticalUi.HudEdge));
                if (phase > 0f) UiSdf.Arc(ring, 0, Mathf.PI * 2 * phase, 2, UiSdf.Style.Solid(TacticalUi.Accent));
            }
            else if (!knife && human.Magazine == 0)
            {
                string reload = input.Bindings.TryGetControl("Reload", out string control) ? TacticalUi.ControlLabel(control) : "R";
                string hint = grenade ? ShellUiCopy.Empty : (human.Reserve > 0 ? ShellUiCopy.ReloadHint.Trim() : ShellUiCopy.OutOfAmmo);
                if (!grenade && human.Reserve > 0) combatUi.Keycap(new Rect(x - 24, y - 15, 24, 14), reload, TacticalUi.Accent);
                combatUi.Label(new Rect(x - 20, y - 14, w + 20, 14), hint, 10, TacticalUi.Accent, TextAnchor.MiddleRight);
            }
        }

        private void DrawTacticalMap(Rect safe, ActorLifecycleState human, MatchViewSnapshot? snapshot)
        {
            // ADR-104: circular heading-up minimap with the compass as a ring around it. Drawn in absolute HUD space
            // (no GUI.BeginGroup: IMGUI applies the group offset before GUI.matrix, so rotated glyphs used to land
            // outside the group and vanish). Map content is masked to the disc by the UiSdf circle clip.
            const float radius = CombatHudVisual.MinimapRadius, band = CombatHudVisual.CompassBandWidth;
            var centre = new Vector2(safe.x + radius + band, safe.yMax - radius - band);
            DrawMinimap(centre, human, snapshot);

            // The in-match teammate list was removed: at 15v15 it ran over the HP block. Team status lives on the
            // Tab scoreboard instead.
            float squadX = centre.x + radius + band + 10f;

            bool danger = CombatHudVisual.IsHealthDanger(human.Health);
            Color healthInk = danger ? TacticalUi.Enemy
                : CombatHudVisual.IsHealthWarning(human.Health) ? TacticalUi.Accent
                : TacticalUi.Text;
            float pulse = danger
                ? CombatHudVisual.CriticalBarPulse(Time.unscaledTime, reducedFlash, reducedCameraMotion)
                : 1f;
            float hx = squadX, hy = safe.yMax - 34;
            float barH = CombatHudVisual.HealthBarHeight;
            combatUi.Label(new Rect(hx, hy - 2, 44, 26), human.Health.ToString(), 24, healthInk, TextAnchor.MiddleLeft);
            combatUi.Label(new Rect(hx + 42, hy + 8, 60, 11), "HP", 9, TacticalUi.Muted, TextAnchor.MiddleLeft);
            float barW = 118f, gap = 2f;
            float segW = (barW - gap * (CombatHudVisual.HealthSegments - 1)) / CombatHudVisual.HealthSegments;
            float barY = safe.yMax - barH;
            // Dual-layer track under segmented fill.
            TacticalUi.Fill(new Rect(hx, barY, barW, barH), UiInk.Of(UiTokens.HudBarWell));
            TacticalUi.Fill(new Rect(hx, barY, barW, 1), UiInk.Light(.05f));
            for (int s = 0; s < CombatHudVisual.HealthSegments; s++)
            {
                float sx = hx + s * (segW + gap);
                float fill = CombatHudVisual.SegmentFill(human.Health, s, segW);
                if (fill <= 0f) continue;
                var ink = UiInk.Alpha(healthInk, healthInk.a * pulse);
                TacticalUi.Fill(new Rect(sx, barY, fill, barH), ink);
                if (fill > 1.5f)
                    TacticalUi.Fill(new Rect(sx, barY, Mathf.Min(1.5f, fill), barH), UiInk.Light(.16f * pulse));
            }

            // ADR-110 armor block to the right of HP: same segmented bar, steel-blue, dimmed once broken.
            ushort armor = 0;
            if (session != null) session.TryGetArmor(session.HumanEntityId, out armor);
            {
                float ax = hx + barW + CombatHudVisual.ArmorBlockOffset;
                var armorInk = armor > 0 ? UiInk.Of(UiTokens.Armor) : TacticalUi.Muted;
                combatUi.Label(new Rect(ax, hy - 2, 44, 26), armor.ToString(), 24, armorInk, TextAnchor.MiddleLeft);
                combatUi.Label(new Rect(ax + 42, hy + 8, 60, 11), "AR", 9, TacticalUi.Muted, TextAnchor.MiddleLeft);
                TacticalUi.Fill(new Rect(ax, barY, barW, barH), UiInk.Of(UiTokens.HudBarWell));
                TacticalUi.Fill(new Rect(ax, barY, barW, 1), UiInk.Light(.05f));
                for (int s = 0; s < CombatHudVisual.HealthSegments; s++)
                {
                    float fill = CombatHudVisual.SegmentFill(armor, s, segW);
                    if (fill <= 0f) continue;
                    TacticalUi.Fill(new Rect(ax + s * (segW + gap), barY, fill, barH), armorInk);
                }
            }

            // ADR-060 tightness bar above HP; authority fraction via LocalMovementView (C0 unchanged).
            float tightness01 = 1f;
            if (session != null && session.TryGetMovementView(session.HumanEntityId, out var movement))
                tightness01 = movement.Tightness01;
            float tightH = CombatHudVisual.TightnessBarHeight;
            float tightY = barY - CombatHudVisual.TightnessBarGap - tightH;
            TacticalUi.Fill(new Rect(hx, tightY, barW, tightH), UiInk.Of(UiTokens.HudBarWell));
            float tightFill = CombatHudVisual.TightnessFillWidth(tightness01, barW);
            if (tightFill > 0f)
            {
                var tightInk = tightness01 <= 0.25f ? TacticalUi.Accent : TacticalUi.Ally;
                TacticalUi.Fill(new Rect(hx, tightY, tightFill, tightH), tightInk);
            }
            combatUi.Label(new Rect(hx + barW + 4, tightY - 2, 28, 10), "STA", 8, TacticalUi.Muted, TextAnchor.MiddleLeft);
        }

        private void DrawMinimap(Vector2 centre, ActorLifecycleState human, MatchViewSnapshot? snapshot)
        {
            const float radius = CombatHudVisual.MinimapRadius, band = CombatHudVisual.CompassBandWidth;
            float yaw = human.AimYaw;
            float pxPerMeter = radius / CombatHudVisual.MinimapRangeMeters;
            float outer = radius + band;
            Rect disc = new Rect(centre.x - radius, centre.y - radius, radius * 2f, radius * 2f);
            Rect bezel = new Rect(centre.x - outer, centre.y - outer, outer * 2f, outer * 2f);

            // 1. Bezel plate with a soft shadow, then the map disc (lighter centre, darker edge).
            var bezelStyle = UiSdf.Style.Solid(UiInk.Of(UiTokens.Well, .86f)).WithShadow(.35f, 8f, 2f);
            if (!UiSdf.Circle(bezel, bezelStyle)) TacticalUi.Fill(bezel, UiInk.Of(UiTokens.Well, .7f));
            UiSdf.Circle(disc, UiSdf.Style.Solid(UiInk.Of(UiTokens.Panel, .82f))
                .WithGradient(UiInk.Of(UiTokens.Well, .9f), UiSdf.Gradient.Radial));

            // 2. Map content, masked to the disc.
            UiSdf.BeginCircleClip(centre, radius);
            try
            {
                var rangeRing = UiSdf.Style.Solid(UiInk.Of(UiTokens.HudRangeRing, .09f));
                rangeRing.Stroke = 1f;
                float half = radius * .5f;
                UiSdf.Circle(new Rect(centre.x - half, centre.y - half, half * 2f, half * 2f), rangeRing);
                // Forward view cone (always up on a heading-up map).
                UiSdf.Arc(disc, -35f * Mathf.Deg2Rad, 35f * Mathf.Deg2Rad, radius,
                    UiSdf.Style.Solid(UiInk.Alpha(TacticalUi.Accent, .09f)));
                DrawMinimapStructures(centre, human, pxPerMeter);
            }
            finally { UiSdf.EndCircleClip(); }

            // 3. Teammates (rim-clamped when out of range) and 4. temporary gunfire reveals.
            float rim = radius - CombatHudVisual.MinimapRimInset;
            if (snapshot != null && session != null)
                for (int i = 0; i < snapshot.Participants.Count; i++)
                {
                    var participant = snapshot.Participants[i];
                    if (!participant.Alive || participant.Team != human.Team || participant.IsHuman) continue;
                    if (!session.TryGetActorState(participant.EntityId, out var ally)) continue;
                    CombatHudVisual.MinimapProject(yaw, human.Feet.X, human.Feet.Z, ally.Feet.X, ally.Feet.Z, pxPerMeter, out float rx, out float dy);
                    if (CombatHudVisual.MinimapClampToRim(ref rx, ref dy, rim))
                    {
                        Rect pip = new Rect(centre.x + rx - 3f, centre.y + dy - 3f, 6f, 6f);
                        if (!UiSdf.Circle(pip, UiSdf.Style.Solid(UiInk.Alpha(TacticalUi.Ally, .7f))))
                            TacticalUi.Fill(pip, TacticalUi.Ally);
                        continue;
                    }
                    TacticalUi.MapGlyph(centre.x + rx, centre.y + dy, Generation.Icons.IconLibrary.AllyArrow, TacticalUi.Ally, ally.AimYaw - yaw, 12f);
                }
            float now = Time.unscaledTime;
            for (int i = 0; i < gunfireRevealUntil.Length; i++)
            {
                float alpha = CombatHudVisual.GunfireRevealAlpha(gunfireRevealUntil[i] - now);
                if (alpha <= 0.02f) continue;
                CombatHudVisual.MinimapProject(yaw, human.Feet.X, human.Feet.Z, gunfireRevealX[i], gunfireRevealZ[i], pxPerMeter, out float rx, out float dy);
                bool clamped = CombatHudVisual.MinimapClampToRim(ref rx, ref dy, rim);
                var ink = UiInk.Alpha(TacticalUi.Enemy, clamped ? alpha * .7f : alpha);
                TacticalUi.MapGlyph(centre.x + rx, centre.y + dy, Generation.Icons.IconLibrary.EnemyMarker, ink, 0f, clamped ? 9f : 12f);
            }

            // 5. Self: always centred and pointing up.
            if (!TacticalUi.Icon(new Rect(centre.x - 8f, centre.y - 8f, 16f, 16f), Generation.Icons.IconLibrary.AllyArrow, TacticalUi.Accent, shadow: true))
                TacticalUi.Fill(new Rect(centre.x - 3f, centre.y - 3f, 6f, 6f), TacticalUi.Accent);

            // 6. Compass ring: hairlines, ticks and letters turn with the map so N always marks north.
            var hairline = UiSdf.Style.Solid(TacticalUi.HudEdge);
            hairline.Stroke = 1f;
            UiSdf.Circle(bezel, hairline);
            UiSdf.Circle(new Rect(disc.x - .5f, disc.y - .5f, disc.width + 1f, disc.height + 1f),
                new UiSdf.Style { Fill = UiInk.Light(.14f), Fill2 = UiInk.Light(.14f), Stroke = 1f, Glint = -1f });
            float headingDeg = Mathf.Repeat(yaw * Mathf.Rad2Deg, 360f);
            for (int bearing = 0; bearing < 360; bearing += 15)
            {
                bool cardinal = CombatHudVisual.IsCardinal(bearing);
                if (cardinal) continue; // letters take the cardinal slots
                float angle = CombatHudVisual.CompassRingAngle(bearing, headingDeg);
                float len = bearing % 45 == 0 ? 5f : 3f;
                var dir = new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle));
                var tickInk = bearing % 45 == 0 ? UiInk.Alpha(TacticalUi.Text, .55f)
                    : UiInk.Alpha(TacticalUi.Muted, .4f);
                TacticalUi.Line(centre + dir * (outer - 2f - len), centre + dir * (outer - 2f), tickInk, 1f);
            }
            for (int c = 0; c < 4; c++)
            {
                int bearing = c * 90;
                float angle = CombatHudVisual.CompassRingAngle(bearing, headingDeg);
                var dir = new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle));
                Vector2 p = centre + dir * (radius + band * .5f);
                bool north = bearing == 0;
                combatUi.Label(new Rect(p.x - 9f, p.y - 7.5f, 18f, 15f), CombatHudVisual.CardinalOrBearing(bearing),
                    north ? 12 : 10, north ? TacticalUi.Accent : TacticalUi.Text, TextAnchor.MiddleCenter);
            }
            if (enemySpawn.HasValue)
            {
                var spawn = enemySpawn.Value;
                float objective = CombatHudVisual.DamageIndicatorAngle(yaw, human.Feet.X, human.Feet.Z, spawn.X, spawn.Z);
                var dir = new Vector2(Mathf.Sin(objective), -Mathf.Cos(objective));
                Vector2 p = centre + dir * (outer + 7f);
                TacticalUi.Icon(new Rect(p.x - 5f, p.y - 5f, 10f, 10f), Generation.Icons.IconLibrary.Objective, TacticalUi.Accent, shadow: true);
            }

            // 7. Fixed heading notch and bearing readout above the ring.
            float notchY = centre.y - outer;
            Matrix4x4 prior = GUI.matrix;
            try
            {
                GUI.matrix = prior * Matrix4x4.TRS(new Vector3(centre.x, notchY + 1f, 0f), Quaternion.Euler(0f, 0f, 180f), Vector3.one);
                if (!UiSdf.Shape(new Rect(-5f, -3f, 10f, 7f), UiShapeKind.Triangle, UiSdf.Style.Solid(TacticalUi.Accent)))
                    TacticalUi.Fill(new Rect(-1f, -3f, 2f, 7f), TacticalUi.Accent);
            }
            finally { GUI.matrix = prior; }
            Rect readout = new Rect(centre.x - 18f, notchY - 21f, 36f, 15f);
            TacticalUi.CombatPlate(readout, strong: true);
            combatUi.Label(readout, CombatHudVisual.HeadingDegrees(yaw).ToString("000"), 11, TacticalUi.Text, TextAnchor.MiddleCenter);
        }

        private void DrawMinimapStructures(Vector2 centre, ActorLifecycleState human, float pxPerMeter)
        {
            const float radius = CombatHudVisual.MinimapRadius;
            float yaw = human.AimYaw;
            float turn = -yaw * Mathf.Rad2Deg;
            bool sdf = UiSdf.Available;
            var fill = UiInk.Of(UiTokens.HudFeature, .82f);
            var edge = new UiSdf.Style { Fill = UiInk.Shade(.5f), Fill2 = UiInk.Shade(.5f), Radius = 1f, Stroke = 1f, Glint = -1f };
            Matrix4x4 prior = GUI.matrix;
            try
            {
                for (int i = 0; i < hudBoxes.Count; i++)
                {
                    var box = hudBoxes[i];
                    if (!box.Gameplay || box.Size.Y < .3f) continue;
                    CombatHudVisual.MinimapProject(yaw, human.Feet.X, human.Feet.Z, box.Center.X, box.Center.Z, pxPerMeter, out float rx, out float dy);
                    float hw = box.Size.X * pxPerMeter * .5f, hh = box.Size.Z * pxPerMeter * .5f;
                    float reach = radius + Mathf.Sqrt(hw * hw + hh * hh);
                    if (rx * rx + dy * dy > reach * reach) continue;
                    if (!sdf && !CombatHudVisual.RectInsideCircle(rx, dy, hw, hh, -yaw, radius)) continue;
                    GUI.matrix = prior * Matrix4x4.TRS(new Vector3(centre.x + rx, centre.y + dy, 0f), Quaternion.Euler(0f, 0f, turn), Vector3.one);
                    Rect r = new Rect(-hw, -hh, hw * 2f, hh * 2f);
                    if (UiSdf.Rect(r, UiSdf.Style.Solid(fill, 1f))) UiSdf.Rect(r, edge);
                    else TacticalUi.Fill(r, fill);
                }
            }
            finally { GUI.matrix = prior; }
        }

        private void DrawHealthVignette(int health)
        {
            // Accessibility: reducedFlash softens peak/pulse; muzzle overlay stays fully suppressed separately.
            float strength = CombatHudVisual.HealthVignette(health, damagePulseUntil > Time.unscaledTime, reducedFlash);
            if (strength <= 0.01f) return;
            if (CombatHudVisual.IsHealthWarning(health) || CombatHudVisual.IsHealthDanger(health))
                strength *= CombatHudVisual.VignetteModulation(Time.unscaledTime, reducedFlash, reducedCameraMotion);
            TacticalUi.EdgeVignette(combatUi.Width, combatUi.Height, strength, TacticalUi.Enemy);
        }

        private void ObserveLocalScoreAwards()
        {
            if (session == null) return;
            scoreAwardDrain.Clear();
            session.DrainLocalScoreAwards(scoreAwardDrain);
            float now = Time.unscaledTime;
            for (int i = 0; i < scoreAwardDrain.Count; i++)
            {
                var award = scoreAwardDrain[i];
                if (award.Kind == LocalScoreAwardKind.Headshot)
                {
                    // ADR-073 feed badge; ADR-097 adds the +20 bonus to the feed row and to the kill totals
                    // (the Headshot award always follows its Kill award in the same drain).
                    PushScoreFeed(ScoreBadgeVisual.HeadshotLabel, award.Points, now);
                    if (award.Points > 0)
                    {
                        ammoScorePoints = unchecked(ammoScorePoints + award.Points);
                        scoreRibbonPoints = unchecked(scoreRibbonPoints + award.Points);
                    }
                    continue;
                }
                if (award.Points == 0) continue;
                bool isKill = award.Kind == LocalScoreAwardKind.Kill;
                string label = isKill
                    ? ScoreBadgeVisual.KillLabelFor(award.KillStreak)
                    : ScoreBadgeVisual.AssistLabel;
                PushScoreFeed(label, award.Points, now);
                ammoScorePoints = award.Points;
                ammoScoreUntil = now + ScoreBadgeVisual.AmmoPopupHoldSeconds;
                if (isKill)
                {
                    scoreRibbonLabel = label;
                    scoreRibbonPoints = award.Points;
                    scoreRibbonUntil = now + ScoreBadgeVisual.RibbonHoldDuration(reducedFlash);
                    scoreRibbonStart = now;
                    scoreRibbonStreak = award.KillStreak;
                }
            }
        }

        private void PushScoreFeed(string label, uint points, float now)
        {
            for (int j = scoreFeedLabels.Length - 1; j > 0; j--)
            {
                scoreFeedLabels[j] = scoreFeedLabels[j - 1];
                scoreFeedPoints[j] = scoreFeedPoints[j - 1];
                scoreFeedUntil[j] = scoreFeedUntil[j - 1];
            }
            scoreFeedLabels[0] = label;
            scoreFeedPoints[0] = points;
            scoreFeedUntil[0] = now + ScoreBadgeVisual.FeedHoldSeconds;
        }

        private void DrawScoreRibbon(Rect safe)
        {
            float remaining = scoreRibbonUntil - Time.unscaledTime;
            float alpha = ScoreBadgeVisual.RibbonAlpha(remaining, reducedFlash);
            if (alpha <= 0.02f) return;
            float w = 260f, h = 40f;
            float x = safe.x + (safe.width - w) * 0.5f;
            float y = safe.y + 78f;
            var banner = new Rect(x, y, w, h);
            TacticalUi.ScoreRibbon(banner, alpha, Time.unscaledTime - scoreRibbonStart, scoreRibbonStreak, reducedCameraMotion, reducedFlash);
            Color ink = TacticalUi.Text;
            ink.a = alpha;
            Color pts = TacticalUi.Accent;
            pts.a = alpha;
            combatUi.Label(new Rect(x, y - 22, w, 16), scoreRibbonLabel, 12, ink, TextAnchor.MiddleCenter);
            combatUi.Label(new Rect(x, y + h + 2, w, 20), ScoreBadgeVisual.FormatPoints(scoreRibbonPoints), 18, pts, TextAnchor.MiddleCenter);
        }

        private void DrawScoreFeed(Rect safe)
        {
            float now = Time.unscaledTime;
            const float rowH = 16f;
            float baseY = safe.y + safe.height * 0.52f;
            float w = 200f;
            float x = safe.x + (safe.width - w) * 0.5f;
            for (int i = 0; i < scoreFeedLabels.Length; i++)
            {
                float remaining = scoreFeedUntil[i] - now;
                float alpha = ScoreBadgeVisual.FeedAlpha(remaining);
                if (alpha <= 0.02f) continue;
                string label = scoreFeedLabels[i];
                if (string.IsNullOrEmpty(label)) continue;
                Rect r = new Rect(x, baseY + i * (rowH + 2f), w, rowH);
                Color plate = TacticalUi.HudPlate;
                plate.a *= alpha * 0.75f;
                TacticalUi.Fill(r, plate);
                Color ink = TacticalUi.Text;
                ink.a = alpha;
                Color pts = TacticalUi.Accent;
                pts.a = alpha;
                combatUi.Label(new Rect(r.x + 8, r.y, r.width - 56, r.height), label, 11, ink, TextAnchor.MiddleLeft);
                if (scoreFeedPoints[i] > 0)
                {
                    combatUi.Label(
                        new Rect(r.xMax - 52, r.y, 44, r.height),
                        ScoreBadgeVisual.FormatPoints(scoreFeedPoints[i]),
                        12,
                        pts,
                        TextAnchor.MiddleRight);
                }
            }
        }

        private void DrawAmmoScorePopup(Rect safe)
        {
            float remaining = ammoScoreUntil - Time.unscaledTime;
            float alpha = ScoreBadgeVisual.AmmoPopupAlpha(remaining);
            if (alpha <= 0.02f) return;
            const float w = 152f, h = 62f;
            float x = safe.xMax - w;
            float y = safe.yMax - h - 22f;
            Color ink = TacticalUi.Accent;
            ink.a = alpha;
            combatUi.Label(new Rect(x, y, w, 18), ScoreBadgeVisual.FormatPoints(ammoScorePoints), 16, ink, TextAnchor.MiddleRight);
        }

        private void ObserveEliminations(IReadOnlyList<CombatEvent> events)
        {
            var snapshot = session?.TryAcquireSnapshot();
            if (snapshot == null) return;
            for (int i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                if (ev.Kind != CombatEventKind.Death) continue;
                bool seen = false;
                for (int j = 0; j < recentDeathIds.Length; j++) if (recentDeathIds[j].Equals(ev.Id)) { seen = true; break; }
                if (seen) continue;
                recentDeathIds[deathIdCursor++ % recentDeathIds.Length] = ev.Id;
                byte killerTeam = 0, victimTeam = 0;
                string? killer = null;
                string victim = "Operator";
                for (int j = 0; j < snapshot.Participants.Count; j++)
                {
                    var p = snapshot.Participants[j];
                    if (ev.Instigator.IsValid && p.EntityId.Equals(ev.Instigator)) { killer = p.IsHuman ? KillFeedText.LocalOperator : p.DisplayName; killerTeam = p.Team; }
                    if (p.EntityId.Equals(ev.Target)) { victim = p.IsHuman ? KillFeedText.LocalOperator : p.DisplayName; victimTeam = p.Team; }
                }
                KillFeedCause.Resolve(events, i, out KillFeedWeapon weapon, out bool headshot);
                for (int j = killFeed.Length - 1; j > 0; j--)
                {
                    killFeed[j] = killFeed[j - 1]; killFeedUntil[j] = killFeedUntil[j - 1];
                    killFeedY[j] = killFeedY[j - 1];
                    killFeedKillerTeam[j] = killFeedKillerTeam[j - 1]; killFeedVictimTeam[j] = killFeedVictimTeam[j - 1];
                }
                float bottom=-CombatHudVisual.KillFeedRowHeight-2;
                for(int j=1;j<killFeed.Length;j++)if(killFeedUntil[j]>Time.unscaledTime)bottom=Mathf.Max(bottom,killFeedY[j]);
                killFeedY[0] = bottom+CombatHudVisual.KillFeedRowHeight+2;
                killFeedKillerTeam[0] = killerTeam; killFeedVictimTeam[0] = victimTeam;
                killFeed[0] = KillFeedText.Create(killer, victim, weapon, headshot);
                killFeedUntil[0] = Time.unscaledTime + CombatHudVisual.KillFeedHoldSeconds;
            }
        }

        private void DrawKillFeed(Rect safe)
        {
            float now = Time.unscaledTime;
            float rowH = CombatHudVisual.KillFeedRowHeight;
            const float gap = 2f;
            float rowW = CombatHudVisual.KillFeedRowWidth;
            float targetY = 0f;
            for (int i = killFeed.Length-1; i >= 0; i--)
            {
                float remaining = killFeedUntil[i] - now;
                float alpha = CombatHudVisual.KillFeedAlpha(remaining);
                if (alpha <= 0.02f) continue;
                KillFeedEntry entry = killFeed[i];
                if (string.IsNullOrEmpty(entry.Victim) && !entry.HasKiller) continue;
                float elapsed = CombatHudVisual.KillFeedHoldSeconds - remaining;
                if (Event.current.type == EventType.Repaint)
                    killFeedY[i] = UiMotion.Follow(killFeedY[i], targetY, 20f, Time.unscaledDeltaTime, reducedCameraMotion);
                float offset = UiMotion.SlideOffset(UiMotion.Staggered(elapsed,i,reducedCameraMotion), 1f, reducedCameraMotion);
                Rect r = new Rect(safe.xMax - rowW + offset, safe.y + 62 + killFeedY[i], rowW, rowH);
                targetY += (rowH + gap) * UiMotion.Clamp01(remaining / UiMotion.TransitionSeconds);
                alpha *= UiMotion.EnterAlpha(elapsed);
                Color plate = TacticalUi.HudPlate;
                plate.a *= alpha * 0.8f;
                TacticalUi.Fill(r, plate);
                bool localKiller = KillFeedText.IsLocalKiller(entry);
                bool localVictim = KillFeedText.IsLocalVictim(entry);
                Color rail = localKiller ? TacticalUi.Ally : (localVictim ? TacticalUi.Enemy : TacticalUi.HudEdge);
                rail.a *= alpha;
                TacticalUi.AccentBar(new Rect(r.x, r.y + 2, 2, r.height - 4), rail);

                Color killerPlate = killFeedKillerTeam[i] == 1 ? TacticalUi.Ally : TacticalUi.Enemy;
                Color victimPlate = killFeedVictimTeam[i] == 1 ? TacticalUi.Ally : TacticalUi.Enemy;
                killerPlate.a = killFeedKillerTeam[i] == 0 ? 0f : alpha * .12f;
                victimPlate.a = killFeedVictimTeam[i] == 0 ? 0f : alpha * .12f;
                TacticalUi.Fill(new Rect(r.x + 4, r.y + 1, CombatHudVisual.KillFeedNameWidth + 4, r.height - 2), killerPlate);

                string left = entry.LeftLabel;
                string right = entry.RightLabel;
                bool env = !entry.HasKiller;
                Color leftInk = localKiller ? TacticalUi.Ally : TacticalUi.Text;
                Color rightInk = localVictim ? TacticalUi.Enemy : TacticalUi.Muted;
                leftInk.a = alpha;
                rightInk.a = alpha;
                Color iconInk = TacticalUi.Muted;
                iconInk.a = alpha * 0.92f;

                float nameW = CombatHudVisual.KillFeedNameWidth;
                float iconX = r.x + 8f + nameW + 4f;
                combatUi.Label(new Rect(r.x + 8, r.y, nameW, r.height), left, 11, leftInk, TextAnchor.MiddleRight);

                if (entry.Weapon != KillFeedWeapon.None)
                {
                    float wIcon = CombatHudVisual.KillFeedWeaponIconWidth;
                    TacticalUi.KillFeedWeaponIcon(new Rect(iconX, r.y + 2f, wIcon, r.height - 4f), entry.Weapon, iconInk);
                    iconX += wIcon + CombatHudVisual.KillFeedIconGap;
                    if (entry.Headshot)
                    {
                        float hIcon = CombatHudVisual.KillFeedHeadshotIconWidth;
                        Color hs = localKiller ? TacticalUi.Ally : TacticalUi.Accent;
                        hs.a = alpha;
                        TacticalUi.KillFeedHeadshotIcon(new Rect(iconX, r.y + 2f, hIcon, r.height - 4f), hs);
                        iconX += hIcon + CombatHudVisual.KillFeedIconGap;
                    }
                }
                else if (env)
                {
                    // No weapon for environment deaths; small muted dash keeps spacing readable.
                    Color dash = TacticalUi.Muted;
                    dash.a = alpha * 0.7f;
                    TacticalUi.Fill(new Rect(iconX + 4f, r.y + r.height * 0.45f, 10f, 1.5f), dash);
                    iconX += 18f;
                }

                float rightX = iconX;
                float rightW = r.xMax - 6f - rightX;
                if (rightW < 40f) rightW = 40f;
                TacticalUi.Fill(new Rect(rightX, r.y + 1, rightW, r.height - 2), victimPlate);
                combatUi.Label(new Rect(rightX, r.y, rightW, r.height), right, 11, rightInk, TextAnchor.MiddleLeft);
            }
        }

        private void DrawAccessibilityOverlays()
        {
            if (muzzleFlashRemaining > 0f && !reducedFlash)
            {
                // Soft lower-center wash near the muzzle, not a full-screen bubble flash.
                float alpha = Mathf.Clamp01(muzzleFlashRemaining / 0.09f) * 0.28f;
                float w = Screen.width;
                float h = Screen.height;
                var prev = GUI.color;
                GUI.color = UiInk.Of(UiTokens.Caption, alpha);
                GUI.DrawTexture(new Rect(w * 0.28f, h * 0.42f, w * 0.44f, h * 0.38f), Texture2D.whiteTexture);
                GUI.color = UiInk.Of(UiTokens.CaptionGlow, alpha * 0.55f);
                GUI.DrawTexture(new Rect(w * 0.38f, h * 0.52f, w * 0.24f, h * 0.22f), Texture2D.whiteTexture);
                GUI.color = prev;
            }
            if (subtitlesEnabled && subtitleRemaining > 0f && !string.IsNullOrEmpty(subtitleText))
            {
                // ADR-123 caption: brand face on a HUD plate, in the same scaled logical space as the rest of the HUD.
                float scale = Mathf.Max(.5f, hud.Settings.UiScale);
                var priorMatrix = GUI.matrix;
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
                float lw = Screen.width / scale, lh = Screen.height / scale;
                float tw = Mathf.Min(lw * .6f, combatUi.TextWidth(subtitleText, UiType.Lead) + 36f);
                Rect plate = new Rect((lw - tw) * .5f, lh - 178f, tw, 30f);
                TacticalUi.CombatPlate(plate, strong: true, accentLeft: true);
                combatUi.LabelFit(plate, subtitleText, UiType.Lead, UiInk.Of(UiTokens.Caption), TextAnchor.MiddleCenter, UiType.Small);
                GUI.matrix = priorMatrix;
            }
        }

        private static GUIStyle GuiStyle()
        {
            if (cached != null) return cached;
            // Diagnostic (non-shell) HUD still uses the brand face and text token.
            cached = new GUIStyle(GUI.skin.label)
            {
                fontSize = UiType.Lead,
                normal = { textColor = TacticalUi.StyleInk(TacticalUi.Text) }
            };
            var face = UiFonts.ForLabelSize(UiType.Lead);
            if (face != null) cached.font = face;
            return cached;
        }
    }
}
