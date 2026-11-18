#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shooter.Contracts;
using Shooter.Game.Contracts;

namespace Shooter.World.Runtime
{
    /// <summary>Test-only map runtime. Shipping Bootstrap must reject this adapter.</summary>
    public sealed class FakeMapRuntime : IMapRuntime, IMapCoverSource
    {
        private MapGameplayDefinition? loaded;
        private readonly IWorldQueries world;
        private readonly INavigation navigation;
        public bool RejectAllSpawns { get; set; }
        /// <summary>ADR-112: authored cover a test map exposes to the bots.</summary>
        public List<MapCoverProxy> CoverProxies { get; } = new List<MapCoverProxy>();
        public IReadOnlyList<MapCoverProxy> Covers => CoverProxies;

        public FakeMapRuntime(IWorldQueries world, INavigation navigation)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        }

        public IWorldQueries WorldQueries => world;
        public INavigation Navigation => navigation;

        public Task<GameOperationResult> LoadAsync(MapGameplayDefinition definition, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            loaded = definition ?? throw new ArgumentNullException(nameof(definition));
            return Task.FromResult(GameOperationResult.Ok);
        }

        public bool TryValidateSpawn(SpawnSlot slot, out string diagnostic)
        {
            if (RejectAllSpawns)
            {
                diagnostic = "reject-all";
                return false;
            }
            if (loaded == null)
            {
                diagnostic = "not-loaded";
                return false;
            }
            diagnostic = "ok";
            return true;
        }

        public GameOperationResult Unload()
        {
            loaded = null;
            return GameOperationResult.Ok;
        }
    }
}
