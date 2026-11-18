#nullable enable

namespace Shooter.Simulation.AI
{
    /// <summary>
    /// T082/T085/T090 combat-behaviour tuning (docs/features/ai.md): one data set for every quality profile.
    /// Ticks are 60 Hz simulation ticks; angles are radians unless the name says otherwise.
    /// </summary>
    public static class AiTuning
    {
        public const float TickSeconds = 1f / 60f;

        // Reaction (spec: 250–500 ms initial delay).
        public const uint ReactionBaseTicks = 18;
        public const uint ReactionSpreadTicks = 18;
        /// <summary>Switching to another enemy while already fighting is faster than a cold acquisition.</summary>
        public const uint SwitchReactionBaseTicks = 8;
        public const uint SwitchReactionSpreadTicks = 6;
        /// <summary>Distant targets are harder to pick out: extra reaction ticks per 10 m beyond 15 m (capped).</summary>
        public const float RangeReactionTicksPer10m = 3f;
        public const uint MaxRangeReactionTicks = 12;
        /// <summary>A target seen again within this gap keeps its reaction and accuracy state.</summary>
        public const uint TargetMemoryTicks = 30;
        /// <summary>"In combat" window used for switch reactions, callout suppression and tactical reloads.</summary>
        public const uint CombatMemoryTicks = 120;
        /// <summary>Stale-scan cadence: a bot with a sticky target re-checks other candidates this often.</summary>
        public const uint TargetScanIntervalTicks = 20;
        /// <summary>A newly seen enemy replaces the sticky one only if it is this much closer.</summary>
        public const float TargetSwitchCloserFactor = 0.7f;
        /// <summary>An attacker that damaged the bot this recently is checked first.</summary>
        public const uint AttackerPriorityTicks = 120;

        // Aim turning (spec: bounded angular acceleration).
        public const float CombatTurnRate = 7.3f;        // ~420 deg/s
        public const float CombatTurnAccel = 70f;
        public const float TravelTurnRate = 10f;
        public const float TravelTurnAccel = 90f;
        public const float PitchRate = 5f;
        public const float PitchAccel = 50f;
        public const float MaxPitch = 1.45f;             // inside the C0 ±89° bound

        // Aim uncertainty (spec: seeded uncertainty and accuracy recovery). Expressed as a miss distance at the target.
        public const float AcquireErrorMeters = 1.5f;
        public const float SettledErrorMeters = 0.28f;
        /// <summary>Settled error grows with range so long shots are not laser-accurate.</summary>
        public const float SettledErrorPerMeter = 0.005f;
        /// <summary>Counted from the first tick the bot may fire, so the opening shots are visibly off target (T085).</summary>
        public const uint AccuracyRecoveryTicks = 66;  // ADR-112: eye-ray hits land where aimed; settle slightly slower
        public const float MinErrorRadians = 0.005f;
        public const float MaxErrorRadians = 0.14f;      // ~8 deg
        public const float MovingErrorMeters = 0.18f;
        public const float TargetMotionErrorMeters = 0.2f;
        public const float FlinchErrorMeters = 0.5f;
        public const uint FlinchTicks = 30;
        /// <summary>The error direction drifts every bucket; turning limits smooth the jump.</summary>
        public const uint ErrorBucketTicks = 10;

        // Fire discipline.
        public const float FireToleranceMeters = 0.35f;
        public const float FireToleranceMinRadians = 0.026f; // ~1.5 deg
        public const float CloseRange = 12f;
        public const float LongRange = 30f;
        public const float AdvanceRange = 55f;
        public const float BackOffRange = 4f;
        /// <summary>Legacy default primary cadence; prefer <see cref="Combat.WeaponCatalog"/> / equipped definition.</summary>
        public const uint WeaponIntervalTicks = 6;
        public const uint CloseBurstMinTicks = 60, CloseBurstSpreadTicks = 30, ClosePauseMinTicks = 4, ClosePauseSpreadTicks = 6;
        public const uint MidBurstMinRounds = 3, MidBurstSpreadRounds = 4, MidPauseMinTicks = 10, MidPauseSpreadTicks = 11;
        public const uint LongBurstMinRounds = 1, LongBurstSpreadRounds = 2, LongPauseMinTicks = 16, LongPauseSpreadTicks = 13;

        // Combat movement.
        public const uint StrafeFlipMinTicks = 24;
        public const uint StrafeFlipSpreadTicks = 49;
        public const float StrafeMaxOffset = 3.0f;       // oscillate around the nav point where the fight started (ADR-112 wider)
        public const uint StrafeBlockedCheckTicks = 12;
        public const float StrafeBlockedMeters = 0.15f;
        /// <summary>Entity ids with (id % CrouchShooterModulo) == 0 crouch-burst at mid range instead of strafing.</summary>
        public const ulong CrouchShooterModulo = 3;
        /// <summary>T090: horizontal look-ahead along the strafe axis before committing the step (meters).</summary>
        public const float StrafeProbeMeters = 0.75f;
        /// <summary>T090: max allowed ground drop from current feet to the probe; larger drops flip the strafe.</summary>
        public const float StrafeMaxDropMeters = 0.55f;
        /// <summary>T090: cast starts this far above the foot plane when sampling ground under a strafe probe.</summary>
        public const float StrafeGroundLiftMeters = 0.6f;
        /// <summary>ADR-100: chance (per strafe reversal) of a combat hop; deterministic from id and tick.</summary>
        public const float CombatHopChance = 0.25f;
        public const uint CombatHopCooldownTicks = 180;
        /// <summary>ADR-100 traversal hop probes: knee ray above the 0.25 m motor step, clearance ray below the ~0.6 m apex.</summary>
        public const float VaultProbeHeightMeters = 0.35f;
        public const float VaultClearHeightMeters = 0.75f;
        public const float VaultProbeMeters = 0.9f;
        public const uint VaultProbeIntervalTicks = 6;

        // Hearing and damage stimuli (no rays; distance-attenuated).
        public const float GunfireHearingRadius = 45f;
        public const float GunfireNoisePerMeter = 0.08f;
        public const float DamageSourceNoiseMeters = 1.5f;
        public const uint StimulusRefreshTicks = 30;
        public const uint ThreatLookTicks = 180;
        public const float ThreatLookRange = 30f;

        // T085 footstep hearing: horizontal speed of an enemy → audible radius (no rays).
        public const float SprintStepSpeed = 5.0f, SprintStepRadius = 24f;
        public const float RunStepSpeed = 2.6f, RunStepRadius = 13f;
        public const float WalkStepSpeed = 0.6f, WalkStepRadius = 5f;
        /// <summary>Any enemy this close is noticed regardless of speed (body presence).</summary>
        public const float PresenceRadius = 2.5f;
        public const float FootstepNoisePerMeter = 0.05f;
        /// <summary>A fresh close stimulus (footsteps/damage) wins the look over an older last-seen point.</summary>
        public const uint CloseThreatTicks = 60;
        /// <summary>Hits from an unseen enemy while fighting another: turn after this many within the window.</summary>
        public const int UnseenHitsToTurn = 2;
        public const uint UnseenHitWindowTicks = 90;
        public const ushort TurnImmediatelyBelowHealth = 50;
        public const uint ThreatTurnTicks = 45;

        // Team coordination.
        public const float CalloutRadius = 30f;
        public const int DestinationCrowdLimit = 1;
        /// <summary>Allies' goals/positions closer than this make a patrol or combat destination "taken".</summary>
        public const float SpreadMeters = 12f;
        /// <summary>Preferred patrol leg length; much shorter or longer legs score lower.</summary>
        public const float PatrolPreferredMeters = 35f;
        /// <summary>At most this many allies investigate the same stimulus; the rest hold and watch it.</summary>
        public const int InvestigateCrowdLimit = 2;
        public const float InvestigateCrowdMeters = 10f;
        /// <summary>T090: destinations near an ally's active path corners are penalized (shared corridors).</summary>
        public const float RouteCorridorMeters = 9f;
        /// <summary>T090: score penalty per ally path corner within <see cref="RouteCorridorMeters"/> of a candidate.</summary>
        public const float RouteCorridorPenalty = 1.0f;

        // Health, ammo and cover.
        public const ushort LowHealth = 35;
        public const ushort LosingTradeMargin = 10;
        public const uint RetreatCooldownTicks = 600;
        /// <summary>Legacy default primary magazine; prefer equipped <see cref="Combat.WeaponDefinition.MagazineSize"/>.</summary>
        public const int MagazineSize = 30;
        /// <summary>Legacy 40% of default rifle mag; prefer fraction of equipped magazine size.</summary>
        public const int TacticalReloadBelow = 12;
        public const uint CoverHoldTicks = 120;
        // T087 knife / one grenade per life.
        public const float KnifeEngageMeters = 2.5f;
        public const float GrenadeMinMeters = 8f;
        public const float GrenadeMaxMeters = 32f;
        public const uint GrenadeCookTicks = 24;
        public const float CoverArrivalMeters = 1.0f;
        public const int MaxCoverProbeRaysPerTick = 4;
        public const float CoverChestHeight = 0.75f;      // crouched chest: cover must hide a crouched bot
        public const float ThreatEyeLift = 0.35f;         // threat sense point is the chest; its eye is above
        /// <summary>T090: side offset from a cover point used as a peek sample toward the threat.</summary>
        public const float CoverPeekSideMeters = 0.65f;
        /// <summary>T090: eye height at the peek sample (standing upper chest).</summary>
        public const float CoverPeekEyeHeight = 1.35f;
        /// <summary>Cover probe candidates relative to "away from threat": (degrees, meters), nearest first.</summary>
        public static readonly float[] CoverProbeAngles = { 0f, 40f, -40f, 80f, -80f, 0f, 30f, -30f };
        public static readonly float[] CoverProbeRadii = { 4f, 4f, 4f, 4.5f, 4.5f, 7f, 7f, 7f };

        // T090 wall-aware hearing (occlusion samples; footsteps use leftover AI ray budget when available).
        /// <summary>Minimum ticks between footstep occlusion samples for one bot.</summary>
        public const uint HearOcclusionIntervalTicks = 12;

        // ADR-112 smoothness.
        /// <summary>Ticks a seen target stays engaged through budget-starved (no-ray) ticks: the fair-sense window.</summary>
        public const uint VisibleHoldTicks = 12;
        /// <summary>Per-tick ease of the world move vector (time constant about three ticks).</summary>
        public const float MoveEaseAlpha = 0.3f;
        /// <summary>Distance from a path corner at which the route direction starts bending into the next leg.</summary>
        public const float CornerBlendMeters = 1.5f;

        // ADR-112 map tactics.
        /// <summary>Authored cover farther than this from the bot is not considered.</summary>
        public const float MapCoverSearchMeters = 18f;
        /// <summary>A hiding spot this close to the threat is no hiding spot.</summary>
        public const float MapCoverMinThreatMeters = 6f;
        /// <summary>How much closer to the threat a cover move may take the bot.</summary>
        public const float MapCoverMaxAdvanceMeters = 6f;
        /// <summary>Allies (or their cover spots) within this of a spot make it less attractive.</summary>
        public const float MapCoverAllySpacing = 3f;
        /// <summary>Cover at least this tall hides a standing body: no peeking up over it.</summary>
        public const float StandingCoverHeight = 1.6f;
        /// <summary>In low cover: a peek cycle of this many ticks, standing for the last <see cref="CoverPeekStandTicks"/>.</summary>
        public const uint CoverPeekCycleTicks = 90;
        public const uint CoverPeekStandTicks = 36;
        /// <summary>Open-ground exchange before moving to cover: base plus a seeded spread (ticks).</summary>
        public const uint EngageInOpenTicks = 120;
        public const uint EngageInOpenSpreadTicks = 120;
        public const uint CoverRepositionCooldownTicks = 480;
        /// <summary>Allies engaging/searching within this of a contact are "pressing" it.</summary>
        public const float PressRadiusMeters = 12f;
        public const float FlankMinMeters = 8f;
        public const float FlankMaxMeters = 30f;
        public const float FlankPreferredMeters = 16f;
        public const float FlankArrivalMeters = 2f;
        /// <summary>Destinations this close to the bot's own spawn are avoided (no loitering at home).</summary>
        public const float OwnSpawnAvoidMeters = 15f;
        /// <summary>Two or more allies fighting within this of a destination make it a hot spot to avoid.</summary>
        public const float HotSpotMeters = 15f;
    }
}
