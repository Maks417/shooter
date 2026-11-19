#nullable enable
using System;
using System.Collections.Generic;

namespace Shooter.Game.Settings
{
    /// <summary>
    /// ADR-113 presentation data for one rank's reward: the atlas icon, a short title, an optional tier numeral and a
    /// detail line. Pure data next to the ladder; icon names match <c>Shooter.Generation.Icons.IconLibrary</c> glyphs
    /// or the mesh-traced <c>weapon.*</c> silhouettes (checked by the Generation tests).
    /// </summary>
    public sealed class CareerRewardArt
    {
        public int Rank { get; }
        public CareerRewardKind Kind { get; }
        public string IconName { get; }
        public string Title { get; }
        /// <summary>Roman tier for buffs ("I", "II"); empty otherwise.</summary>
        public string Tier { get; }
        public string Detail { get; }
        /// <summary>True when the icon is a wide (two-cell or weapon) silhouette.</summary>
        public bool Wide { get; }

        internal CareerRewardArt(int rank, CareerRewardKind kind, string icon, string title, string tier, string detail, bool wide)
        {
            Rank = rank;
            Kind = kind;
            IconName = icon;
            Title = title;
            Tier = tier;
            Detail = detail;
            Wide = wide;
        }

        /// <summary>Title with the tier numeral ("Fast Hands II").</summary>
        public string FullTitle => Tier.Length == 0 ? Title : Title + " " + Tier;
    }

    /// <summary>ADR-113 reward art for every rank of <see cref="CareerLadder"/>.</summary>
    public static class CareerRewardCatalog
    {
        private static readonly CareerRewardArt[] Table =
        {
            Art(1, CareerRewardKind.Starter, "reward-starter", "Starter kit", "", "M16A4 · G17 · knife · grenade", false),
            Art(2, CareerRewardKind.Sight, "reward-sight-collimator", "Collimator", "", "Red-dot sight · 1.2x", true),
            Art(3, CareerRewardKind.Buff, "reward-buff-fast-hands", "Fast Hands", "I", "Reload -8%", false),
            Art(4, CareerRewardKind.Weapon, "weapon.m4a1", "M4A1", "", "Carbine", true),
            Art(5, CareerRewardKind.Weapon, "weapon.ak102", "AK-102", "", "Assault rifle", true),
            Art(6, CareerRewardKind.Sight, "reward-sight-prism", "2x Prism", "", "Prism sight · 2x", true),
            Art(7, CareerRewardKind.Buff, "reward-buff-hip-shooter", "Hip Shooter", "I", "Hip spread -8%", false),
            Art(8, CareerRewardKind.Weapon, "weapon.golden-eagle", "Golden Eagle", "", "Sidearm", true),
            Art(9, CareerRewardKind.Buff, "reward-buff-steady-aim", "Steady Aim", "I", "ADS spread -8%", false),
            Art(10, CareerRewardKind.Weapon, "weapon.m870", "M870", "", "Pump shotgun", true),
            Art(11, CareerRewardKind.Weapon, "weapon.ak47", "AK-47", "", "Assault rifle", true),
            Art(12, CareerRewardKind.Buff, "reward-buff-extra-mag", "Extra Mag", "I", "+1 spare magazine", false),
            Art(13, CareerRewardKind.Sight, "reward-sight-scope4", "4x Scope", "", "Marksman scope · 4x", true),
            Art(14, CareerRewardKind.Weapon, "weapon.mk16", "Mk 16", "", "Assault rifle", true),
            Art(15, CareerRewardKind.Buff, "reward-buff-choke", "Choke", "", "Pellet spread -15%", false),
            Art(16, CareerRewardKind.Buff, "reward-buff-recoil", "Recoil Control", "I", "Recoil -8%", false),
            Art(17, CareerRewardKind.Weapon, "weapon.mk14", "Mk 14 DMR", "", "Marksman rifle", true),
            Art(18, CareerRewardKind.Buff, "reward-buff-quick-draw", "Quick Draw", "", "Equip -20%", false),
            Art(19, CareerRewardKind.Sight, "reward-sight-scope8", "8x Scope", "", "Sniper scope · 8x", true),
            Art(20, CareerRewardKind.Weapon, "weapon.m24", "M24 SWS", "", "Sniper rifle", true),
            Art(21, CareerRewardKind.Buff, "reward-buff-slick-action", "Slick Action", "I", "Bolt/pump cycle -8%", false),
            Art(22, CareerRewardKind.Buff, "reward-buff-fast-hands", "Fast Hands", "II", "Reload -15%", false),
            Art(23, CareerRewardKind.Weapon, "weapon.mtac338", "M-TAC 338", "", "Sniper rifle", true),
            Art(24, CareerRewardKind.Buff, "reward-buff-recoil", "Recoil Control", "II", "Recoil -15%", false),
            Art(25, CareerRewardKind.Buff, "reward-buff-light-load", "Light Load", "", "Move speed +3%", false),
            Art(26, CareerRewardKind.Buff, "reward-buff-extra-mag", "Extra Mag", "II", "+2 spare magazines", false),
            Art(27, CareerRewardKind.Buff, "reward-buff-hip-shooter", "Hip Shooter", "II", "Hip spread -15%", false),
            Art(28, CareerRewardKind.Buff, "reward-buff-steady-aim", "Steady Aim", "II", "ADS spread -15%", false),
            Art(29, CareerRewardKind.Buff, "reward-buff-slick-action", "Slick Action", "II", "Bolt/pump cycle -15%", false),
            Art(30, CareerRewardKind.Final, "reward-final", "Gold insignia", "", "Final rank", false),
        };

        private static CareerRewardArt Art(int rank, CareerRewardKind kind, string icon, string title, string tier, string detail, bool wide) =>
            new CareerRewardArt(rank, kind, icon, title, tier, detail, wide);

        public static IReadOnlyList<CareerRewardArt> All => Table;

        public static CareerRewardArt For(int rank) => Table[Math.Max(1, Math.Min(CareerLadder.RankCount, rank)) - 1];

        public static CareerRewardArt For(RankDefinition rank) => For(rank.Index);

        /// <summary>Atlas icon for a buff id (Active buffs chips); empty for unknown ids.</summary>
        public static string BuffIcon(string buffId)
        {
            foreach (var step in CareerLadder.Ranks)
                if (step.RewardId == buffId) return For(step.Index).IconName;
            return "";
        }
    }
}
