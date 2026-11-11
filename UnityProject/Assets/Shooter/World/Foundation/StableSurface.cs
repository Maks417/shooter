using UnityEngine;

namespace Shooter.World
{
    public sealed class StableSurface : MonoBehaviour
    {
        public ulong StableId { get; private set; }
        public ulong SurfaceId { get; private set; }
        public ushort SurfaceTag { get; private set; }
        public void Initialize(ulong stableId, ulong surfaceId, ushort surfaceTag)
        {
            if (stableId == 0 || surfaceId == 0) throw new System.ArgumentException("Static IDs must be nonzero");
            StableId = stableId; SurfaceId = surfaceId; SurfaceTag = surfaceTag;
        }
    }
}
