#nullable enable
using System;
using System.Collections.Generic;

namespace Shooter.Game.Settings
{
    /// <summary>What a rank unlocks (ADR-108).</summary>
    public enum CareerRewardKind : byte
    {
        Starter = 0,
        Sight = 1,
        Weapon = 2,
        Buff = 3,
        Final = 4
    }

    /// <summary>Rank colour band for the icon tint (enlisted olive, warrant/officer silver or bronze, general gold).</summary>
    public enum CareerRankBand : byte
    {
        Enlisted = 0,
        Warrant = 1,
        Bronze = 2,
        Officer = 3,
        General = 4
    }

    /// <summary>One step of the ADR-108 career ladder.</summary>
    public sealed class RankDefinition
    {
        public int Index { get; }
        public string Name { get; }
        public string Code { get; }
        public CareerRankBand Band { get; }
        public long Threshold { get; }
        public CareerRewardKind RewardKind { get; }
        /// <summary>Weapon, sight or buff id; empty for the starter kit and the final rank.</summary>
        public string RewardId { get; }
        /// <summary>Short reward text for the ladder ("AK-102", "Fast Hands I · reload -8%").</summary>
        public string RewardLabel { get; }

        /// <summary>Icon glyph name in the UI atlas (<c>rank-01</c> … <c>rank-30</c>).</summary>
        public string IconName => "rank-" + Index.ToString("00", System.Globalization.CultureInfo.InvariantCulture);

        internal RankDefinition(int index, string name, string code, CareerRankBand band, CareerRewardKind kind, string rewardId, string rewardLabel)
        {
            Index = index;
            Name = name;
            Code = code;
            Band = band;
            Threshold = CareerLadder.ThresholdFor(index);
            RewardKind = kind;
            RewardId = rewardId;
            RewardLabel = rewardLabel;
        }
    }

    /// <summary>
    /// ADR-108 combined career buffs for a rank, in integer per-mille (1000 = unchanged). The shell maps this onto the
    /// simulation's <c>WeaponModifiers</c>; this assembly stays free of Simulation references. ADR-119 adds hip spread,
    /// shotgun pellet spread, the bolt/pump cycle and move speed (above 1000 = faster).
    /// </summary>
    public readonly struct CareerBuffs : IEquatable<CareerBuffs>
    {
        public ushort RecoilPerMille { get; }
        public ushort ReloadPerMille { get; }
        public ushort AdsSpreadPerMille { get; }
        public ushort EquipPerMille { get; }
        public byte ExtraMagazines { get; }
        public ushort HipSpreadPerMille { get; }
        public ushort PelletSpreadPerMille { get; }
        public ushort ManualCyclePerMille { get; }
        public ushort MoveSpeedPerMille { get; }

        public CareerBuffs(ushort recoil, ushort reload, ushort adsSpread, ushort equip, byte extraMagazines,
            ushort hipSpread = 1000, ushort pelletSpread = 1000, ushort manualCycle = 1000, ushort moveSpeed = 1000)
        {
            RecoilPerMille = recoil;
            ReloadPerMille = reload;
            AdsSpreadPerMille = adsSpread;
            EquipPerMille = equip;
            ExtraMagazines = extraMagazines;
            HipSpreadPerMille = hipSpread;
            PelletSpreadPerMille = pelletSpread;
            ManualCyclePerMille = manualCycle;
            MoveSpeedPerMille = moveSpeed;
        }

        public static CareerBuffs None => new CareerBuffs(1000, 1000, 1000, 1000, 0);

        public bool IsNone => Equals(None);

        public bool Equals(CareerBuffs other) =>
            RecoilPerMille == other.RecoilPerMille && ReloadPerMille == other.ReloadPerMille && AdsSpreadPerMille == other.AdsSpreadPerMille
            && EquipPerMille == other.EquipPerMille && ExtraMagazines == other.ExtraMagazines
            && HipSpreadPerMille == other.HipSpreadPerMille && PelletSpreadPerMille == other.PelletSpreadPerMille
            && ManualCyclePerMille == other.ManualCyclePerMille && MoveSpeedPerMille == other.MoveSpeedPerMille;

        public override bool Equals(object? obj) => obj is CareerBuffs other && Equals(other);

        public override int GetHashCode() =>
            ((((((RecoilPerMille * 31 + ReloadPerMille) * 31 + AdsSpreadPerMille) * 31 + EquipPerMille) * 31 + HipSpreadPerMille) * 31
                + PelletSpreadPerMille) * 31 + ManualCyclePerMille) * 31 + MoveSpeedPerMille + ExtraMagazines;

        public override string ToString() =>
            "recoil=" + RecoilPerMille + " reload=" + ReloadPerMille + " ads=" + AdsSpreadPerMille + " equip=" + EquipPerMille
            + " mags=+" + ExtraMagazines + " hip=" + HipSpreadPerMille + " pellets=" + PelletSpreadPerMille
            + " cycle=" + ManualCyclePerMille + " move=" + MoveSpeedPerMille;
    }

    /// <summary>
    /// ADR-108 career ladder, extended by ADR-119 to the 30 US Army grades from Private to General of the Armies.
    /// Reaching rank <c>n</c> takes <c>1000·(n²-1)</c> career points (CP); every rank above the first unlocks one sight,
    /// gun or small global gun buff. Pure data, no I/O. <see cref="RankCount"/> is the one source of truth for the
    /// ladder length; its mirrors in assemblies that cannot reference this one are pinned by a test.
    /// </summary>
    public static class CareerLadder
    {
        public const int RankCount = 30;
        public const string StarterPrimary = "weapons/m16";
        public const string StarterSidearm = "weapons/g17";
        public const string StarterSight = "sights/irons";

        public const string BuffFastHands1 = "buffs/fast-hands-1";
        public const string BuffFastHands2 = "buffs/fast-hands-2";
        public const string BuffSteadyAim1 = "buffs/steady-aim-1";
        public const string BuffSteadyAim2 = "buffs/steady-aim-2";
        public const string BuffExtraMag1 = "buffs/extra-mag-1";
        public const string BuffExtraMag2 = "buffs/extra-mag-2";
        public const string BuffRecoil1 = "buffs/recoil-control-1";
        public const string BuffRecoil2 = "buffs/recoil-control-2";
        public const string BuffQuickDraw = "buffs/quick-draw";
        // ADR-119 perks.
        public const string BuffHipShooter1 = "buffs/hip-shooter-1";
        public const string BuffHipShooter2 = "buffs/hip-shooter-2";
        public const string BuffChoke = "buffs/choke";
        public const string BuffSlickAction1 = "buffs/slick-action-1";
        public const string BuffSlickAction2 = "buffs/slick-action-2";
        public const string BuffLightLoad = "buffs/light-load";

        private static readonly RankDefinition[] Table =
        {
            new RankDefinition(1, "Private", "PVT", CareerRankBand.Enlisted, CareerRewardKind.Starter, "", "M16A4 (iron sights) · G17 · knife · grenade"),
            new RankDefinition(2, "Private 2nd Class", "PV2", CareerRankBand.Enlisted, CareerRewardKind.Sight, "sights/collimator", "Collimator sight"),
            new RankDefinition(3, "Private First Class", "PFC", CareerRankBand.Enlisted, CareerRewardKind.Buff, BuffFastHands1, "Fast Hands I · reload -8%"),
            new RankDefinition(4, "Specialist", "SPC", CareerRankBand.Enlisted, CareerRewardKind.Weapon, "weapons/m4a1", "M4A1 carbine"),
            new RankDefinition(5, "Corporal", "CPL", CareerRankBand.Enlisted, CareerRewardKind.Weapon, "weapons/ak102", "AK-102"),
            new RankDefinition(6, "Sergeant", "SGT", CareerRankBand.Enlisted, CareerRewardKind.Sight, "sights/scope2", "2x prism sight"),
            new RankDefinition(7, "Staff Sergeant", "SSG", CareerRankBand.Enlisted, CareerRewardKind.Buff, BuffHipShooter1, "Hip Shooter I · hip spread -8%"),
            new RankDefinition(8, "Sergeant First Class", "SFC", CareerRankBand.Enlisted, CareerRewardKind.Weapon, "weapons/golden-eagle", "Golden Eagle sidearm"),
            new RankDefinition(9, "Master Sergeant", "MSG", CareerRankBand.Enlisted, CareerRewardKind.Buff, BuffSteadyAim1, "Steady Aim I · ADS spread -8%"),
            new RankDefinition(10, "First Sergeant", "1SG", CareerRankBand.Enlisted, CareerRewardKind.Weapon, "weapons/m870", "M870 shotgun"),
            new RankDefinition(11, "Sergeant Major", "SGM", CareerRankBand.Enlisted, CareerRewardKind.Weapon, "weapons/ak47", "AK-47"),
            new RankDefinition(12, "Command Sergeant Major", "CSM", CareerRankBand.Enlisted, CareerRewardKind.Buff, BuffExtraMag1, "Extra Mag I · +1 spare magazine"),
            new RankDefinition(13, "Sergeant Major of the Army", "SMA", CareerRankBand.Enlisted, CareerRewardKind.Sight, "sights/scope4", "4x scope"),
            new RankDefinition(14, "Warrant Officer 1", "WO1", CareerRankBand.Warrant, CareerRewardKind.Weapon, "weapons/mk16", "Mk 16 rifle"),
            new RankDefinition(15, "Chief Warrant Officer 2", "CW2", CareerRankBand.Warrant, CareerRewardKind.Buff, BuffChoke, "Choke · pellet spread -15%"),
            new RankDefinition(16, "Chief Warrant Officer 3", "CW3", CareerRankBand.Warrant, CareerRewardKind.Buff, BuffRecoil1, "Recoil Control I · recoil -8%"),
            new RankDefinition(17, "Chief Warrant Officer 4", "CW4", CareerRankBand.Warrant, CareerRewardKind.Weapon, "weapons/mk14", "Mk 14 DMR"),
            new RankDefinition(18, "Chief Warrant Officer 5", "CW5", CareerRankBand.Warrant, CareerRewardKind.Buff, BuffQuickDraw, "Quick Draw · equip -20%"),
            new RankDefinition(19, "Second Lieutenant", "2LT", CareerRankBand.Bronze, CareerRewardKind.Sight, "sights/scope8", "8x scope"),
            new RankDefinition(20, "First Lieutenant", "1LT", CareerRankBand.Officer, CareerRewardKind.Weapon, "weapons/m24", "M24 SWS sniper"),
            new RankDefinition(21, "Captain", "CPT", CareerRankBand.Officer, CareerRewardKind.Buff, BuffSlickAction1, "Slick Action I · bolt/pump cycle -8%"),
            new RankDefinition(22, "Major", "MAJ", CareerRankBand.Bronze, CareerRewardKind.Buff, BuffFastHands2, "Fast Hands II · reload -15%"),
            new RankDefinition(23, "Lieutenant Colonel", "LTC", CareerRankBand.Officer, CareerRewardKind.Weapon, "weapons/mtac338", "M-TAC 338 sniper"),
            new RankDefinition(24, "Colonel", "COL", CareerRankBand.Officer, CareerRewardKind.Buff, BuffRecoil2, "Recoil Control II · recoil -15%"),
            new RankDefinition(25, "Brigadier General", "BG", CareerRankBand.General, CareerRewardKind.Buff, BuffLightLoad, "Light Load · move speed +3%"),
            new RankDefinition(26, "Major General", "MG", CareerRankBand.General, CareerRewardKind.Buff, BuffExtraMag2, "Extra Mag II · +2 spare magazines"),
            new RankDefinition(27, "Lieutenant General", "LTG", CareerRankBand.General, CareerRewardKind.Buff, BuffHipShooter2, "Hip Shooter II · hip spread -15%"),
            new RankDefinition(28, "General", "GEN", CareerRankBand.General, CareerRewardKind.Buff, BuffSteadyAim2, "Steady Aim II · ADS spread -15%"),
            new RankDefinition(29, "General of the Army", "GA", CareerRankBand.General, CareerRewardKind.Buff, BuffSlickAction2, "Slick Action II · bolt/pump cycle -15%"),
            new RankDefinition(30, "General of the Armies", "GAS", CareerRankBand.General, CareerRewardKind.Final, "", "Gold rank insignia"),
        };
        public static IReadOnlyList<RankDefinition> Ranks => Table;

        /// <summary>Cumulative CP needed for rank <paramref name="index"/> (1-based): <c>1000·(n²-1)</c>.</summary>
        public static long ThresholdFor(int index)
        {
            if (index <= 1) return 0;
            if (index > RankCount) index = RankCount;
            return 1000L * ((long)index * index - 1);
        }

        public static RankDefinition Get(int index) => Table[Math.Max(1, Math.Min(RankCount, index)) - 1];

        /// <summary>Rank held with <paramref name="careerPoints"/> CP (1..<see cref="RankCount"/>).</summary>
        public static int RankFor(long careerPoints)
        {
            int rank = 1;
            for (int i = 1; i < Table.Length; i++)
                if (careerPoints >= Table[i].Threshold) rank = i + 1;
            return rank;
        }

        /// <summary>The next rank, or null at the top of the ladder.</summary>
        public static RankDefinition? Next(int rank) => rank >= RankCount ? null : Table[rank];

        /// <summary>Progress from the current rank threshold to the next, 0..1 (1 at the top rank).</summary>
        public static float Progress01(long careerPoints)
        {
            int rank = RankFor(careerPoints);
            if (rank >= RankCount) return 1f;
            long start = Table[rank - 1].Threshold;
            long end = Table[rank].Threshold;
            double t = (careerPoints - start) / (double)(end - start);
            return (float)(t < 0 ? 0 : t > 1 ? 1 : t);
        }

        /// <summary>Rank that unlocks <paramref name="id"/> (weapon, sight or buff); 1 for the starter kit and ids the ladder does not gate.</summary>
        public static int UnlockRankFor(string id)
        {
            if (string.IsNullOrEmpty(id)) return 1;
            for (int i = 0; i < Table.Length; i++)
                if (Table[i].RewardId == id) return Table[i].Index;
            return 1;
        }

        public static bool IsUnlocked(string id, int rank) => UnlockRankFor(id) <= rank;

        /// <summary>Combined buffs held at <paramref name="rank"/>; each tier replaces its earlier tier.</summary>
        public static CareerBuffs BuffsFor(int rank)
        {
            ushort recoil = 1000, reload = 1000, ads = 1000, equip = 1000, hip = 1000, pellets = 1000, cycle = 1000, move = 1000;
            byte mags = 0;
            for (int i = 0; i < Table.Length && Table[i].Index <= rank; i++)
            {
                switch (Table[i].RewardId)
                {
                    case BuffFastHands1: reload = Math.Min(reload, (ushort)920); break;
                    case BuffFastHands2: reload = Math.Min(reload, (ushort)850); break;
                    case BuffSteadyAim1: ads = Math.Min(ads, (ushort)920); break;
                    case BuffSteadyAim2: ads = Math.Min(ads, (ushort)850); break;
                    case BuffRecoil1: recoil = Math.Min(recoil, (ushort)920); break;
                    case BuffRecoil2: recoil = Math.Min(recoil, (ushort)850); break;
                    case BuffQuickDraw: equip = Math.Min(equip, (ushort)800); break;
                    case BuffExtraMag1: mags = Math.Max(mags, (byte)1); break;
                    case BuffExtraMag2: mags = Math.Max(mags, (byte)2); break;
                    case BuffHipShooter1: hip = Math.Min(hip, (ushort)920); break;
                    case BuffHipShooter2: hip = Math.Min(hip, (ushort)850); break;
                    case BuffChoke: pellets = Math.Min(pellets, (ushort)850); break;
                    case BuffSlickAction1: cycle = Math.Min(cycle, (ushort)920); break;
                    case BuffSlickAction2: cycle = Math.Min(cycle, (ushort)850); break;
                    case BuffLightLoad: move = Math.Max(move, (ushort)1030); break;
                }
            }
            return new CareerBuffs(recoil, reload, ads, equip, mags, hip, pellets, cycle, move);
        }
    }
}
