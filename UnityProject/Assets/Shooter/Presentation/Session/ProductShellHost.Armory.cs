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

        private void DrawArmory()
        {
            if (armory == null) return;
            ui.Backdrop();
            ui.Header(ShellUiCopy.ArmoryEyebrow, ShellUiCopy.ArmoryTitle, "");
            Rect a = ui.Safe;
            DrawRankBadge(new Rect(a.xMax - 320, a.y + 2, 320, 50), "armory.rank", alignRight: true);
            float top = a.y + 74;
            float bottom = a.yMax - 74;
            float slotW = Mathf.Min(220, a.width * .2f);
            float sightW = Mathf.Min(300, a.width * .28f);
            float gap = 16;
            float listX = a.x + slotW + gap;
            float listW = a.width - slotW - sightW - gap * 2;
            float sightX = listX + listW + gap;

            // Slots: primary and sidearm are chosen here; knife and grenade are standard issue.
            ui.Section(new Rect(a.x, top, slotW, 18), ShellUiCopy.ArmoryLoadout);
            if (ArmorySlotButton(new Rect(a.x, top + 24, slotW, 54), "armory.slot.primary", ShellUiCopy.ArmorySlotPrimary, armory.Primary, ArmorySlot.Primary))
            { armory.SelectSlot(ArmorySlot.Primary); }
            if (ArmorySlotButton(new Rect(a.x, top + 84, slotW, 54), "armory.slot.sidearm", ShellUiCopy.ArmorySlotSidearm, armory.Sidearm, ArmorySlot.Sidearm))
            { armory.SelectSlot(ArmorySlot.Sidearm); }
            FixedSlot(new Rect(a.x, top + 144, slotW, 44), ShellUiCopy.ArmorySlotKnife);
            FixedSlot(new Rect(a.x, top + 194, slotW, 44), ShellUiCopy.ArmorySlotGrenade);
            ui.Label(new Rect(a.x, top + 252, slotW, 80), ShellUiCopy.ArmoryHint, 13, TacticalUi.Muted);

            // Weapons offered for the selected slot.
            var weapons = armory.WeaponsFor(armory.Slot);
            ui.Section(new Rect(listX, top, listW, 18), ShellUiCopy.ArmoryWeapons);
            float rowH = Mathf.Min(64, (bottom - top - 24) / Mathf.Max(1, weapons.Count) - 6);
            for (int i = 0; i < weapons.Count; i++)
            {
                var weapon = weapons[i];
                Rect row = new Rect(listX, top + 24 + i * (rowH + 6), listW, rowH);
                bool equipped = armory.SelectedIn(armory.Slot).Equals(weapon);
                if (ui.Button(row, "armory.weapon." + weapon, "", selected: equipped) && !armory.IsLocked(weapon))
                {
                    armory.SelectWeapon(weapon);
                    SaveArmoryIfChanged();
                }
                DrawWeaponRow(row, weapon, equipped);
            }

            // Sight buffs for the equipped primary (sidearms keep their own irons).
            ui.Section(new Rect(sightX, top, sightW, 18), ShellUiCopy.ArmorySights);
            DefinitionId gun = armory.Slot == ArmorySlot.Primary ? armory.Primary : armory.Sidearm;
            var sights = armory.SightsFor(gun);
            if (sights.Count == 0)
                ui.Label(new Rect(sightX, top + 28, sightW, 40), ShellUiCopy.ArmorySidearmSights, 14, TacticalUi.Muted);
            DefinitionId mounted = armory.SightFor(gun);
            for (int i = 0; i < sights.Count; i++)
            {
                var sight = sights[i];
                Rect row = new Rect(sightX, top + 24 + i * 62, sightW, 56);
                bool on = mounted.Equals(sight.Id);
                bool sightLocked = armory.IsLocked(sight.Id);
                if (ui.Button(row, "armory.sight." + sight.Name, "", selected: on) && !sightLocked)
                {
                    armory.SelectSight(sight.Id, gun);
                    SaveArmoryIfChanged();
                }
                ui.Label(new Rect(row.x + 16, row.y + 7, row.width - 24, 20), sight.DisplayLabel, 16,
                    on ? TacticalUi.Accent : sightLocked ? TacticalUi.Muted : TacticalUi.Text);
                ui.Label(new Rect(row.x + 16, row.y + 29, row.width - 24, 20), sightLocked ? LockedText(sight.Id) : sight.Summary, 12, TacticalUi.Muted);
                if (sightLocked) DrawLockTag(new Rect(row.xMax - 84, row.y + 7, 72, 16));
            }

            ui.Panel(new Rect(a.x, a.yMax - 66, a.width, 44), accentTop: true);
            ui.Label(new Rect(a.x + 170, a.yMax - 54, a.width - 190, 24), armory.Summary, 16, TacticalUi.Text);
            if (ui.Button(new Rect(a.x, a.yMax - 66, 150, 44), "armory.back", ShellUiCopy.SetupBack)) CloseArmory();
            DrawFooter(armoryStatus);
        }

        private bool ArmorySlotButton(Rect r, string id, string slot, DefinitionId weapon, ArmorySlot which)
        {
            bool clicked = ui.Button(r, id, "", selected: armory!.Slot == which);
            ui.Label(new Rect(r.x + 16, r.y + 7, r.width - 24, 16), slot, 11, TacticalUi.Accent);
            ui.Label(new Rect(r.x + 16, r.y + 24, r.width - 24, 22), ArmorySelection.LabelFor(weapon), 17, TacticalUi.Text);
            return clicked;
        }

        private void FixedSlot(Rect r, string slot)
        {
            ui.Panel(r);
            ui.Label(new Rect(r.x + 16, r.y + 6, r.width - 24, 16), slot, 11, TacticalUi.Muted);
            ui.Label(new Rect(r.x + 16, r.y + 22, r.width - 24, 18), ShellUiCopy.ArmoryFixed, 12, TacticalUi.Muted);
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
