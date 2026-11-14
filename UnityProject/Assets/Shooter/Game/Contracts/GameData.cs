#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Game.Contracts
{
    public sealed class SessionRequest
    {
        public MapId MapId { get; }
        public ModeId ModeId { get; }
        public byte TeamSize { get; }
        public ulong Seed { get; }
        public uint RuleVersion { get; }

        public SessionRequest(MapId mapId, ModeId modeId, byte teamSize, ulong seed, uint ruleVersion = GameContractVersion.RuleVersion)
        {
            GameRequire.That(teamSize == 5 || teamSize == 6, nameof(teamSize));
            GameRequire.That(ruleVersion == GameContractVersion.RuleVersion, nameof(ruleVersion));
            MapId = mapId;
            ModeId = modeId;
            TeamSize = teamSize;
            Seed = seed;
            RuleVersion = ruleVersion;
        }

        public int ScoreLimit => GameContractVersion.ScoreLimit;
        public int TimeLimitActiveTicks => GameContractVersion.TimeLimitActiveTicks;
        public int ParticipantCount => TeamSize * 2;
    }

    public sealed class MapCatalogEntry
    {
        public MapId MapId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public FrozenList<ModeId> SupportedModes { get; }
        public FrozenList<byte> SupportedTeamSizes { get; }
        public ulong RecipeSeed { get; }
        public string RecipeVersion { get; }
        public ContentId GameplayRootHash { get; }
        public ContentId LowVisualRootHash { get; }
        public ContentId BalancedVisualRootHash { get; }
        public ContentId HighVisualRootHash { get; }

        public MapCatalogEntry(
            MapId mapId,
            string displayName,
            string description,
            IReadOnlyList<ModeId> supportedModes,
            IReadOnlyList<byte> supportedTeamSizes,
            ulong recipeSeed,
            string recipeVersion,
            ContentId gameplayRootHash,
            ContentId lowVisualRootHash,
            ContentId balancedVisualRootHash,
            ContentId highVisualRootHash)
        {
            MapId = mapId;
            DisplayName = GameRequire.Display(displayName, nameof(displayName), GameContractVersion.MaxMapId);
            Description = GameRequire.Display(description, nameof(description), GameContractVersion.MaxDescription);
            SupportedModes = new FrozenList<ModeId>(supportedModes, GameContractVersion.MaxModesPerMap);
            GameRequire.That(SupportedModes.Count >= 1, nameof(supportedModes));
            var sizes = GameRequire.Copy(supportedTeamSizes, 8, nameof(supportedTeamSizes));
            foreach (var size in sizes) GameRequire.That(size == 5 || size == 6, nameof(supportedTeamSizes));
            SupportedTeamSizes = new FrozenList<byte>(sizes, 8);
            RecipeSeed = recipeSeed;
            RecipeVersion = GameRequire.Display(recipeVersion, nameof(recipeVersion), 64);
            GameplayRootHash = gameplayRootHash;
            LowVisualRootHash = lowVisualRootHash;
            BalancedVisualRootHash = balancedVisualRootHash;
            HighVisualRootHash = highVisualRootHash;
        }
    }

    public readonly struct SpawnSlot
    {
        public uint SlotId { get; }
        public byte Team { get; }
        public Float3 Position { get; }
        public float Yaw { get; }

        public SpawnSlot(uint slotId, byte team, Float3 position, float yaw)
        {
            GameRequire.That(slotId != 0, nameof(slotId));
            GameRequire.That(team == 1 || team == 2, nameof(team));
            GameRequire.Finite(yaw, nameof(yaw));
            SlotId = slotId;
            Team = team;
            Position = position;
            Yaw = yaw;
        }
    }

    public sealed class MapGameplayDefinition
    {
        public MapId MapId { get; }
        public uint CollisionRevision { get; }
        public ContentId CollisionHash { get; }
        public uint NavRevision { get; }
        public ContentId NavHash { get; }
        public Aabb Bounds { get; }
        public FrozenList<SpawnSlot> SpawnSlots { get; }
        public FrozenList<Float3> PatrolDestinations { get; }
        public FrozenList<Float3> CombatDestinations { get; }

        public MapGameplayDefinition(
            MapId mapId,
            uint collisionRevision,
            ContentId collisionHash,
            uint navRevision,
            ContentId navHash,
            Aabb bounds,
            IReadOnlyList<SpawnSlot> spawnSlots,
            IReadOnlyList<Float3> patrolDestinations,
            IReadOnlyList<Float3> combatDestinations)
        {
            GameRequire.That(collisionRevision != 0, nameof(collisionRevision));
            GameRequire.That(navRevision != 0, nameof(navRevision));
            MapId = mapId;
            CollisionRevision = collisionRevision;
            CollisionHash = collisionHash;
            NavRevision = navRevision;
            NavHash = navHash;
            Bounds = bounds;
            SpawnSlots = new FrozenList<SpawnSlot>(spawnSlots, GameContractVersion.MaxSpawnSlots);
            PatrolDestinations = new FrozenList<Float3>(patrolDestinations, 64);
            CombatDestinations = new FrozenList<Float3>(combatDestinations, 64);
            GameRequire.That(SpawnSlots.Count >= 2, nameof(spawnSlots));
            foreach (var slot in SpawnSlots)
            {
                GameRequire.That(InBounds(slot.Position, bounds), nameof(spawnSlots));
            }
        }

        private static bool InBounds(Float3 p, Aabb bounds) =>
            p.X >= bounds.Min.X && p.X <= bounds.Max.X &&
            p.Y >= bounds.Min.Y && p.Y <= bounds.Max.Y &&
            p.Z >= bounds.Min.Z && p.Z <= bounds.Max.Z;
    }

    public sealed class ParticipantState
    {
        public ParticipantId ParticipantId { get; }
        public string DisplayName { get; }
        public byte Team { get; }
        public bool IsHuman { get; }
        public EntityId EntityId { get; }
        public LifeId LifeId { get; }
        public bool Alive { get; }
        public uint Kills { get; }
        public uint Deaths { get; }
        public uint RespawnDueActiveTick { get; }

        public ParticipantState(
            ParticipantId participantId,
            string displayName,
            byte team,
            bool isHuman,
            EntityId entityId,
            LifeId lifeId,
            bool alive,
            uint kills,
            uint deaths,
            uint respawnDueActiveTick)
        {
            GameRequire.That(team == 1 || team == 2, nameof(team));
            ParticipantId = participantId;
            DisplayName = GameRequire.Display(displayName, nameof(displayName), GameContractVersion.MaxDisplayName);
            Team = team;
            IsHuman = isHuman;
            EntityId = entityId;
            LifeId = lifeId;
            Alive = alive;
            Kills = kills;
            Deaths = deaths;
            RespawnDueActiveTick = respawnDueActiveTick;
        }
    }

    public sealed class MatchViewSnapshot
    {
        public ulong AuthoritySessionId { get; }
        public uint CompletedSimulationTick { get; }
        public uint ActiveTicksElapsed { get; }
        public uint RemainingActiveTicks { get; }
        public GameMatchPhase Phase { get; }
        public MatchOutcome Outcome { get; }
        public FinishReason Reason { get; }
        public uint Team1Kills { get; }
        public uint Team2Kills { get; }
        public FrozenList<ParticipantState> Participants { get; }
        public uint PendingRespawns { get; }

        public MatchViewSnapshot(
            ulong authoritySessionId,
            uint completedSimulationTick,
            uint activeTicksElapsed,
            uint remainingActiveTicks,
            GameMatchPhase phase,
            MatchOutcome outcome,
            FinishReason reason,
            uint team1Kills,
            uint team2Kills,
            IReadOnlyList<ParticipantState> participants,
            uint pendingRespawns)
        {
            GameRequire.That(authoritySessionId != 0, nameof(authoritySessionId));
            GameRequire.EnumDefined(phase, nameof(phase));
            GameRequire.EnumDefined(outcome, nameof(outcome));
            GameRequire.EnumDefined(reason, nameof(reason));
            AuthoritySessionId = authoritySessionId;
            CompletedSimulationTick = completedSimulationTick;
            ActiveTicksElapsed = activeTicksElapsed;
            RemainingActiveTicks = remainingActiveTicks;
            Phase = phase;
            Outcome = outcome;
            Reason = reason;
            Team1Kills = team1Kills;
            Team2Kills = team2Kills;
            Participants = new FrozenList<ParticipantState>(participants, GameContractVersion.MaxParticipants);
            PendingRespawns = pendingRespawns;
        }
    }

    public sealed class DeathResolution
    {
        public ulong AuthoritySessionId { get; }
        public EventId DeathEventId { get; }
        public EntityId VictimEntityId { get; }
        public LifeId VictimLifeId { get; }
        public EntityId? KillerEntityId { get; }
        public uint CompletedTick { get; }

        public DeathResolution(
            ulong authoritySessionId,
            EventId deathEventId,
            EntityId victimEntityId,
            LifeId victimLifeId,
            EntityId? killerEntityId,
            uint completedTick)
        {
            GameRequire.That(authoritySessionId != 0, nameof(authoritySessionId));
            AuthoritySessionId = authoritySessionId;
            DeathEventId = deathEventId;
            VictimEntityId = victimEntityId;
            VictimLifeId = victimLifeId;
            KillerEntityId = killerEntityId;
            CompletedTick = completedTick;
        }
    }

    public sealed class MatchResults
    {
        public SessionRequest Request { get; }
        public MapId MapId { get; }
        public string CatalogIdentity { get; }
        public string GameplayIdentity { get; }
        public string BuildIdentity { get; }
        public ulong AuthoritySessionId { get; }
        public uint StartTick { get; }
        public uint EndTick { get; }
        public uint ActiveDurationTicks { get; }
        public uint Team1Kills { get; }
        public uint Team2Kills { get; }
        public MatchOutcome Outcome { get; }
        public FinishReason Reason { get; }
        public FrozenList<ParticipantState> FinalRows { get; }

        public MatchResults(
            SessionRequest request,
            MapId mapId,
            string catalogIdentity,
            string gameplayIdentity,
            string buildIdentity,
            ulong authoritySessionId,
            uint startTick,
            uint endTick,
            uint activeDurationTicks,
            uint team1Kills,
            uint team2Kills,
            MatchOutcome outcome,
            FinishReason reason,
            IReadOnlyList<ParticipantState> finalRows)
        {
            GameRequire.That(outcome != MatchOutcome.None, nameof(outcome));
            GameRequire.That(reason != FinishReason.None, nameof(reason));
            GameRequire.That(authoritySessionId != 0, nameof(authoritySessionId));
            Request = request ?? throw new ArgumentNullException(nameof(request));
            MapId = mapId;
            CatalogIdentity = GameRequire.Display(catalogIdentity, nameof(catalogIdentity), 128);
            GameplayIdentity = GameRequire.Display(gameplayIdentity, nameof(gameplayIdentity), 128);
            BuildIdentity = GameRequire.Display(buildIdentity, nameof(buildIdentity), 128);
            AuthoritySessionId = authoritySessionId;
            StartTick = startTick;
            EndTick = endTick;
            ActiveDurationTicks = activeDurationTicks;
            Team1Kills = team1Kills;
            Team2Kills = team2Kills;
            Outcome = outcome;
            Reason = reason;
            FinalRows = new FrozenList<ParticipantState>(finalRows, GameContractVersion.MaxParticipants);
        }
    }

    /// <summary>Immutable local presentation/input preferences. Schema 2 per ADR-036; no gameplay mutation.</summary>
    public sealed class UserSettings
    {
        public const string SchemaId = "shooter.user-settings.v2";
        public const string ReservedEscapeControl = "Key.Escape";

        public uint Schema { get; }
        public float MasterVolume { get; }
        public float EffectsVolume { get; }
        public float UiVolume { get; }
        public byte QualityTier { get; }
        public int ResolutionWidth { get; }
        public int ResolutionHeight { get; }
        public DisplayMode DisplayMode { get; }
        public bool VSync { get; }
        public int FpsCap { get; }
        public float HipFovDegrees { get; }
        public float MouseSensitivity { get; }
        public bool InvertY { get; }
        public float UiScale { get; }
        public bool ReducedCameraMotion { get; }
        public bool ReducedFlash { get; }
        public float Contrast { get; }
        public bool Subtitles { get; }
        public FrozenList<InputBinding> Bindings { get; }

        public UserSettings(
            uint schema,
            float masterVolume,
            float effectsVolume,
            float uiVolume,
            byte qualityTier,
            int resolutionWidth,
            int resolutionHeight,
            DisplayMode displayMode,
            bool vSync,
            int fpsCap,
            float hipFovDegrees,
            float mouseSensitivity,
            bool invertY,
            float uiScale,
            bool reducedCameraMotion,
            bool reducedFlash,
            float contrast,
            bool subtitles,
            IReadOnlyList<InputBinding> bindings)
        {
            GameRequire.That(schema == GameContractVersion.SettingsSchema, nameof(schema));
            GameRequire.Finite(masterVolume, nameof(masterVolume));
            GameRequire.Finite(effectsVolume, nameof(effectsVolume));
            GameRequire.Finite(uiVolume, nameof(uiVolume));
            GameRequire.That(masterVolume >= 0f && masterVolume <= 1f, nameof(masterVolume));
            GameRequire.That(effectsVolume >= 0f && effectsVolume <= 1f, nameof(effectsVolume));
            GameRequire.That(uiVolume >= 0f && uiVolume <= 1f, nameof(uiVolume));
            GameRequire.That(qualityTier <= 2, nameof(qualityTier));
            GameRequire.That(resolutionWidth >= GameContractVersion.MinResolutionWidth, nameof(resolutionWidth));
            GameRequire.That(resolutionHeight >= GameContractVersion.MinResolutionHeight, nameof(resolutionHeight));
            GameRequire.EnumDefined(displayMode, nameof(displayMode));
            GameRequire.That(fpsCap == 0 || fpsCap == 30 || fpsCap == 60 || fpsCap == 120, nameof(fpsCap));
            GameRequire.Finite(hipFovDegrees, nameof(hipFovDegrees));
            GameRequire.That(hipFovDegrees >= GameContractVersion.MinHipFovDegrees && hipFovDegrees <= GameContractVersion.MaxHipFovDegrees, nameof(hipFovDegrees));
            GameRequire.Finite(mouseSensitivity, nameof(mouseSensitivity));
            GameRequire.That(mouseSensitivity >= GameContractVersion.MinMouseSensitivity && mouseSensitivity <= GameContractVersion.MaxMouseSensitivity, nameof(mouseSensitivity));
            GameRequire.Finite(uiScale, nameof(uiScale));
            GameRequire.That(uiScale >= GameContractVersion.MinUiScale && uiScale <= GameContractVersion.MaxUiScale, nameof(uiScale));
            GameRequire.Finite(contrast, nameof(contrast));
            GameRequire.That(contrast >= GameContractVersion.MinContrast && contrast <= GameContractVersion.MaxContrast, nameof(contrast));
            Bindings = ValidateBindings(bindings);
            Schema = schema;
            MasterVolume = masterVolume;
            EffectsVolume = effectsVolume;
            UiVolume = uiVolume;
            QualityTier = qualityTier;
            ResolutionWidth = resolutionWidth;
            ResolutionHeight = resolutionHeight;
            DisplayMode = displayMode;
            VSync = vSync;
            FpsCap = fpsCap;
            HipFovDegrees = hipFovDegrees;
            MouseSensitivity = mouseSensitivity;
            InvertY = invertY;
            UiScale = uiScale;
            ReducedCameraMotion = reducedCameraMotion;
            ReducedFlash = reducedFlash;
            Contrast = contrast;
            Subtitles = subtitles;
        }

        public static UserSettings Defaults => new UserSettings(
            GameContractVersion.SettingsSchema,
            1f,
            1f,
            0.7f,
            0,
            1280,
            720,
            DisplayMode.Windowed,
            false,
            60,
            80f,
            0.10f,
            false,
            1f,
            false,
            false,
            1f,
            false,
            DefaultBindings());

        public bool SameDisplaySurface(UserSettings other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            return ResolutionWidth == other.ResolutionWidth
                && ResolutionHeight == other.ResolutionHeight
                && DisplayMode == other.DisplayMode
                && VSync == other.VSync
                && FpsCap == other.FpsCap;
        }

        public static IReadOnlyList<InputBinding> DefaultBindings() => new[]
        {
            new InputBinding("MoveForward", "Key.W"),
            new InputBinding("MoveBack", "Key.S"),
            new InputBinding("MoveLeft", "Key.A"),
            new InputBinding("MoveRight", "Key.D"),
            new InputBinding("Aim", "Mouse.Right"),
            new InputBinding("Fire", "Mouse.Left"),
            new InputBinding("Reload", "Key.R"),
            new InputBinding("Sprint", "Key.LeftShift"),
            new InputBinding("Crouch", "Key.LeftCtrl"),
            new InputBinding("Jump", "Key.Space"),
            new InputBinding("Slot1", "Key.Digit1"),
            new InputBinding("Slot2", "Key.Digit2"),
            new InputBinding("Scoreboard", "Key.Tab"),
            new InputBinding("Pause", ReservedEscapeControl)
        };

        private static FrozenList<InputBinding> ValidateBindings(IReadOnlyList<InputBinding> bindings)
        {
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            var list = new FrozenList<InputBinding>(bindings, GameContractVersion.MaxSettingsBindings);
            GameRequire.That(list.Count >= 1, nameof(bindings));
            var actions = new HashSet<string>(StringComparer.Ordinal);
            var controls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool pauseEscape = false;
            foreach (var binding in list)
            {
                GameRequire.That(actions.Add(binding.Action), nameof(bindings));
                GameRequire.That(controls.Add(binding.Control), nameof(bindings));
                if (string.Equals(binding.Action, "Pause", StringComparison.Ordinal)
                    && string.Equals(binding.Control, ReservedEscapeControl, StringComparison.OrdinalIgnoreCase))
                    pauseEscape = true;
                if (!string.Equals(binding.Action, "Pause", StringComparison.Ordinal)
                    && string.Equals(binding.Control, ReservedEscapeControl, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Escape is reserved for Pause/menu restore.", nameof(bindings));
            }
            GameRequire.That(actions.Contains("Pause") && pauseEscape, nameof(bindings));
            foreach (var required in new[]
                     {
                         "MoveForward", "MoveBack", "MoveLeft", "MoveRight", "Aim", "Fire", "Reload",
                         "Sprint", "Crouch", "Jump", "Slot1", "Slot2", "Scoreboard", "Pause"
                     })
            {
                GameRequire.That(actions.Contains(required), nameof(bindings));
            }
            return list;
        }
    }

    public readonly struct InputBinding : IEquatable<InputBinding>
    {
        public string Action { get; }
        public string Control { get; }

        public InputBinding(string action, string control)
        {
            Action = GameRequire.Display(action, nameof(action), GameContractVersion.MaxBindingToken);
            Control = GameRequire.Display(control, nameof(control), GameContractVersion.MaxBindingToken);
            foreach (char c in Action)
                GameRequire.That((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'), nameof(action));
        }

        public bool Equals(InputBinding other) =>
            string.Equals(Action, other.Action, StringComparison.Ordinal)
            && string.Equals(Control, other.Control, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is InputBinding other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Action, Control);
        public override string ToString() => Action + "=" + Control;
    }

    public sealed class GameOperationResult
    {
        public GameErrorCode Error { get; }
        public string Diagnostic { get; }
        public string UserFacing { get; }

        public GameOperationResult(GameErrorCode error, string diagnostic, string userFacing)
        {
            GameRequire.EnumDefined(error, nameof(error));
            Diagnostic = GameRequire.Display(diagnostic ?? string.Empty, nameof(diagnostic), 512);
            UserFacing = GameRequire.Display(userFacing ?? string.Empty, nameof(userFacing), 256);
            Error = error;
        }

        public static GameOperationResult Ok => new GameOperationResult(GameErrorCode.None, "ok", "ok");
        public bool Succeeded => Error == GameErrorCode.None;
    }
}
