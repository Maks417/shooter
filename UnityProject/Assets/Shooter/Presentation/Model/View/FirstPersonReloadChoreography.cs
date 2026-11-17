#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Presentation.View
{
    /// <summary>One frame of first-person reload choreography (ADR-099). All values are cosmetic.</summary>
    public readonly struct ReloadPose
    {
        /// <summary>Camera-local weapon translation (meters) layered on the hip/ADS composition.</summary>
        public Float3 WeaponPosition { get; }
        /// <summary>Weapon Euler offset (degrees: pitch, yaw, roll).</summary>
        public Float3 WeaponRotationDegrees { get; }
        /// <summary>Weapon-local offset of the packaged magazine part (the old magazine until the fresh one seats).</summary>
        public Float3 MagazineOffset { get; }
        public bool MagazineVisible { get; }
        /// <summary>Weapon-local offset of the fresh magazine carried up from the belt.</summary>
        public Float3 FreshMagazineOffset { get; }
        public bool FreshMagazineVisible { get; }
        /// <summary>Support-hand wrist offset from its packaged grip (hand-root local, meters).</summary>
        public Float3 SupportWristOffset { get; }
        /// <summary>0 = fingers wrapped on the grip/magazine, 1 = fully open.</summary>
        public float SupportOpen01 { get; }
        /// <summary>Bolt/slide override (0 closed .. 1 locked back); negative leaves the shot cycle in charge.</summary>
        public float CycleOverride01 { get; }

        public ReloadPose(Float3 weaponPosition, Float3 weaponRotationDegrees, Float3 magazineOffset, bool magazineVisible,
            Float3 freshMagazineOffset, bool freshMagazineVisible, Float3 supportWristOffset, float supportOpen01,
            float cycleOverride01)
        {
            WeaponPosition = weaponPosition;
            WeaponRotationDegrees = weaponRotationDegrees;
            MagazineOffset = magazineOffset;
            MagazineVisible = magazineVisible;
            FreshMagazineOffset = freshMagazineOffset;
            FreshMagazineVisible = freshMagazineVisible;
            SupportWristOffset = supportWristOffset;
            SupportOpen01 = supportOpen01;
            CycleOverride01 = cycleOverride01;
        }

        /// <summary>Rest pose: nothing moves, the packaged magazine is shown, the shot cycle is in charge.</summary>
        public static ReloadPose Rest => new ReloadPose(default, default, default, true, default, false, default, 0f, -1f);
    }

    /// <summary>
    /// Weapon-specific reload choreography as numeric key tables (ADR-099). The phase is the authoritative
    /// ActionPayload window, so every beat scales with the definition file's reload ticks and never reads ammo drops.
    /// Rifle/carbine: tilt, press release, strip and drop the old magazine, fetch a fresh one from the belt, insert,
    /// slap; empty reloads add a bolt-catch press. Pistol: cant, thumb release (magazine falls), fetch, insert, palm
    /// seat; empty reloads add an overhand slide rack.
    /// </summary>
    public static class FirstPersonReloadChoreography
    {
        /// <summary>Weapon-local gravity for a dropped magazine (m/s²).</summary>
        public const float DropGravity = 9.81f;
        /// <summary>A dropped magazine is hidden once it falls this far below the well (out of view).</summary>
        public const float DropHideDepth = 0.42f;

        private sealed class Track
        {
            public MotionCurve Position = null!;
            public MotionCurve Rotation = null!;
            public MotionCurve Hand = null!;
            public MotionCurve Open = null!;
            /// <summary>Hand-relative offset when the support hand sits on the magazine base in the well.</summary>
            public Float3 Well;
            /// <summary>Hand takes the old magazine at this phase (tactical strip); equals Release for a free drop.</summary>
            public float Grip;
            public float Release;
            public float FreshFrom;
            public float Seat;
            /// <summary>Bolt-catch press / slide release; negative when the cycle is not locked back.</summary>
            public float CycleRelease = -1f;
        }

        private static MotionKey K(float t, float x, float y, float z) => new MotionKey(t, x, y, z);
        private static MotionKey O(float t, float open) => new MotionKey(t, open, 0f, 0f);
        private static MotionKey W(float t, Float3 well, float dx, float dy, float dz) =>
            new MotionKey(t, well.X + dx, well.Y + dy, well.Z + dz);

        // Support-hand reach from the fore-end to the magazine base (the pre-ADR-099 reach endpoint).
        private static readonly Float3 RifleWell = new Float3(0.018f, -0.087f, -0.262f);
        private static readonly Float3 PistolWell = new Float3(0.018f, -0.044f, -0.017f);

        private static readonly Track RifleTactical = Rifle(empty: false);
        private static readonly Track RifleEmpty = Rifle(empty: true);
        private static readonly Track PistolTactical = Pistol(empty: false);
        private static readonly Track PistolEmpty = Pistol(empty: true);

        private static Track Rifle(bool empty)
        {
            Float3 w = RifleWell;
            var track = new Track
            {
                Well = w,
                Grip = empty ? 0.15f : 0.17f,
                Release = empty ? 0.15f : 0.29f,
                FreshFrom = 0.45f,
                Seat = 0.66f,
                CycleRelease = empty ? 0.80f : -1f
            };
            track.Position = empty
                ? new MotionCurve(K(0f, 0, 0, 0), K(.10f, -.022f, -.038f, -.02f), K(.30f, -.028f, -.055f, -.025f),
                    K(.50f, -.03f, -.068f, -.02f), K(.62f, -.026f, -.05f, -.02f), K(.67f, -.024f, -.034f, -.022f),
                    K(.73f, -.022f, -.046f, -.02f), K(.80f, -.016f, -.034f, -.016f), K(.83f, -.014f, -.024f, -.024f),
                    K(.90f, -.008f, -.016f, -.01f), K(1f, 0, 0, 0))
                : new MotionCurve(K(0f, 0, 0, 0), K(.10f, -.022f, -.038f, -.02f), K(.30f, -.028f, -.055f, -.025f),
                    K(.50f, -.03f, -.068f, -.02f), K(.62f, -.026f, -.05f, -.02f), K(.67f, -.024f, -.034f, -.022f),
                    K(.73f, -.022f, -.046f, -.02f), K(.86f, -.01f, -.02f, -.01f), K(1f, 0, 0, 0));
            track.Rotation = empty
                ? new MotionCurve(K(0f, 0, 0, 0), K(.10f, 8, 6, -28), K(.30f, 10, 8, -32), K(.50f, 12, 10, -30),
                    K(.62f, 8, 8, -26), K(.67f, 4, 8, -21), K(.73f, 8, 8, -24), K(.80f, 6, 12, -16),
                    K(.83f, 2, 12, -12), K(.90f, 3, 6, -8), K(1f, 0, 0, 0))
                : new MotionCurve(K(0f, 0, 0, 0), K(.10f, 8, 6, -28), K(.30f, 10, 8, -32), K(.50f, 12, 10, -30),
                    K(.62f, 8, 8, -26), K(.67f, 4, 8, -21), K(.73f, 8, 8, -24), K(.86f, 3, 4, -9), K(1f, 0, 0, 0));
            // Bolt catch sits on the left of the receiver, above and ahead of the well.
            Float3 b = new Float3(w.X - .045f, w.Y + .06f, w.Z + .03f);
            track.Hand = empty
                ? new MotionCurve(K(0f, 0, 0, 0), W(.08f, w, 0, .02f, .01f), W(.14f, w, 0, 0, 0),
                    W(.22f, w, -.03f, -.10f, .01f), W(.40f, w, -.06f, -.28f, .05f), W(.47f, w, -.06f, -.28f, .05f),
                    W(.58f, w, 0, -.10f, .02f), W(.63f, w, 0, -.025f, 0), W(.66f, w, 0, 0, 0), W(.69f, w, 0, .016f, 0),
                    K(.76f, b.X, b.Y, b.Z), K(.79f, b.X + .006f, b.Y - .005f, b.Z), K(.83f, b.X, b.Y, b.Z),
                    K(.94f, 0, 0, 0), K(1f, 0, 0, 0))
                : new MotionCurve(K(0f, 0, 0, 0), W(.08f, w, 0, .02f, .01f), W(.14f, w, 0, 0, 0), W(.17f, w, 0, 0, 0),
                    W(.29f, w, .01f, -.12f, -.02f), W(.40f, w, -.06f, -.28f, .05f), W(.47f, w, -.06f, -.28f, .05f),
                    W(.58f, w, 0, -.10f, .02f), W(.63f, w, 0, -.025f, 0), W(.66f, w, 0, 0, 0), W(.69f, w, 0, .016f, 0),
                    K(.80f, w.X * .45f, w.Y * .45f, w.Z * .45f), K(.92f, 0, 0, 0), K(1f, 0, 0, 0));
            track.Open = empty
                ? new MotionCurve(O(0f, 0), O(.06f, .9f), O(.13f, .7f), O(.15f, .2f), O(.30f, .8f), O(.43f, .9f),
                    O(.47f, 0), O(.66f, 0), O(.69f, .8f), O(.74f, .3f), O(.80f, .2f), O(.86f, .7f), O(.96f, .2f), O(1f, 0))
                : new MotionCurve(O(0f, 0), O(.06f, .9f), O(.14f, .8f), O(.17f, 0), O(.27f, 0), O(.31f, .9f),
                    O(.43f, .9f), O(.47f, 0), O(.66f, 0), O(.69f, .8f), O(.80f, .7f), O(.95f, .2f), O(1f, 0));
            return track;
        }

        private static Track Pistol(bool empty)
        {
            Float3 w = PistolWell;
            // Overhand grip on the rear slide serrations for the rack.
            Float3 s = new Float3(.01f, .07f, -.06f);
            var track = new Track
            {
                Well = w,
                Grip = 0.14f,
                Release = 0.14f,
                FreshFrom = 0.30f,
                Seat = 0.58f,
                CycleRelease = empty ? 0.80f : -1f
            };
            track.Position = empty
                ? new MotionCurve(K(0f, 0, 0, 0), K(.10f, -.03f, .02f, -.04f), K(.14f, -.03f, .027f, -.04f),
                    K(.24f, -.03f, .018f, -.04f), K(.50f, -.03f, .014f, -.04f), K(.57f, -.03f, .004f, -.04f),
                    K(.61f, -.03f, .026f, -.04f), K(.68f, -.03f, .016f, -.04f), K(.76f, -.036f, .02f, -.046f),
                    K(.80f, -.036f, .014f, -.03f), K(.84f, -.03f, .016f, -.038f), K(1f, 0, 0, 0))
                : new MotionCurve(K(0f, 0, 0, 0), K(.10f, -.03f, .02f, -.04f), K(.14f, -.03f, .027f, -.04f),
                    K(.24f, -.03f, .018f, -.04f), K(.50f, -.03f, .014f, -.04f), K(.57f, -.03f, .004f, -.04f),
                    K(.61f, -.03f, .026f, -.04f), K(.68f, -.03f, .016f, -.04f), K(.86f, -.012f, .006f, -.016f),
                    K(1f, 0, 0, 0));
            track.Rotation = empty
                ? new MotionCurve(K(0f, 0, 0, 0), K(.10f, -12, 10, 22), K(.14f, -15, 10, 24), K(.50f, -12, 10, 22),
                    K(.57f, -8, 8, 18), K(.61f, -15, 8, 21), K(.68f, -10, 8, 20), K(.76f, -6, 14, 30),
                    K(.80f, -11, 14, 27), K(.86f, -6, 8, 16), K(1f, 0, 0, 0))
                : new MotionCurve(K(0f, 0, 0, 0), K(.10f, -12, 10, 22), K(.14f, -15, 10, 24), K(.50f, -12, 10, 22),
                    K(.57f, -8, 8, 18), K(.61f, -15, 8, 21), K(.68f, -10, 8, 20), K(.86f, -3, 3, 7), K(1f, 0, 0, 0));
            track.Hand = empty
                ? new MotionCurve(K(0f, 0, 0, 0), K(.12f, -.05f, -.06f, -.02f), K(.27f, -.10f, -.25f, 0),
                    K(.32f, -.10f, -.25f, 0), W(.46f, w, 0, -.09f, 0), W(.54f, w, 0, -.02f, 0), W(.58f, w, 0, 0, 0),
                    W(.61f, w, 0, .012f, 0), K(.72f, s.X, s.Y, s.Z), K(.76f, s.X, s.Y, s.Z),
                    K(.80f, s.X, s.Y, s.Z - .03f), K(.83f, s.X + .02f, s.Y + .012f, s.Z - .03f), K(.94f, 0, 0, 0),
                    K(1f, 0, 0, 0))
                : new MotionCurve(K(0f, 0, 0, 0), K(.12f, -.05f, -.06f, -.02f), K(.27f, -.10f, -.25f, 0),
                    K(.32f, -.10f, -.25f, 0), W(.46f, w, 0, -.09f, 0), W(.54f, w, 0, -.02f, 0), W(.58f, w, 0, 0, 0),
                    W(.61f, w, 0, .012f, 0), K(.74f, w.X * .5f, w.Y * .5f, w.Z * .5f), K(.88f, 0, 0, 0), K(1f, 0, 0, 0));
            track.Open = empty
                ? new MotionCurve(O(0f, 0), O(.10f, .8f), O(.26f, .9f), O(.31f, 0), O(.58f, 0), O(.62f, .8f),
                    O(.70f, .7f), O(.75f, .1f), O(.82f, .1f), O(.86f, .8f), O(.96f, .2f), O(1f, 0))
                : new MotionCurve(O(0f, 0), O(.10f, .8f), O(.26f, .9f), O(.31f, 0), O(.58f, 0), O(.62f, .8f),
                    O(.78f, .6f), O(.94f, .2f), O(1f, 0));
            return track;
        }

        /// <summary>
        /// Evaluates the reload at <paramref name="reloadPhase"/> (0..1 over the authoritative window; non-positive = rest).
        /// <paramref name="reloadSeconds"/> only times the dropped magazine's free fall.
        /// Reduced motion keeps the hand, magazines and weapon travel (they must stay attached) but halves the tilt/cant.
        /// </summary>
        public static ReloadPose Evaluate(WeaponVisualFamily family, float reloadPhase, bool magazineEmpty,
            float reloadSeconds, bool reducedMotion)
        {
            if (!float.IsFinite(reloadPhase)) throw new ArgumentOutOfRangeException(nameof(reloadPhase));
            if (!float.IsFinite(reloadSeconds)) throw new ArgumentOutOfRangeException(nameof(reloadSeconds));
            if (reloadPhase <= 0f || reloadPhase >= 1f) return ReloadPose.Rest;
            Track? track = family == WeaponVisualFamily.PrimaryGun ? (magazineEmpty ? RifleEmpty : RifleTactical)
                : family == WeaponVisualFamily.Sidearm ? (magazineEmpty ? PistolEmpty : PistolTactical)
                : null;
            if (track == null) return ReloadPose.Rest;

            float t = reloadPhase;
            // The weapon still has to come down to the hand (position kept); only the tilt/cant is halved.
            Float3 pos = track.Position.Sample(t);
            Float3 rot = MotionCurve.Scale(track.Rotation.Sample(t), reducedMotion ? 0.5f : 1f);
            Float3 hand = track.Hand.Sample(t);
            float open = Math.Clamp(track.Open.Sample(t).X, 0f, 1f);
            Float3 inHand = MotionCurve.Sub(hand, track.Well);

            // Old magazine: seated, then carried in the support hand (tactical strip), then in free fall.
            Float3 magazine = default;
            bool magazineVisible = true;
            if (t >= track.Seat)
            {
                magazine = default;
            }
            else if (t >= track.Release)
            {
                Float3 start = track.Release > track.Grip ? MotionCurve.Sub(track.Hand.Sample(track.Release), track.Well) : default;
                float seconds = (t - track.Release) * Math.Max(reloadSeconds, 0.1f);
                // A stripped magazine is tossed slightly outward; a dropped one falls straight out of the well.
                float drift = track.Release > track.Grip ? 0.25f : 0.05f;
                magazine = new Float3(
                    start.X + drift * seconds,
                    start.Y - 0.5f * DropGravity * seconds * seconds,
                    start.Z - 0.06f * seconds);
                magazineVisible = magazine.Y > -DropHideDepth && t < track.FreshFrom + 0.04f;
            }
            else if (t >= track.Grip)
            {
                magazine = inHand;
            }

            bool fresh = t >= track.FreshFrom && t < track.Seat;
            float cycle = -1f;
            if (track.CycleRelease > 0f)
                cycle = 1f - MotionCurve.Ramp(t, track.CycleRelease, track.CycleRelease + 0.018f);

            return new ReloadPose(pos, rot, magazine, magazineVisible, fresh ? inHand : default, fresh, hand, open, cycle);
        }

        /// <summary>Phase at which the fresh magazine seats (for tests and audio alignment).</summary>
        public static float SeatPhase(WeaponVisualFamily family) =>
            family == WeaponVisualFamily.Sidearm ? PistolTactical.Seat : RifleTactical.Seat;

        /// <summary>Phase at which the old magazine leaves the well.</summary>
        public static float ReleasePhase(WeaponVisualFamily family, bool magazineEmpty) =>
            family == WeaponVisualFamily.Sidearm
                ? (magazineEmpty ? PistolEmpty : PistolTactical).Release
                : (magazineEmpty ? RifleEmpty : RifleTactical).Release;

        /// <summary>Phase of the bolt-catch press or slide release on an empty reload; negative for a tactical reload.</summary>
        public static float CycleReleasePhase(WeaponVisualFamily family, bool magazineEmpty) =>
            family == WeaponVisualFamily.Sidearm
                ? (magazineEmpty ? PistolEmpty : PistolTactical).CycleRelease
                : (magazineEmpty ? RifleEmpty : RifleTactical).CycleRelease;
    }
}
