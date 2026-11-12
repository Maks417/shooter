#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Generation
{
    /// <summary>Bounded deterministic preflight, not a compiler or cache. Returns dependencies before consumers.</summary>
    public static class DependencyOrder
    {
        public const int MaxNodes = 4096;
        public static IReadOnlyList<ContentId> Sort(IReadOnlyDictionary<ContentId, IReadOnlyList<ContentId>> graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (graph.Count > MaxNodes) throw new ArgumentException("Dependency graph exceeds node limit.");
            var remaining = new Dictionary<ContentId, int>();
            var consumers = new Dictionary<ContentId, List<ContentId>>();
            foreach (var pair in graph)
            {
                if (pair.Value == null || pair.Value.Count > 256) throw new ArgumentException("Invalid dependency count.");
                remaining.Add(pair.Key, pair.Value.Count);
                consumers.Add(pair.Key, new List<ContentId>());
            }
            foreach (var pair in graph)
            {
                var unique = new HashSet<ContentId>();
                foreach (var dependency in pair.Value)
                {
                    if (!unique.Add(dependency)) throw new ArgumentException("Duplicate dependency.");
                    if (!consumers.TryGetValue(dependency, out var list)) throw new ArgumentException("Missing dependency: " + dependency);
                    list.Add(pair.Key);
                }
            }
            var ready = new SortedSet<ContentId>();
            foreach (var pair in remaining) if (pair.Value == 0) ready.Add(pair.Key);
            var result = new List<ContentId>(graph.Count);
            while (ready.Count != 0)
            {
                ContentId next = ready.Min;
                ready.Remove(next);
                result.Add(next);
                foreach (var consumer in consumers[next])
                    if (--remaining[consumer] == 0) ready.Add(consumer);
            }
            if (result.Count != graph.Count) throw new ArgumentException("Dependency cycle detected.");
            return result.AsReadOnly();
        }
    }
}
