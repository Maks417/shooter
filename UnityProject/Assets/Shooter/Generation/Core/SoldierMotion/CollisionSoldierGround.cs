#nullable enable
using System;using System.Collections.Generic;using Shooter.Generation;using Shooter.Generation.Environment;
namespace Shooter.Generation
{
    /// <summary>Cosmetic support from the exact installed axis-aligned gameplay boxes plus its explicit y=0 foundation.
    /// Never raycasts new geometry or modifies the authority root.</summary>
    public sealed class CollisionSoldierGround:ISoldierGround
    {
        private readonly IReadOnlyList<CollisionBox> boxes;
        public float ReferenceHeight { get; set; }
        public CollisionSoldierGround(IReadOnlyList<CollisionBox> boxes){this.boxes=boxes??throw new ArgumentNullException(nameof(boxes));}
        public bool TrySample(float x,float z,out float y)
        {
            y=float.MinValue;
            if(Math.Abs(x)<=CompoundPlan.MapHalfExtent&&Math.Abs(z)<=CompoundPlan.MapHalfExtent&&ReferenceHeight>=-.05f&&ReferenceHeight<=.4f)y=0;
            foreach(var box in boxes)
            {
                if(!box.Gameplay)continue;
                if(Math.Abs(x-box.Center.X)>box.Size.X*.5f||Math.Abs(z-box.Center.Z)>box.Size.Z*.5f)continue;
                float top=box.Center.Y+box.Size.Y*.5f;
                if(top<=ReferenceHeight+.25f&&top>=ReferenceHeight-.5f&&top>y)y=top;
            }
            return y!=float.MinValue;
        }
    }
}
