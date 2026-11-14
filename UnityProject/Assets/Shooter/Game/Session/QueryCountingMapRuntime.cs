#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Shooter.Contracts;
using Shooter.Game.Contracts;

namespace Shooter.Game.Session
{
    /// <summary>IMapRuntime decorator exposing CountingWorldQueries for spawn/combat/AI world samples.</summary>
    public sealed class QueryCountingMapRuntime : IMapRuntime
    {
        private readonly IMapRuntime inner;
        private readonly CountingWorldQueries counting;

        public QueryCountingMapRuntime(IMapRuntime inner)
            : this(inner, new CountingWorldQueries((inner ?? throw new ArgumentNullException(nameof(inner))).WorldQueries))
        {
        }

        public QueryCountingMapRuntime(IMapRuntime inner, CountingWorldQueries sharedCounting)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            counting = sharedCounting ?? throw new ArgumentNullException(nameof(sharedCounting));
        }

        public CountingWorldQueries CountingQueries => counting;
        public IWorldQueries WorldQueries => counting;
        public INavigation Navigation => inner.Navigation;

        public Task<GameOperationResult> LoadAsync(MapGameplayDefinition definition, CancellationToken cancel) =>
            inner.LoadAsync(definition, cancel);

        public bool TryValidateSpawn(SpawnSlot slot, out string diagnostic) =>
            inner.TryValidateSpawn(slot, out diagnostic);

        public GameOperationResult Unload() => inner.Unload();
    }
}
