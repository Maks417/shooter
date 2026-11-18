#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Presentation.View;

namespace Shooter.Presentation.Menus
{
    /// <summary>Armory slot tabs. Knife and grenade are standard issue and only shown.</summary>
    public enum ArmorySlot : byte
    {
        Primary = 0,
        Sidearm = 1
    }

    /// <summary>
    /// ADR-107 pure Armory model: which primary and sidearm the player takes into every match and the sight chosen for
    /// each primary. The offered guns come from the host (catalog entries that spawn); sights come from
    /// <see cref="WeaponSightChoices"/>. Every change bumps <see cref="Revision"/> so the host saves once per change.
    /// Unknown or stale ids fall back to the first offered gun and the gun's factory sight.
    /// </summary>
    public sealed class ArmorySelection
    {
        private readonly DefinitionId[] primaries;
        private readonly DefinitionId[] sidearms;
        private readonly SortedDictionary<string, string> sights = new SortedDictionary<string, string>(StringComparer.Ordinal);
        private Func<DefinitionId, bool>? locked;

        public ArmorySlot Slot { get; private set; } = ArmorySlot.Primary;
        public DefinitionId Primary { get; private set; }
        public DefinitionId Sidearm { get; private set; }
        public int Revision { get; private set; }

        public IReadOnlyList<DefinitionId> Primaries => primaries;
        public IReadOnlyList<DefinitionId> Sidearms => sidearms;

        /// <summary>Saved sight per primary (weapon id → sight id), only for valid choices.</summary>
        public IReadOnlyDictionary<string, string> SightChoices => sights;

        public ArmorySelection(IReadOnlyList<DefinitionId> primaries, IReadOnlyList<DefinitionId> sidearms,
            string? primary = null, string? sidearm = null, IReadOnlyDictionary<string, string>? savedSights = null,
            Func<DefinitionId, bool>? isLocked = null)
        {
            locked = isLocked;
            if (primaries == null || primaries.Count == 0) throw new ArgumentException("At least one primary required.", nameof(primaries));
            if (sidearms == null || sidearms.Count == 0) throw new ArgumentException("At least one sidearm required.", nameof(sidearms));
            this.primaries = Copy(primaries);
            this.sidearms = Copy(sidearms);
            Primary = Pick(this.primaries, primary);
            Sidearm = Pick(this.sidearms, sidearm);
            if (savedSights != null)
                foreach (var pair in savedSights)
                {
                    if (!TryId(pair.Key, out var weapon) || !TryId(pair.Value, out var sight)) continue;
                    if (Array.IndexOf(this.primaries, weapon) < 0) continue;
                    if (WeaponSightChoices.TryGet(weapon, out var choice) && choice.Allows(sight)) sights[pair.Key] = pair.Value;
                }
        }

        /// <summary>ADR-108: true when the career has not unlocked <paramref name="id"/> (gun or sight) yet.</summary>
        public bool IsLocked(DefinitionId id) => locked != null && id.IsValid && locked(id);

        /// <summary>
        /// ADR-108: replaces the lock rule (after a promotion or on load) and moves any locked choice to the first unlocked
        /// gun; saved sights are kept and only resolved per <see cref="SightFor"/>. Bumps <see cref="Revision"/> on change.
        /// </summary>
        public void SetLocks(Func<DefinitionId, bool>? isLocked)
        {
            locked = isLocked;
            var primary = Pick(primaries, Primary.ToString());
            var sidearm = Pick(sidearms, Sidearm.ToString());
            if (!primary.Equals(Primary) || !sidearm.Equals(Sidearm)) Revision++;
            Primary = primary;
            Sidearm = sidearm;
        }

        public IReadOnlyList<DefinitionId> WeaponsFor(ArmorySlot slot) => slot == ArmorySlot.Sidearm ? sidearms : primaries;

        public DefinitionId SelectedIn(ArmorySlot slot) => slot == ArmorySlot.Sidearm ? Sidearm : Primary;

        public void SelectSlot(ArmorySlot slot) => Slot = slot;

        /// <summary>Equips <paramref name="weapon"/> in the slot that offers it. Returns false for guns the Armory does not offer.</summary>
        public bool SelectWeapon(DefinitionId weapon)
        {
            if (IsLocked(weapon)) return false;
            if (Array.IndexOf(primaries, weapon) >= 0)
            {
                Slot = ArmorySlot.Primary;
                if (!Primary.Equals(weapon)) { Primary = weapon; Revision++; }
                return true;
            }
            if (Array.IndexOf(sidearms, weapon) >= 0)
            {
                Slot = ArmorySlot.Sidearm;
                if (!Sidearm.Equals(weapon)) { Sidearm = weapon; Revision++; }
                return true;
            }
            return false;
        }

        /// <summary>Sights offered on <paramref name="weapon"/>; empty for guns without mounted sights (sidearms).</summary>
        public IReadOnlyList<SightVisualProfile> SightsFor(DefinitionId weapon)
        {
            var list = new List<SightVisualProfile>();
            if (!WeaponSightChoices.TryGet(weapon, out var choice)) return list;
            foreach (var id in choice.Allowed)
                if (WeaponSightChoices.TryGetSight(id, out var profile)) list.Add(profile);
            return list;
        }

        /// <summary>The sight <paramref name="weapon"/> carries: the saved choice, else its factory sight; invalid when it takes none.</summary>
        public DefinitionId SightFor(DefinitionId weapon)
        {
            if (!WeaponSightChoices.TryGet(weapon, out var choice)) return default;
            if (sights.TryGetValue(weapon.ToString(), out var saved) && TryId(saved, out var id) && choice.Allows(id) && !IsLocked(id)) return id;
            if (!IsLocked(choice.Default)) return choice.Default;
            // ADR-108: factory optic still locked; mount the first unlocked allowed sight (irons first).
            foreach (var allowed in choice.Allowed)
                if (!IsLocked(allowed)) return allowed;
            return choice.Default;
        }

        public DefinitionId PrimarySight => SightFor(Primary);

        /// <summary>Mounts <paramref name="sight"/> on <paramref name="weapon"/> (default: the equipped primary).</summary>
        public bool SelectSight(DefinitionId sight, DefinitionId weapon = default)
        {
            if (!weapon.IsValid) weapon = Primary;
            if (!WeaponSightChoices.TryGet(weapon, out var choice) || !choice.Allows(sight) || IsLocked(sight)) return false;
            if (SightFor(weapon).Equals(sight) && sights.ContainsKey(weapon.ToString())) return true;
            sights[weapon.ToString()] = sight.ToString();
            Revision++;
            return true;
        }

        /// <summary>Display label of a gun ("M16A4"); the id text when the gun has no presentation row.</summary>
        public static string LabelFor(DefinitionId weapon) =>
            WeaponVisualProfiles.TryGet(weapon, out var profile) && profile.DisplayLabel.Length > 0 ? profile.DisplayLabel.ToUpperInvariant() : weapon.ToString();

        public static string SightLabelFor(DefinitionId sight) =>
            WeaponSightChoices.TryGetSight(sight, out var profile) ? profile.DisplayLabel : "";

        /// <summary>"M16A4 · 2X PRISM / G17" for the shell.</summary>
        public string Summary => ShellUiCopy.ArmorySummary(LabelFor(Primary), SightLabelFor(PrimarySight), LabelFor(Sidearm));

        private DefinitionId Pick(DefinitionId[] offered, string? wanted)
        {
            if (TryId(wanted, out var id) && Array.IndexOf(offered, id) >= 0 && !IsLocked(id)) return id;
            foreach (var gun in offered)
                if (!IsLocked(gun)) return gun;
            return offered[0];
        }

        private static DefinitionId[] Copy(IReadOnlyList<DefinitionId> source)
        {
            var list = new List<DefinitionId>(source.Count);
            foreach (var id in source)
            {
                if (!id.IsValid) throw new ArgumentException("Offered weapon ids must be valid.");
                if (!list.Contains(id)) list.Add(id);
            }
            return list.ToArray();
        }

        private static bool TryId(string? text, out DefinitionId id)
        {
            id = default;
            if (string.IsNullOrEmpty(text)) return false;
            try { id = new DefinitionId(text!); return true; }
            catch (ArgumentException) { return false; }
        }
    }
}
