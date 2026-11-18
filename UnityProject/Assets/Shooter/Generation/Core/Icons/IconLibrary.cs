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
        /// <summary>ADR-108 career insignia prefix: <c>rank-01</c> (Private) … <c>rank-20</c> (General).</summary>
        public const string CareerRankPrefix = "rank-";
        public const int CareerRankCount = 20;
        public const string EmblemAlly = "emblem-ally";
        public const string EmblemEnemy = "emblem-enemy";
        public const string MedalStar = "medal-star";
        public const string Crown = "crown";
        public const string Audio = "settings-audio";
        public const string Video = "settings-video";
        public const string Controls = "settings-controls";
        public const string Accessibility = "settings-accessibility";

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

        /// <summary>ADR-108: insignia name for career rank <paramref name="rank"/> (1..20).</summary>
        public static string CareerRank(int rank) =>
            CareerRankPrefix + Math.Max(1, Math.Min(CareerRankCount, rank)).ToString("00", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// ADR-108 career insignia, one unique silhouette per rank: enlisted chevrons and rockers (01–11), a segmented
        /// warrant bar (12), lieutenant and captain bars (13–15), field-grade leaves and the eagle (16–18), general stars (19–20).
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
            yield return new IconRecipe(CareerRank(12))
                .Add(IconShape.Box(170, 390, 660, 220, 40))
                .Cut(IconShape.Box(345, 390, 60, 220)).Cut(IconShape.Box(595, 390, 60, 220));
            yield return new IconRecipe(CareerRank(13)).Add(IconShape.Box(400, 130, 200, 740, 40));
            yield return new IconRecipe(CareerRank(14))
                .Add(IconShape.Box(380, 130, 240, 740, 40)).Cut(IconShape.Box(470, 210, 60, 580, 20));
            yield return new IconRecipe(CareerRank(15))
                .Add(IconShape.Box(250, 130, 180, 740, 40)).Add(IconShape.Box(570, 130, 180, 740, 40));
            yield return new IconRecipe(CareerRank(16)).Add(IconShape.Polygon(Leaf));
            yield return new IconRecipe(CareerRank(17))
                .Add(IconShape.Polygon(Leaf))
                .Cut(IconShape.Capsule(500, 230, 500, 760, 50))
                .Cut(IconShape.Capsule(500, 420, 330, 330, 40)).Cut(IconShape.Capsule(500, 420, 670, 330, 40))
                .Cut(IconShape.Capsule(500, 580, 320, 520, 40)).Cut(IconShape.Capsule(500, 580, 680, 520, 40));
            yield return new IconRecipe(CareerRank(18)).Add(IconShape.Polygon(Eagle));
            yield return new IconRecipe(CareerRank(19)).Add(IconShape.Polygon(Star));
            yield return new IconRecipe(CareerRank(20))
                .Add(IconShape.Ring(500, 430, 430, 75)).Cut(IconShape.Box(0, 0, 1000, 560))
                .Add(SmallStar(155, 400, 3, 10)).Add(SmallStar(385, 400, 3, 10))
                .Add(SmallStar(615, 400, 3, 10)).Add(SmallStar(845, 400, 3, 10));
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
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in list)
                if (!names.Add(r.Name)) throw new InvalidOperationException("Duplicate icon name: " + r.Name);
            return list;
        }
    }
}
