#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>Dispatches registered map ids (<see cref="MapRegistry"/>) to their layout document or code planner.</summary>
    public static class ProductMapPlanner
    {
        /// <summary>Plan a registered map with its layout document or named code planner (ADR-083).</summary>
        public static CompoundPlan Generate(string mapId)
        {
            if (mapId == null) throw new ArgumentNullException(nameof(mapId));
            return MapRegistry.Require(mapId).Generate();
        }

        /// <summary>Validates an arena plan with <see cref="ArenaRules"/> and <see cref="MapTopologyValidation"/>; throws on failure.</summary>
        public static void ValidateOrThrow(string mapId, CompoundPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var rules = ArenaRules.Validate(plan);
            if (!rules.Passed) throw new InvalidOperationException("Arena rules failed for " + mapId + ": " + rules.Diagnostic);
            var topology = MapTopologyValidation.Validate(mapId, plan);
            if (!topology.Passed) throw new InvalidOperationException("Map topology failed FIX07 checks: " + topology.Diagnostic);
        }

        /// <summary>Combat destinations for AI/catalog consumers: enemy anchor, own anchor, centre, then lane points.</summary>
        public static IReadOnlyList<Float3> CombatDestinations(CompoundPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var list = new List<Float3> { plan.ObjectiveCenter, plan.PlayerSpawn };
            if (plan.Arena == null)
            {
                list.Add(new Float3(0f, 0f, 0f));
                list.Add(new Float3(-96f, 0f, 0f));
                list.Add(new Float3(96f, 0f, 0f));
                list.Add(new Float3(112f, 0f, 0f));
                return list;
            }
            list.Add(plan.Arena.Center);
            var lanes = plan.Arena.LanePoints;
            for (int i = 0; i < lanes.Count; i++)
            {
                bool dup = false;
                for (int j = 0; j < list.Count; j++)
                    if (Math.Abs(list[j].X - lanes[i].X) < 0.01f && Math.Abs(list[j].Z - lanes[i].Z) < 0.01f) dup = true;
                if (!dup) list.Add(lanes[i]);
            }
            return list;
        }
    }
}
