#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Game.Session;
using Shooter.Generation.Environment;

namespace Shooter.Presentation.Session
{
    /// <summary>Pure verified-byte read for the menu's cosmetic stage; never starts a gameplay session.</summary>
    public interface IProductMenuVisualSource
    {
        bool TryLoadMenuVisuals(MapId map, out PackagedVisualPayloads visuals, out string diagnostic);
    }

    /// <summary>Bootstrap-owned services the product shell needs without referencing FoundationEntry.</summary>
    public interface IProductShellBackend
    {
        /// <summary>T195: the match through its shell-facing port; Presentation never sees the concrete session.</summary>
        IGameSessionControl Session { get; }
        IArmoryCatalogView Armory { get; }
        IMapCatalog Catalog { get; }
        GameOperationResult Pause();
        GameOperationResult Resume();
        void WireMapActors();
        bool TryGetVerifiedMap(out IReadOnlyList<CollisionBox> boxes, out byte[]? metadataBytes, out string mapId);
        bool TryGetVerifiedVisuals(out PackagedVisualPayloads visuals, out string diagnostic);
        void InstallHumanSampler(Func<uint, LocalPlayerCommand?> sampler);
    }
}
