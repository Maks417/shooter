#nullable enable
using System;
using System.Collections.Generic;

namespace Shooter.Generation.Icons
{
    /// <summary>
    /// ADR-085 original procedural glyph set (numeric recipes, no imported art). Names are stable keys used by the
    /// HUD/shell. Weapon silhouettes are not here: they are traced from the packaged weapon meshes
    /// (<see cref="MeshSilhouette"/>) so the icon always matches the 3D weapon.
    /// </summary>
    public static class IconLibrary
    {
        public const string Headshot = "headshot";
        public const string Elimination = "elimination";
        public const string Assist = "assist";
        public const string Ammo = "ammo";
        public const string Magazine = "magazine";
        public const string Health = "health";
        public const string Reload = "reload";
        public const string Objective = "objective";
        public const string AllyArrow = "ally-arrow";
        public const string EnemyMarker = "enemy-marker";
        public const string RankPrivate = "rank-1";
        public const string RankCorporal = "rank-2";
        public const string RankSergeant = "rank-3";
        public const string RankStar = "rank-star";
        /// <summary>ADR-108 career insignia prefix: <c>rank-01</c> (Private) … <c>rank-30</c> (General of the Armies, ADR-119).</summary>
        public const string CareerRankPrefix = "rank-";
        public const int CareerRankCount = 30;
        public const string EmblemAlly = "emblem-ally";
        public const string EmblemEnemy = "emblem-enemy";
        public const string MedalStar = "medal-star";
        public const string Crown = "crown";
        public const string Audio = "settings-audio";
        public const string Video = "settings-video";
        public const string Controls = "settings-controls";
        public const string Accessibility = "settings-accessibility";

        // ADR-113 career art: emblem plates behind the insignia, reward glyphs and state marks.
        public const string CareerPlateEnlisted = "career-plate-enlisted";
        public const string CareerPlateOfficer = "career-plate-officer";
        public const string CareerPlateGeneral = "career-plate-general";
        public const string CareerWreath = "career-wreath";
        public const string RewardSightIrons = "reward-sight-irons";
        public const string RewardSightCollimator = "reward-sight-collimator";
        public const string RewardSightPrism = "reward-sight-prism";
        public const string RewardSightScope4 = "reward-sight-scope4";
        public const string RewardSightScope8 = "reward-sight-scope8";
        public const string RewardFastHands = "reward-buff-fast-hands";
        public const string RewardSteadyAim = "reward-buff-steady-aim";
        public const string RewardExtraMag = "reward-buff-extra-mag";
        public const string RewardRecoil = "reward-buff-recoil";
        public const string RewardQuickDraw = "reward-buff-quick-draw";
        // ADR-119 perks.
        public const string RewardHipShooter = "reward-buff-hip-shooter";
        public const string RewardChoke = "reward-buff-choke";
        public const string RewardSlickAction = "reward-buff-slick-action";
        public const string RewardLightLoad = "reward-buff-light-load";
        public const string RewardStarter = "reward-starter";
        public const string RewardFinal = "reward-final";
        public const string UiLock = "ui-lock";
        public const string UiCheck = "ui-check";

        // Five-point star, centre (500,520), outer 430 / inner 175 (precomputed integers: no trigonometry at runtime).
        private static readonly int[] Star =
            { 500, 90, 603, 378, 909, 387, 666, 574, 753, 868, 500, 695, 247, 868, 334, 574, 91, 387, 397, 378 };

        private static int[] Scaled(int[] xy, int cx, int cy, int num, int den, int ox = 0, int oy = 0)
        {
            var r = new int[xy.Length];
            for (int i = 0; i < xy.Length; i += 2)
            {
                r[i] = ox + cx + (xy[i] - cx) * num / den;
                r[i + 1] = oy + cy + (xy[i + 1] - cy) * num / den;
            }
            return r;
        }

        private static IconShape Chevron(int y, int thickness) =>
            IconShape.Stroke(thickness, 180, y + 120, 500, y - 60, 820, y + 120);

        // ADR-108 rocker: the downward arc under enlisted chevrons.
        private static IconShape Rocker(int y, int thickness) =>
            IconShape.Stroke(thickness, 180, y, 330, y + 70, 500, y + 95, 670, y + 70, 820, y);

        private static IconShape SmallStar(int cx, int cy, int num, int den) =>
            IconShape.Polygon(Scaled(Star, 500, 520, num, den, cx - 500, cy - 520));

        // Oak leaf (field grade), point up, stem down.
        private static readonly int[] Leaf =
        {
            500, 80, 590, 200, 700, 170, 680, 300, 800, 330, 720, 450, 820, 520, 700, 600, 740, 720, 600, 700, 545, 800,
            545, 920, 455, 920, 455, 800, 400, 700, 260, 720, 300, 600, 180, 520, 280, 450, 200, 330, 320, 300, 300, 170, 410, 200
        };

        // Spread eagle (colonel): head, raised wings, tail.
        private static readonly int[] Eagle =
        {
            500, 170, 570, 230, 550, 320, 650, 300, 770, 190, 910, 170, 870, 290, 930, 330, 850, 420, 890, 470, 760, 530,
            630, 520, 610, 640, 690, 830, 565, 770, 500, 890, 435, 770, 310, 830, 390, 640, 370, 520, 240, 530, 110, 470,
            150, 420, 70, 330, 130, 290, 90, 170, 230, 190, 350, 300, 450, 320, 430, 230
        };

        /// <summary>ADR-108: insignia name for career rank <paramref name="rank"/> (1..<see cref="CareerRankCount"/>).</summary>
        public static string CareerRank(int rank) =>
            CareerRankPrefix + Math.Max(1, Math.Min(CareerRankCount, rank)).ToString("00", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// ADR-108 career insignia, one unique silhouette per rank, re-indexed by ADR-119 to the 30 US Army grades:
        /// enlisted chevrons and rockers (01–11), the command sergeant major's wreathed star and the sergeant major of the
        /// Army's two stars (12–13), warrant bars with one to four squares and the CW5 stripe (14–18), lieutenant and
        /// captain bars (19–21), field-grade leaves and the eagle (22–24), and one to four general stars, five stars in a
        /// ring and the arc of four for General of the Armies (25–30).
        /// </summary>
        private static IEnumerable<IconRecipe> CareerRanks()
        {
            yield return new IconRecipe(CareerRank(1))
                .Add(IconShape.Box(200, 410, 600, 180, 40)).Cut(IconShape.Box(260, 470, 480, 60, 20));
            yield return new IconRecipe(CareerRank(2)).Add(Chevron(470, 150));
            yield return new IconRecipe(CareerRank(3)).Add(Chevron(330, 140)).Add(Rocker(650, 120));
            yield return new IconRecipe(CareerRank(4))
                .Add(IconShape.Polygon(220, 150, 780, 150, 780, 520, 500, 890, 220, 520))
                .Cut(SmallStar(500, 450, 2, 5));
            yield return new IconRecipe(CareerRank(5)).Add(Chevron(330, 140)).Add(Chevron(600, 140));
            yield return new IconRecipe(CareerRank(6)).Add(Chevron(230, 120)).Add(Chevron(460, 120)).Add(Chevron(690, 120));
            yield return new IconRecipe(CareerRank(7))
                .Add(Chevron(170, 110)).Add(Chevron(350, 110)).Add(Chevron(530, 110)).Add(Rocker(790, 110));
            yield return new IconRecipe(CareerRank(8))
                .Add(Chevron(140, 100)).Add(Chevron(290, 100)).Add(Chevron(440, 100)).Add(Rocker(670, 95)).Add(Rocker(820, 95));
            yield return new IconRecipe(CareerRank(9))
                .Add(Chevron(120, 90)).Add(Chevron(250, 90)).Add(Chevron(380, 90))
                .Add(Rocker(590, 85)).Add(Rocker(715, 85)).Add(Rocker(840, 85));
            yield return new IconRecipe(CareerRank(10))
                .Add(Chevron(120, 90)).Add(Chevron(250, 90)).Add(Chevron(380, 90))
                .Add(IconShape.Polygon(500, 520, 575, 600, 500, 680, 425, 600))
                .Add(Rocker(720, 85)).Add(Rocker(845, 85));
            yield return new IconRecipe(CareerRank(11))
                .Add(Chevron(120, 90)).Add(Chevron(250, 90)).Add(Chevron(380, 90))
                .Add(SmallStar(500, 610, 1, 4))
                .Add(Rocker(720, 85)).Add(Rocker(845, 85));
            // Command sergeant major: the star inside a wreath ring.
            yield return new IconRecipe(CareerRank(12))
                .Add(Chevron(120, 90)).Add(Chevron(250, 90)).Add(Chevron(380, 90))
                .Add(IconShape.Ring(500, 615, 92, 26)).Add(SmallStar(500, 615, 1, 6))
                .Add(Rocker(735, 80)).Add(Rocker(855, 80));
            // Sergeant major of the Army: two stars between chevrons and rockers.
            yield return new IconRecipe(CareerRank(13))
                .Add(Chevron(120, 90)).Add(Chevron(250, 90)).Add(Chevron(380, 90))
                .Add(SmallStar(405, 610, 1, 5)).Add(SmallStar(595, 610, 1, 5))
                .Add(Rocker(735, 80)).Add(Rocker(855, 80));
            // Warrant officers: a bar with one to four squares; CW5 has a lengthwise stripe.
            yield return new IconRecipe(CareerRank(14))
                .Add(IconShape.Box(170, 390, 660, 220, 40)).Cut(IconShape.Box(445, 445, 110, 110));
            yield return new IconRecipe(CareerRank(15))
                .Add(IconShape.Box(170, 390, 660, 220, 40)).Cut(IconShape.Box(325, 445, 110, 110)).Cut(IconShape.Box(565, 445, 110, 110));
            yield return new IconRecipe(CareerRank(16))
                .Add(IconShape.Box(170, 390, 660, 220, 40))
                .Cut(IconShape.Box(255, 450, 100, 100)).Cut(IconShape.Box(450, 450, 100, 100)).Cut(IconShape.Box(645, 450, 100, 100));
            yield return new IconRecipe(CareerRank(17))
                .Add(IconShape.Box(170, 390, 660, 220, 40))
                .Cut(IconShape.Box(225, 455, 90, 90)).Cut(IconShape.Box(370, 455, 90, 90))
                .Cut(IconShape.Box(540, 455, 90, 90)).Cut(IconShape.Box(685, 455, 90, 90));
            yield return new IconRecipe(CareerRank(18))
                .Add(IconShape.Box(170, 390, 660, 220, 40)).Cut(IconShape.Box(230, 478, 540, 44, 20));
            yield return new IconRecipe(CareerRank(19)).Add(IconShape.Box(400, 130, 200, 740, 40));
            yield return new IconRecipe(CareerRank(20))
                .Add(IconShape.Box(380, 130, 240, 740, 40)).Cut(IconShape.Box(470, 210, 60, 580, 20));
            yield return new IconRecipe(CareerRank(21))
                .Add(IconShape.Box(250, 130, 180, 740, 40)).Add(IconShape.Box(570, 130, 180, 740, 40));
            yield return new IconRecipe(CareerRank(22)).Add(IconShape.Polygon(Leaf));
            yield return new IconRecipe(CareerRank(23))
                .Add(IconShape.Polygon(Leaf))
                .Cut(IconShape.Capsule(500, 230, 500, 760, 50))
                .Cut(IconShape.Capsule(500, 420, 330, 330, 40)).Cut(IconShape.Capsule(500, 420, 670, 330, 40))
                .Cut(IconShape.Capsule(500, 580, 320, 520, 40)).Cut(IconShape.Capsule(500, 580, 680, 520, 40));
            yield return new IconRecipe(CareerRank(24)).Add(IconShape.Polygon(Eagle));
            yield return new IconRecipe(CareerRank(25)).Add(IconShape.Polygon(Star));
            yield return new IconRecipe(CareerRank(26)).Add(SmallStar(280, 500, 9, 20)).Add(SmallStar(720, 500, 9, 20));
            yield return new IconRecipe(CareerRank(27))
                .Add(SmallStar(185, 500, 3, 10)).Add(SmallStar(500, 500, 3, 10)).Add(SmallStar(815, 500, 3, 10));
            yield return new IconRecipe(CareerRank(28))
                .Add(SmallStar(140, 500, 1, 4)).Add(SmallStar(380, 500, 1, 4)).Add(SmallStar(620, 500, 1, 4)).Add(SmallStar(860, 500, 1, 4));
            // General of the Army: five stars set in a ring.
            yield return new IconRecipe(CareerRank(29))
                .Add(SmallStar(500, 240, 3, 10)).Add(SmallStar(766, 433, 3, 10)).Add(SmallStar(665, 747, 3, 10))
                .Add(SmallStar(335, 747, 3, 10)).Add(SmallStar(234, 433, 3, 10));
            // General of the Armies: four gold stars under a laurel arc.
            yield return new IconRecipe(CareerRank(30))
                .Add(IconShape.Ring(500, 430, 430, 75)).Cut(IconShape.Box(0, 0, 1000, 560))
                .Add(SmallStar(155, 400, 3, 10)).Add(SmallStar(385, 400, 3, 10))
                .Add(SmallStar(615, 400, 3, 10)).Add(SmallStar(845, 400, 3, 10));
        }

        // ADR-113 sixteen-point service burst (general plate), outer 440 / inner 372 around (500,500).
        private static readonly int[] Burst =
        {
            500, 60, 642, 156, 811, 189, 844, 358, 940, 500, 844, 642, 811, 811, 642, 844,
            500, 940, 358, 844, 189, 811, 156, 642, 60, 500, 156, 358, 189, 189, 358, 156
        };

        // ADR-113 left laurel branch: stem along a 320 arc around (500,470) and leaf capsules (x0,y0,x1,y1 quads).
        private static readonly int[] WreathStem =
        {
            417, 779, 365, 760, 316, 732, 274, 696, 238, 654, 210, 605, 191, 553, 181, 498, 181, 442, 191, 387,
            210, 335, 238, 286, 274, 244, 316, 208, 365, 180
        };

        private static readonly int[] WreathLeaves =
        {
            350, 753, 232, 774, 350, 753, 297, 668, 248, 667, 131, 639, 248, 667, 234, 568, 190, 547, 94, 474,
            190, 547, 217, 451, 185, 414, 128, 309, 185, 414, 249, 338, 235, 291, 225, 171, 235, 291, 325, 247,
            321, 205, 357, 90, 321, 205, 421, 198
        };

        private static int[] MirrorX(int[] xy)
        {
            var r = (int[])xy.Clone();
            for (int i = 0; i < r.Length; i += 2) r[i] = 1000 - r[i];
            return r;
        }

        private static IconRecipe AddWreath(IconRecipe recipe)
        {
            foreach (var stem in new[] { WreathStem, MirrorX(WreathStem) })
                recipe.Add(IconShape.Stroke(42, stem));
            foreach (var leaves in new[] { WreathLeaves, MirrorX(WreathLeaves) })
                for (int i = 0; i + 3 < leaves.Length; i += 4)
                    recipe.Add(IconShape.Capsule(leaves[i], leaves[i + 1], leaves[i + 2], leaves[i + 3], 78));
            return recipe;
        }

        /// <summary>
        /// ADR-113 career art: emblem plates layered behind the rank insignia, reward glyphs for sights, buffs, the
        /// starter kit and the final rank, and lock/check state marks. Sights and the starter kit span two cells.
        /// </summary>
        private static IEnumerable<IconRecipe> CareerArt()
        {
            yield return new IconRecipe(CareerPlateEnlisted)
                .Add(IconShape.Polygon(150, 90, 850, 90, 870, 130, 870, 560, 500, 920, 130, 560, 130, 130));
            yield return new IconRecipe(CareerPlateOfficer)
                .Add(IconShape.Polygon(500, 60, 885, 280, 885, 720, 500, 940, 115, 720, 115, 280));
            yield return new IconRecipe(CareerPlateGeneral).Add(IconShape.Polygon(Burst));
            yield return AddWreath(new IconRecipe(CareerWreath));

            // Sights, side view, muzzle to the right, on a rail stub.
            yield return new IconRecipe(RewardSightIrons, 2)
                .Add(IconShape.Box(150, 640, 1700, 110, 20))
                .Add(IconShape.Box(250, 330, 280, 330, 40)).Cut(IconShape.Circle(390, 450, 70))
                .Add(IconShape.Box(1460, 360, 280, 300, 30)).Cut(IconShape.Box(1545, 340, 110, 180, 20))
                .Add(IconShape.Box(1575, 400, 50, 260));
            yield return new IconRecipe(RewardSightCollimator, 2)
                .Add(IconShape.Box(420, 660, 1160, 130, 30))
                .Add(IconShape.Box(500, 230, 1000, 460, 150)).Cut(IconShape.Box(600, 320, 800, 280, 100))
                .Add(IconShape.Circle(1000, 460, 60))
                .Add(IconShape.Box(1330, 150, 130, 110, 25));
            yield return new IconRecipe(RewardSightPrism, 2)
                .Add(IconShape.Box(500, 680, 1000, 120, 20))
                .Add(IconShape.Box(420, 300, 1080, 380, 60))
                .Add(IconShape.Box(260, 350, 200, 280, 50))
                .Add(IconShape.Box(1460, 240, 280, 500, 70))
                .Cut(IconShape.Box(540, 390, 820, 44, 22))
                .Add(IconShape.Box(860, 200, 180, 120, 30));
            yield return new IconRecipe(RewardSightScope4, 2)
                .Add(IconShape.Capsule(330, 470, 1650, 470, 190))
                .Add(IconShape.Box(150, 320, 330, 300, 60))
                .Add(IconShape.Box(1440, 300, 400, 340, 60))
                .Add(IconShape.Box(880, 230, 160, 160, 30))
                .Add(IconShape.Box(600, 540, 140, 260, 20)).Add(IconShape.Box(1260, 540, 140, 260, 20))
                .Add(IconShape.Box(450, 790, 1100, 90, 20));
            yield return new IconRecipe(RewardSightScope8, 2)
                .Add(IconShape.Capsule(260, 500, 1700, 500, 170))
                .Add(IconShape.Box(90, 370, 300, 260, 60))
                .Add(IconShape.Box(1480, 280, 440, 440, 70))
                .Add(IconShape.Box(800, 250, 160, 170, 30)).Add(IconShape.Box(1040, 290, 120, 130, 30))
                .Add(IconShape.Box(560, 570, 130, 230, 20)).Add(IconShape.Box(1280, 570, 130, 230, 20))
                .Add(IconShape.Box(420, 790, 1180, 90, 20));

            // Buffs.
            yield return new IconRecipe(RewardFastHands)
                .Add(IconShape.Polygon(480, 150, 770, 150, 820, 860, 550, 880))
                .Cut(IconShape.Box(550, 270, 220, 34)).Cut(IconShape.Box(560, 410, 230, 34))
                .Add(IconShape.Capsule(140, 330, 380, 330, 70))
                .Add(IconShape.Capsule(90, 500, 400, 500, 70))
                .Add(IconShape.Capsule(160, 670, 420, 670, 70));
            yield return new IconRecipe(RewardSteadyAim)
                .Add(IconShape.Stroke(70, 150, 340, 150, 150, 340, 150)).Add(IconShape.Stroke(70, 660, 150, 850, 150, 850, 340))
                .Add(IconShape.Stroke(70, 150, 660, 150, 850, 340, 850)).Add(IconShape.Stroke(70, 660, 850, 850, 850, 850, 660))
                .Add(IconShape.Capsule(500, 250, 500, 390, 60)).Add(IconShape.Capsule(500, 610, 500, 750, 60))
                .Add(IconShape.Capsule(250, 500, 390, 500, 60)).Add(IconShape.Capsule(610, 500, 750, 500, 60))
                .Add(IconShape.Circle(500, 500, 52));
            yield return new IconRecipe(RewardExtraMag)
                .Add(IconShape.Polygon(150, 280, 390, 280, 420, 890, 200, 900))
                .Add(IconShape.Polygon(440, 330, 660, 330, 690, 890, 480, 900))
                .Cut(IconShape.Box(690, 110, 140, 330, 30)).Cut(IconShape.Box(590, 200, 340, 150, 30))
                .Add(IconShape.Box(720, 140, 80, 270, 20)).Add(IconShape.Box(625, 235, 270, 80, 20));
            yield return new IconRecipe(RewardRecoil)
                .Add(IconShape.Capsule(200, 130, 800, 130, 80))
                .Add(IconShape.Polygon(500, 900, 820, 570, 620, 570, 620, 250, 380, 250, 380, 570, 180, 570));
            yield return new IconRecipe(RewardQuickDraw)
                .Add(IconShape.Polygon(580, 80, 240, 560, 470, 560, 390, 920, 780, 390, 550, 390, 680, 80));
            // ADR-119: Hip Shooter (open crosshair arcs), Choke (shot cone narrowing at the muzzle), Slick Action
            // (bolt handle with a return arrow) and Light Load (double forward chevron).
            yield return new IconRecipe(RewardHipShooter)
                .Add(IconShape.Ring(500, 500, 320, 80))
                .Cut(IconShape.Box(450, 0, 100, 1000)).Cut(IconShape.Box(0, 450, 1000, 100))
                .Add(IconShape.Circle(500, 500, 70));
            yield return new IconRecipe(RewardChoke)
                .Add(IconShape.Capsule(110, 500, 470, 500, 190))
                .Add(IconShape.Stroke(60, 560, 260, 900, 450)).Add(IconShape.Stroke(60, 560, 500, 900, 500))
                .Add(IconShape.Stroke(60, 560, 740, 900, 550));
            yield return new IconRecipe(RewardSlickAction)
                .Add(IconShape.Capsule(160, 560, 600, 560, 150))
                .Add(IconShape.Capsule(600, 560, 760, 760, 70)).Add(IconShape.Circle(790, 800, 95))
                .Add(IconShape.Stroke(60, 760, 300, 400, 300)).Add(IconShape.Polygon(400, 200, 230, 300, 400, 400));
            yield return new IconRecipe(RewardLightLoad)
                .Add(IconShape.Stroke(120, 170, 200, 460, 500, 170, 800)).Add(IconShape.Stroke(120, 520, 200, 810, 500, 520, 800));

            // Starter kit: two dog tags on a chain.
            yield return new IconRecipe(RewardStarter)
                .Add(IconShape.Box(200, 330, 320, 520, 90)).Cut(IconShape.Circle(360, 420, 45))
                .Cut(IconShape.Box(430, 230, 380, 580, 100))
                .Add(IconShape.Box(470, 250, 320, 520, 90)).Cut(IconShape.Circle(630, 340, 45))
                .Add(IconShape.Stroke(46, 360, 420, 300, 230, 470, 110, 640, 160, 630, 340));
            yield return AddWreath(new IconRecipe(RewardFinal))
                .Add(IconShape.Polygon(330, 640, 330, 400, 420, 500, 500, 350, 580, 500, 670, 400, 670, 640))
                .Add(IconShape.Box(330, 670, 340, 60, 20))
                .Add(IconShape.Circle(330, 380, 40)).Add(IconShape.Circle(500, 320, 40)).Add(IconShape.Circle(670, 380, 40));

            // State marks.
            yield return new IconRecipe(UiLock)
                .Add(IconShape.Ring(500, 400, 190, 90)).Cut(IconShape.Box(0, 400, 1000, 600))
                .Add(IconShape.Box(265, 390, 90, 120)).Add(IconShape.Box(645, 390, 90, 120))
                .Add(IconShape.Box(200, 480, 600, 420, 70))
                .Cut(IconShape.Circle(500, 630, 60)).Cut(IconShape.Box(475, 630, 50, 150, 20));
            yield return new IconRecipe(UiCheck).Add(IconShape.Stroke(150, 170, 520, 410, 760, 830, 270));
        }

        /// <summary>All library glyphs in their stable atlas order.</summary>
        public static IReadOnlyList<IconRecipe> All { get; } = Build();

        private static IReadOnlyList<IconRecipe> Build()
        {
            var list = new List<IconRecipe>
            {
                new IconRecipe(Headshot)
                    .Add(IconShape.Ring(500, 500, 300, 90))
                    .Add(IconShape.Capsule(100, 500, 300, 500, 80)).Add(IconShape.Capsule(700, 500, 900, 500, 80))
                    .Add(IconShape.Capsule(500, 100, 500, 300, 80)).Add(IconShape.Capsule(500, 700, 500, 900, 80))
                    .Add(IconShape.Circle(500, 500, 70)),
                new IconRecipe(Elimination)
                    .Add(IconShape.Capsule(200, 200, 800, 800, 150)).Add(IconShape.Capsule(800, 200, 200, 800, 150)),
                new IconRecipe(Assist)
                    .Add(IconShape.Ring(500, 500, 360, 90))
                    .Add(IconShape.Capsule(500, 300, 500, 700, 120)).Add(IconShape.Capsule(300, 500, 700, 500, 120)),
                new IconRecipe(Ammo)
                    .Add(IconShape.Box(390, 330, 220, 470, 20))
                    .Add(IconShape.Polygon(390, 340, 500, 120, 610, 340))
                    .Add(IconShape.Box(360, 800, 280, 90, 20))
                    .Cut(IconShape.Box(370, 760, 260, 26)),
                new IconRecipe(Magazine)
                    .Add(IconShape.Polygon(330, 140, 650, 140, 700, 860, 420, 880))
                    .Cut(IconShape.Box(400, 260, 220, 30)).Cut(IconShape.Box(410, 400, 230, 30)),
                new IconRecipe(Health)
                    .Add(IconShape.Box(380, 140, 240, 720, 50)).Add(IconShape.Box(140, 380, 720, 240, 50)),
                new IconRecipe(Reload)
                    .Add(IconShape.Ring(500, 520, 300, 110))
                    .Cut(IconShape.Polygon(500, 520, 560, 0, 1000, 0, 1000, 330))
                    .Add(IconShape.Polygon(640, 120, 900, 260, 640, 400)),
                new IconRecipe(Objective)
                    .Add(IconShape.Polygon(500, 120, 880, 820, 500, 640, 120, 820)),
                new IconRecipe(AllyArrow)
                    .Add(IconShape.Polygon(500, 80, 860, 900, 500, 700, 140, 900)),
                new IconRecipe(EnemyMarker)
                    .Add(IconShape.Polygon(500, 90, 910, 500, 500, 910, 90, 500))
                    .Cut(IconShape.Polygon(500, 300, 700, 500, 500, 700, 300, 500)),
                new IconRecipe(RankPrivate).Add(Chevron(470, 150)),
                new IconRecipe(RankCorporal).Add(Chevron(330, 140)).Add(Chevron(600, 140)),
                new IconRecipe(RankSergeant).Add(Chevron(230, 120)).Add(Chevron(460, 120)).Add(Chevron(690, 120)),
                new IconRecipe(RankStar).Add(IconShape.Polygon(Star)),
                // Fictional ally faction: shield with a cut chevron and a star.
                new IconRecipe(EmblemAlly)
                    .Add(IconShape.Polygon(140, 100, 860, 100, 860, 520, 500, 920, 140, 520))
                    .Cut(IconShape.Polygon(220, 180, 780, 180, 780, 500, 500, 820, 220, 500))
                    .Add(IconShape.Stroke(110, 260, 640, 500, 450, 740, 640))
                    .Add(IconShape.Polygon(Scaled(Star, 500, 520, 2, 5, 0, -190))),
                // Fictional opposing faction: hexagon frame, crossed bars and a core diamond.
                new IconRecipe(EmblemEnemy)
                    .Add(IconShape.Polygon(500, 80, 870, 290, 870, 710, 500, 920, 130, 710, 130, 290))
                    .Cut(IconShape.Polygon(500, 170, 790, 335, 790, 665, 500, 830, 210, 665, 210, 335))
                    .Add(IconShape.Capsule(300, 300, 700, 700, 90)).Add(IconShape.Capsule(700, 300, 300, 700, 90))
                    .Add(IconShape.Polygon(500, 390, 610, 500, 500, 610, 390, 500)),
                new IconRecipe(MedalStar)
                    .Add(IconShape.Ring(500, 500, 400, 80))
                    .Add(IconShape.Polygon(Scaled(Star, 500, 520, 7, 10, 0, -14))),
                new IconRecipe(Crown)
                    .Add(IconShape.Polygon(130, 760, 130, 300, 330, 520, 500, 220, 670, 520, 870, 300, 870, 760))
                    .Add(IconShape.Box(130, 800, 740, 100, 30))
                    .Add(IconShape.Circle(130, 270, 70)).Add(IconShape.Circle(500, 190, 70)).Add(IconShape.Circle(870, 270, 70)),
                // Sound waves are right half-rings; the speaker body is added after the half-plane cut.
                new IconRecipe(Audio)
                    .Add(IconShape.Ring(560, 500, 190, 80)).Add(IconShape.Ring(560, 500, 330, 80))
                    .Cut(IconShape.Box(0, 0, 600, 1000))
                    .Add(IconShape.Polygon(120, 380, 300, 380, 520, 170, 520, 830, 300, 620, 120, 620)),
                new IconRecipe(Video)
                    .Add(IconShape.Box(100, 160, 800, 520, 50))
                    .Cut(IconShape.Box(180, 240, 640, 360, 20))
                    .Add(IconShape.Box(450, 680, 100, 110)).Add(IconShape.Box(290, 780, 420, 80, 30)),
                new IconRecipe(Controls)
                    .Add(IconShape.Box(280, 100, 440, 800, 220))
                    .Cut(IconShape.Box(360, 180, 280, 640, 150))
                    .Add(IconShape.Capsule(500, 220, 500, 420, 70))
                    .Add(IconShape.Capsule(330, 470, 670, 470, 50)),
                new IconRecipe(Accessibility)
                    .Add(IconShape.Ring(500, 500, 400, 70))
                    .Add(IconShape.Circle(500, 270, 85))
                    .Add(IconShape.Capsule(250, 420, 750, 420, 80))
                    .Add(IconShape.Capsule(500, 420, 500, 590, 90))
                    .Add(IconShape.Stroke(80, 380, 780, 500, 590, 620, 780)),
            };
            list.AddRange(CareerRanks());
            list.AddRange(CareerArt());
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in list)
                if (!names.Add(r.Name)) throw new InvalidOperationException("Duplicate icon name: " + r.Name);
            return list;
        }
    }
}
