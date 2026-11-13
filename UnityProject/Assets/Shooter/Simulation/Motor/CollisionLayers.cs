#nullable enable
namespace Shooter.Simulation.Motor
{
    /// <summary>Fixed layer indices from gameplay-baseline / TagManager (integrator-owned settings).</summary>
    public static class CollisionLayers
    {
        public const int WorldSolid = 8;
        public const int ActorMotor = 9;
        public const int ActorHit = 10;
        public const int Cosmetic = 11;
        public const int ObjectiveTrigger = 12;
        public const int SensorTrigger = 13;

        public static uint Bit(int layer) => 1u << layer;
        public static uint MotorMask => Bit(WorldSolid);
        public static uint BulletObstructionMask => Bit(WorldSolid) | Bit(ActorHit);
        public static uint VisionMask => Bit(WorldSolid);
    }
}
