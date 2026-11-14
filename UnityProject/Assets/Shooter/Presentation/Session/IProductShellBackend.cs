#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Game.Session;
using Shooter.Generation.Environment;

namespace Shooter.Presentation.Session
{
    /// <summary>Bootstrap-owned services the product shell needs without referencing FoundationEntry.</summary>
    public interface IProductShellBackend
    {
        OfflineGameSession Session { get; }
        IMapCatalog Catalog { get; }
        GameOperationResult Pause();
        GameOperationResult Resume();
        void WireMapActors();
        bool TryGetVerifiedMap(out IReadOnlyList<CollisionBox> boxes, out byte[]? metadataBytes, out string mapId);
        bool TryGetVerifiedVisuals(out PackagedVisualPayloads visuals, out string diagnostic);
        void InstallHumanSampler(Func<uint, InputCommand?> sampler);
    }
}
