#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Shooter.Game.Contracts;
using Shooter.Game.Session;
using Shooter.Game.Settings;
using Shooter.Presentation.Input;
using Shooter.Presentation.Audio;
using Shooter.Presentation.Menus;
using Shooter.Presentation.Scoreboard;
using Shooter.Generation.Icons;
using Shooter.Contracts;
using Shooter.Presentation.View;
using Shooter.Presentation.Hud;
using Shooter.Presentation.Style;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Presentation.Session
{
    /// <summary>Armory screen: loadout persistence, application to the session and the weapon list.</summary>
    public sealed partial class ProductShellHost : MonoBehaviour
    {
        private void LoadArmory(string path)
        {
            armoryStore = new FileArmoryStore(path);
            var result = armoryStore.Load(out var profile);
            armory = new ArmorySelection(backend!.Armory.Primaries, backend.Armory.Sidearms, profile.Primary, profile.Sidearm, profile.Sights,
                CareerLockRule());
            armorySavedRevision = armory.Revision;
            armoryStatus = result.Succeeded ? "" : result.UserFacing;
            ApplyArmoryToSession();
            Debug.Log("SHOOTER_ARMORY loadout=" + armory.Primary + "+" + armory.PrimarySight + "/" + armory.Sidearm + " file=" + path);
        }

        private void ApplyArmoryToSession()
        {
            if (armory == null || session == null) return;
            // ADR-108: the career rank carries its buffs into every human life and sets the scoreboard insignia.
            var buffs = CareerLadder.BuffsFor(CareerRank);
            session.CareerRank = CareerRank;
            session.UseArmoryLoadout(armory.Primary, armory.PrimarySight, armory.Sidearm,
                buffs.RecoilPerMille, buffs.ReloadPerMille, buffs.AdsSpreadPerMille, buffs.EquipPerMille, buffs.ExtraMagazines,
                buffs.HipSpreadPerMille, buffs.PelletSpreadPerMille, buffs.ManualCyclePerMille, buffs.MoveSpeedPerMille);
            if (view != null)
                view.LocalHandling = new WeaponHandlingScale(buffs.RecoilPerMille, buffs.ReloadPerMille, buffs.EquipPerMille, buffs.ManualCyclePerMille);
        }

        private void OpenArmory()
        {
            if (armory == null) return;
            navigation.OpenArmory();
            ApplyCursorForScreen();
        }

        private void CloseArmory()
        {
            SaveArmoryIfChanged();
            navigation.CloseArmory();
        }

        private void SaveArmoryIfChanged()
        {
            if (armory == null || armoryStore == null || armory.Revision == armorySavedRevision) return;
            armorySavedRevision = armory.Revision;
            ApplyArmoryToSession();
            var profile = new ArmoryProfile(armory.Primary.ToString(), armory.Sidearm.ToString(), armory.SightChoices);
            var result = armoryStore.Save(profile);
            armoryStatus = result.Succeeded ? "" : ShellUiCopy.ArmorySaveFailed;
        }

        /// <summary>
        /// ADR-123 Armory: slots, the weapon list for the chosen slot, and a hero panel (large mesh-traced silhouette,
        /// stat bars) with the sight choices under it. Rects come from the pure <see cref="ArmoryLayout"/>.
        /// </summary>
        private void DrawArmory()
        {
            if (armory == null) return;
            ui.Backdrop(dim: .45f);
            ui.Header(ShellUiCopy.ArmoryEyebrow, ShellUiCopy.ArmoryTitle);
            Rect a = ui.Safe;
            var l = ArmoryLayout.Build(new HudRect(a.x, a.y, a.width, a.height));
            DrawRankBadge(new Rect(a.xMax - 300, a.y + 2, 276, 48), "armory.rank", alignRight: true);

            // Slots: primary and sidearm are chosen here; knife and grenade are standard issue.
            Rect slots = R(l.Slots);
            ui.Section(new Rect(slots.x, slots.y, slots.width, 18), ShellUiCopy.ArmoryLoadout);
            if (ArmorySlotButton(new Rect(slots.x, slots.y + 26, slots.width, 54), "armory.slot.primary", ShellUiCopy.ArmorySlotPrimary, armory.Primary, ArmorySlot.Primary))
            { armory.SelectSlot(ArmorySlot.Primary); }
            if (ArmorySlotButton(new Rect(slots.x, slots.y + 86, slots.width, 54), "armory.slot.sidearm", ShellUiCopy.ArmorySlotSidearm, armory.Sidearm, ArmorySlot.Sidearm))
            { armory.SelectSlot(ArmorySlot.Sidearm); }
            FixedSlot(new Rect(slots.x, slots.y + 146, slots.width, 44), ShellUiCopy.ArmorySlotKnife);
            FixedSlot(new Rect(slots.x, slots.y + 196, slots.width, 44), ShellUiCopy.ArmorySlotGrenade);
            if (slots.yMax - (slots.y + 252) > 40)
                ui.Label(new Rect(slots.x, slots.y + 252, slots.width, slots.yMax - slots.y - 252), ShellUiCopy.ArmoryHint, UiType.Caption + 1, TacticalUi.Muted);

            // Weapons offered for the selected slot.
            Rect list = R(l.List);
            var weapons = armory.WeaponsFor(armory.Slot);
            ui.Section(new Rect(list.x, list.y, list.width, 18), ShellUiCopy.ArmoryWeapons);
            float rowH = Mathf.Clamp((list.height - 26) / Mathf.Max(1, weapons.Count) - 6, 36, 64);
            for (int i = 0; i < weapons.Count; i++)
            {
                var weapon = weapons[i];
                Rect row = new Rect(list.x, list.y + 26 + i * (rowH + 6), list.width, rowH);
                if (row.yMax > list.yMax + .5f) break;
                bool equipped = armory.SelectedIn(armory.Slot).Equals(weapon);
                if (ui.Button(row, "armory.weapon." + weapon, "", selected: equipped) && !armory.IsLocked(weapon))
                {
                    armory.SelectWeapon(weapon);
                    SaveArmoryIfChanged();
                }
                DrawWeaponRow(row, weapon, equipped);
            }

            // Hero: the equipped weapon at full size with comparable stat bars.
            DefinitionId gun = armory.Slot == ArmorySlot.Primary ? armory.Primary : armory.Sidearm;
            ui.Section(new Rect(l.Hero.X, l.Hero.Y - 24, l.Hero.Width, 18), ShellUiCopy.ArmoryEquipped);
            DrawArmoryHero(R(l.Hero), gun, weapons);

            // Sight buffs for the equipped primary (sidearms keep their own irons).
            Rect sightsArea = R(l.Sights);
            ui.Section(new Rect(sightsArea.x, sightsArea.y, sightsArea.width, 18), ShellUiCopy.ArmorySights);
            var sights = armory.SightsFor(gun);
            if (sights.Count == 0)
                ui.Label(new Rect(sightsArea.x, sightsArea.y + 28, sightsArea.width, 40), ShellUiCopy.ArmorySidearmSights, UiType.Small, TacticalUi.Muted);
            DefinitionId mounted = armory.SightFor(gun);
            float sightH = Mathf.Clamp((sightsArea.height - 26) / Mathf.Max(1, sights.Count) - 6, 40, 56);
            for (int i = 0; i < sights.Count; i++)
            {
                var sight = sights[i];
                Rect row = new Rect(sightsArea.x, sightsArea.y + 26 + i * (sightH + 6), sightsArea.width, sightH);
                if (row.yMax > sightsArea.yMax + .5f) break;
                bool on = mounted.Equals(sight.Id);
                bool sightLocked = armory.IsLocked(sight.Id);
                if (ui.Button(row, "armory.sight." + sight.Name, "", selected: on) && !sightLocked)
                {
                    armory.SelectSight(sight.Id, gun);
                    SaveArmoryIfChanged();
                }
                ui.LabelFit(new Rect(row.x + 16, row.y + 6, row.width - 108, 20), sight.DisplayLabel, UiType.Body,
                    on ? TacticalUi.Accent : sightLocked ? TacticalUi.Muted : TacticalUi.Text, TextAnchor.UpperLeft, UiType.Caption);
                ui.LabelFit(new Rect(row.x + 16, row.y + row.height - 22, row.width - 24, 16), sightLocked ? LockedText(sight.Id) : sight.Summary, UiType.Caption, TacticalUi.Muted, TextAnchor.UpperLeft, UiType.Micro);
                if (sightLocked) DrawLockTag(new Rect(row.xMax - 84, row.y + 7, 72, 16));
            }

            Rect actions = R(l.Actions);
            if (ui.Button(new Rect(actions.x, actions.y, 160, actions.height), "armory.back", ShellUiCopy.SetupBack)) CloseArmory();
            Rect summary = new Rect(actions.x + 172, actions.y, actions.width - 172, actions.height);
            ui.Frame(summary, FrameKind.Plate, rail: true);
            ui.LabelFit(new Rect(summary.x + 16, summary.y, summary.width - 32, summary.height), armory.Summary, UiType.Body, TacticalUi.Text, TextAnchor.MiddleLeft, UiType.Caption);
            DrawFooter(armoryStatus, back: true);
        }

        /// <summary>Hero panel: large silhouette, name and stat bars scaled against the best weapon offered in the slot.</summary>
        private void DrawArmoryHero(Rect r, DefinitionId gun, System.Collections.Generic.IReadOnlyList<DefinitionId> offered)
        {
            ui.Frame(r, FrameKind.Hero);
            bool hasStats = backend != null && backend.Armory.TryGetStats(gun, out _, out _, out _, out _, out _);
            float statsH = hasStats ? Mathf.Min(78f, r.height * .42f) : 0f;
            Rect art = new Rect(r.x + 18, r.y + 14, r.width - 36, r.height - statsH - 50);
            if (Shooter.Presentation.View.WeaponVisualProfiles.TryGet(gun, out var profile) && art.height > 12)
                TacticalUi.KillFeedWeaponIcon(art, profile.KillFeedGlyph, TacticalUi.Text);
            ui.LabelFit(new Rect(r.x + 18, art.yMax + 4, r.width - 36, 26), ArmorySelection.LabelFor(gun), UiType.Heading, TacticalUi.Text, TextAnchor.UpperLeft, UiType.Body);
            if (!hasStats) return;
            backend!.Armory.TryGetStats(gun, out int damage, out int pellets, out int magazine, out int rpm, out string mode);
            int maxDamage = 1, maxMag = 1, maxRpm = 1;
            for (int i = 0; i < offered.Count; i++)
                if (backend.Armory.TryGetStats(offered[i], out int d, out int p, out int m, out int f, out _))
                {
                    maxDamage = Mathf.Max(maxDamage, d * Mathf.Max(1, p));
                    maxMag = Mathf.Max(maxMag, m);
                    maxRpm = Mathf.Max(maxRpm, f);
                }
            float sy = r.yMax - statsH - 8;
            float rowH = (statsH - 6) / 3f;
            StatBar(new Rect(r.x + 18, sy, r.width - 36, rowH), ShellUiCopy.ArmoryStatDamage, damage * Mathf.Max(1, pellets) / (float)maxDamage,
                pellets > 1 ? pellets + " × " + damage : damage.ToString());
            StatBar(new Rect(r.x + 18, sy + rowH + 3, r.width - 36, rowH), ShellUiCopy.ArmoryStatRate, rpm / (float)maxRpm, rpm + " RPM");
            StatBar(new Rect(r.x + 18, sy + (rowH + 3) * 2, r.width - 36, rowH), ShellUiCopy.ArmoryStatMagazine, magazine / (float)maxMag, magazine + " RND");
            ui.Label(new Rect(r.x + 18, r.y + 12, r.width - 36, 14), mode.ToUpperInvariant(), UiType.Micro + 1, TacticalUi.Accent, TextAnchor.UpperRight);
        }

        private void StatBar(Rect r, string label, float value01, string value)
        {
            float labelW = Mathf.Min(96f, r.width * .3f), valueW = Mathf.Min(80f, r.width * .24f);
            ui.LabelFit(new Rect(r.x, r.y, labelW, r.height), label, UiType.Caption, TacticalUi.Muted, TextAnchor.MiddleLeft, UiType.Micro);
            Rect track = new Rect(r.x + labelW, r.center.y - 3f, r.width - labelW - valueW - 8f, 6f);
            CareerArt.ProgressBar(track, Mathf.Clamp01(value01), TacticalUi.Accent, TacticalUi.AccentDeep, 10, 1f);
            ui.LabelFit(new Rect(track.xMax + 8f, r.y, valueW, r.height), value, UiType.Caption, TacticalUi.Text, TextAnchor.MiddleRight, UiType.Micro);
        }

        private bool ArmorySlotButton(Rect r, string id, string slot, DefinitionId weapon, ArmorySlot which)
        {
            bool clicked = ui.Button(r, id, "", selected: armory!.Slot == which);
            ui.Label(new Rect(r.x + 16, r.y + 7, r.width - 24, 16), slot, UiType.Micro + 1, TacticalUi.Accent);
            ui.LabelFit(new Rect(r.x + 16, r.y + 24, r.width - 24, 22), ArmorySelection.LabelFor(weapon), UiType.Lead - 1, TacticalUi.Text, TextAnchor.UpperLeft, UiType.Caption);
            return clicked;
        }

        private void FixedSlot(Rect r, string slot)
        {
            ui.Frame(r, FrameKind.Plate, alpha: .8f);
            ui.Label(new Rect(r.x + 16, r.y + 6, r.width - 24, 16), slot, UiType.Micro + 1, TacticalUi.Muted);
            ui.Label(new Rect(r.x + 16, r.y + 22, r.width - 24, 18), ShellUiCopy.ArmoryFixed, UiType.Caption, TacticalUi.Muted);
        }

        private void DrawWeaponRow(Rect row, DefinitionId weapon, bool equipped)
        {
            if (armory != null && armory.IsLocked(weapon))
            {
                DrawLockedWeaponRow(row, weapon);
                return;
            }
            float iconW = Mathf.Min(150, row.width * .3f);
            if (WeaponVisualProfiles.TryGet(weapon, out var profile))
                TacticalUi.KillFeedWeaponIcon(new Rect(row.x + 14, row.y + row.height * .2f, iconW, row.height * .6f), profile.KillFeedGlyph,
                    equipped ? TacticalUi.Accent : TacticalUi.Text);
            float tx = row.x + iconW + 28;
            float tw = row.xMax - tx - 12;
            ui.Label(new Rect(tx, row.y + 6, tw, 22), ArmorySelection.LabelFor(weapon), 17, equipped ? TacticalUi.Accent : TacticalUi.Text);
            if (backend != null && backend.Armory.TryGetStats(weapon, out int damage, out int pellets, out int magazine, out int rpm, out string mode))
                ui.Label(new Rect(tx, row.y + 28, tw, 18), mode + "  ·  " + (pellets > 1 ? pellets + " × " + damage : damage.ToString()) + " DMG  ·  "
                    + magazine + " RND  ·  " + rpm + " RPM", 12, TacticalUi.Muted);
            if (equipped)
                ui.Label(new Rect(row.xMax - 110, row.y + 6, 98, 16), ShellUiCopy.ArmoryEquipped, 11, TacticalUi.Accent, TextAnchor.UpperRight);
            if (armory != null && WeaponSightChoices.TryGet(weapon, out _))
            {
                string sight = ArmorySelection.SightLabelFor(armory.SightFor(weapon));
                ui.Label(new Rect(row.xMax - 160, row.y + 28, 148, 16), sight, 11, TacticalUi.Muted, TextAnchor.UpperRight);
            }
        }
    }
}
