#nullable enable
namespace Shooter.Simulation
{
    /// <summary>T027 mirrored gameplay defaults (docs/fixtures/gameplay-defaults.json). Settings/layer indices remain integrator-owned.</summary>
    public static class GameplayDefaults
    {
        public const string SchemaId = "shooter.gameplay.v1";
        public const int TickRate = 60;
        public const int WorldSolidLayer = 8;
        public const int ActorMotorLayer = 9;
        public const int ActorHitLayer = 10;
        public const int CosmeticLayer = 11;
        public const int ObjectiveTriggerLayer = 12;
        public const int SensorTriggerLayer = 13;
        public const ushort MaxHealth = 100;
        public const int CaptureTicks = 120;
        public const int BotCount = 8;
    }
}
