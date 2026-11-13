#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    public readonly struct ChunkCoord : IEquatable<ChunkCoord>
    {
        public int X { get; }
        public int Z { get; }
        public ChunkCoord(int x, int z)
        {
            if (x < 0 || x > 7 || z < 0 || z > 7) throw new ArgumentOutOfRangeException(nameof(x));
            X = x; Z = z;
        }
        public bool Equals(ChunkCoord other) => X == other.X && Z == other.Z;
        public override bool Equals(object? obj) => obj is ChunkCoord c && Equals(c);
        public override int GetHashCode() => (X << 3) ^ Z;
    }

    public readonly struct BuildingFootprint
    {
        public int SlotIndex { get; }
        public Float3 Center { get; }
        public float SizeX { get; }
        public float SizeZ { get; }
        public float Height { get; }
        public bool HasRoof { get; }
        public BuildingFootprint(int slotIndex, Float3 center, float sizeX, float sizeZ, float height, bool hasRoof)
        {
            if (slotIndex < 0 || sizeX <= 0 || sizeZ <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(slotIndex));
            SlotIndex = slotIndex; Center = center; SizeX = sizeX; SizeZ = sizeZ; Height = height; HasRoof = hasRoof;
        }
    }

    public readonly struct PropPlacement
    {
        public int Index { get; }
        public Float3 Center { get; }
        public float SizeX { get; }
        public float SizeY { get; }
        public float SizeZ { get; }
        public ushort SurfaceTag { get; }
        public PropPlacement(int index, Float3 center, float sizeX, float sizeY, float sizeZ, ushort surfaceTag)
        {
            if (index < 0 || sizeX <= 0 || sizeY <= 0 || sizeZ <= 0 || surfaceTag == 0) throw new ArgumentOutOfRangeException(nameof(index));
            Index = index; Center = center; SizeX = sizeX; SizeY = sizeY; SizeZ = sizeZ; SurfaceTag = surfaceTag;
        }
    }

    public readonly struct CoverProxy
    {
        public ulong StableId { get; }
        public Float3 Center { get; }
        public float SizeX { get; }
        public float SizeY { get; }
        public float SizeZ { get; }
        public Float3 PeekStanding { get; }
        public Float3 PeekCrouch { get; }
        public bool HardCover { get; }
        public CoverProxy(ulong stableId, Float3 center, float sizeX, float sizeY, float sizeZ, Float3 peekStanding, Float3 peekCrouch, bool hardCover)
        {
            if (stableId == 0 || sizeX <= 0 || sizeY <= 0 || sizeZ <= 0) throw new ArgumentOutOfRangeException(nameof(stableId));
            StableId = stableId; Center = center; SizeX = sizeX; SizeY = sizeY; SizeZ = sizeZ;
            PeekStanding = peekStanding; PeekCrouch = peekCrouch; HardCover = hardCover;
        }
    }

    public readonly struct CollisionBox
    {
        public ulong StableId { get; }
        public ulong SurfaceId { get; }
        public ushort SurfaceTag { get; }
        public Float3 Center { get; }
        public Float3 Size { get; }
        public bool Gameplay { get; }
        public CollisionBox(ulong stableId, ulong surfaceId, ushort surfaceTag, Float3 center, Float3 size, bool gameplay)
        {
            if (stableId == 0 || surfaceId == 0 || surfaceTag == 0 || size.X <= 0 || size.Y <= 0 || size.Z <= 0)
                throw new ArgumentOutOfRangeException(nameof(stableId));
            StableId = stableId; SurfaceId = surfaceId; SurfaceTag = surfaceTag; Center = center; Size = size; Gameplay = gameplay;
        }
    }

    /// <summary>Immutable generated compound for one seed. Gameplay geometry is profile-independent.</summary>
    public sealed class CompoundPlan
    {
        public const float MapHalfExtent = 128f;
        public const float ChunkSize = 32f;
        public const int ChunkCount = 8;
        public const string GeneratorId = "environment.compound";
        public const string GeneratorVersion = "1.0.0";

        public ulong Seed { get; }
        public uint CollisionRevision { get; }
        public uint NavRevision { get; }
        public ContentId CollisionHash { get; }
        public ContentId NavHash { get; }
        public Float3 PlayerSpawn { get; }
        public Float3 ObjectiveCenter { get; }
        public float ObjectiveRadius { get; }
        public IReadOnlyList<Float3> BotSpawns { get; }
        public IReadOnlyList<BuildingFootprint> Buildings { get; }
        public IReadOnlyList<PropPlacement> Props { get; }
        public IReadOnlyList<CoverProxy> Covers { get; }
        public IReadOnlyList<CollisionBox> CollisionBoxes { get; }
        public IReadOnlyList<Float3> RouteCorners { get; }
        public float[,] HeightSamples { get; }
        public int HeightResolution { get; }

        public CompoundPlan(
            ulong seed,
            uint collisionRevision,
            uint navRevision,
            ContentId collisionHash,
            ContentId navHash,
            Float3 playerSpawn,
            Float3 objectiveCenter,
            float objectiveRadius,
            IReadOnlyList<Float3> botSpawns,
            IReadOnlyList<BuildingFootprint> buildings,
            IReadOnlyList<PropPlacement> props,
            IReadOnlyList<CoverProxy> covers,
            IReadOnlyList<CollisionBox> collisionBoxes,
            IReadOnlyList<Float3> routeCorners,
            float[,] heightSamples)
        {
            if (botSpawns == null || buildings == null || props == null || covers == null || collisionBoxes == null || routeCorners == null || heightSamples == null)
                throw new ArgumentNullException(nameof(botSpawns));
            if (objectiveRadius <= 0) throw new ArgumentOutOfRangeException(nameof(objectiveRadius));
            if (heightSamples.GetLength(0) != heightSamples.GetLength(1)) throw new ArgumentException("Height grid must be square.");
            Seed = seed; CollisionRevision = collisionRevision; NavRevision = navRevision;
            CollisionHash = collisionHash; NavHash = navHash;
            PlayerSpawn = playerSpawn; ObjectiveCenter = objectiveCenter; ObjectiveRadius = objectiveRadius;
            BotSpawns = botSpawns; Buildings = buildings; Props = props; Covers = covers;
            CollisionBoxes = collisionBoxes; RouteCorners = routeCorners; HeightSamples = heightSamples;
            HeightResolution = heightSamples.GetLength(0);
        }

        public static ChunkCoord ChunkAt(float x, float z)
        {
            int cx = (int)Math.Floor((x + MapHalfExtent) / ChunkSize);
            int cz = (int)Math.Floor((z + MapHalfExtent) / ChunkSize);
            cx = Math.Clamp(cx, 0, ChunkCount - 1);
            cz = Math.Clamp(cz, 0, ChunkCount - 1);
            return new ChunkCoord(cx, cz);
        }
    }
}
