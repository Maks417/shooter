#nullable enable
using System;
using Shooter.Contracts;
using Shooter.Presentation.Audio;

namespace Shooter.Presentation.Fx
{
    /// <summary>
    /// ADR-127: finds the pellets of a multi-pellet shot in the authority event stream so each can draw its own tracer.
    /// A shot is one <see cref="CombatEventKind.ShotAccepted"/> (pellet 0's muzzle ray, already drawn by
    /// <see cref="AnalyticFxPool.SpawnGunshotFeedback"/>) followed by an <see cref="CombatEventKind.Impact"/> per pellet
    /// that hit something. Every further impact from the same shooter in the same tick that does not lie on pellet 0's
    /// ray is another pellet. Single-projectile guns produce only the pellet-0 impact, so they never get an extra
    /// tracer and no weapon table is needed. Pellets that hit nothing have no impact and draw no tracer.
    /// </summary>
    public sealed class PelletTracerTracker
    {
        /// <summary>Impacts within about 0.08° of pellet 0's ray are pellet 0 itself.</summary>
        public const double SameRayCosine = 1.0 - 1e-6;

        private bool active;
        private EntityId shooter;
        private uint tick;
        private Float3 origin;
        private Float3 primaryDirection;

        /// <summary>
        /// Remembers a shot. <paramref name="drawsTracers"/> is false for shots with no tracer (the knife), which only
        /// forget the previous shot.
        /// </summary>
        public void ObserveShot(CombatEvent ev, bool drawsTracers = true)
        {
            if (ev.Kind != CombatEventKind.ShotAccepted || ev.Payload is not ShotPayload shot) return;
            active = drawsTracers;
            shooter = ev.Instigator;
            tick = ev.Id.Tick;
            origin = shot.Origin;
            primaryDirection = shot.UnitDirection;
        }

        /// <summary>True when <paramref name="ev"/> is a further pellet impact of the remembered shot.</summary>
        public bool TryPelletImpact(CombatEvent ev, out Float3 impactPoint)
        {
            impactPoint = default;
            if (!active || ev.Kind != CombatEventKind.Impact || ev.Payload is not ImpactPayload impact) return false;
            if (ev.Id.Tick != tick || !ev.Instigator.Equals(shooter)) return false;
            if (ProductAudioEventRouter.IsGrenadeBlastImpact(ev)) return false;
            double dx = impact.Position.X - origin.X, dy = impact.Position.Y - origin.Y, dz = impact.Position.Z - origin.Z;
            double length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (length < 1e-4) return false;
            double cos = (dx * primaryDirection.X + dy * primaryDirection.Y + dz * primaryDirection.Z) / length;
            if (cos >= SameRayCosine) return false;
            impactPoint = impact.Position;
            return true;
        }

        public void Clear() => active = false;
    }
}
