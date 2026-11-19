using System;
using System.Linq;
using Shooter.Contracts;
using Shooter.Simulation.Combat;
using Xunit;

namespace Shooter.Simulation.Tests.Combat
{
    public sealed class WeaponCatalogTests
    {
        [Fact]
        public void CatalogContainsClassicLoadoutAndCarbinePrimary()
        {
            Assert.True(WeaponCatalog.TryGet(new DefinitionId("weapons/rifle"), out var rifle));
            Assert.Equal(WeaponFamily.PrimaryGun, rifle.Family);
            Assert.Equal(6u, rifle.Definition.FireIntervalTicks);
            Assert.Equal(30, rifle.Definition.MagazineSize);

            Assert.True(WeaponCatalog.TryGet(new DefinitionId("weapons/rifle-carbine"), out var carbine));
            Assert.Equal(WeaponFamily.PrimaryGun, carbine.Family);
            Assert.Equal(5u, carbine.Definition.FireIntervalTicks);
            Assert.Equal(25, carbine.Definition.MagazineSize);
            Assert.Equal("weapons/rifle-carbine", carbine.VisualProfileId);

            // ADR-102: five named primaries join the two retired generic ones (kept for tests, spawn weight 0);
            // ADR-118 adds the M4A1, M24 SWS, Mk 16 and M870.
            Assert.Equal(11, WeaponCatalog.Primaries().Count());
            Assert.Equal(0, rifle.Definition.SpawnWeight);
            Assert.Equal(0, carbine.Definition.SpawnWeight);
        }

        [Fact]
        public void FromIdDelegatesToCatalogAndRejectsUnknown()
        {
            Assert.Equal(WeaponDefinition.Rifle.Id, WeaponDefinition.FromId(new DefinitionId("weapons/rifle")).Id);
            Assert.Equal(WeaponDefinition.RifleCarbine.Id, WeaponDefinition.FromId(new DefinitionId("weapons/rifle-carbine")).Id);
            Assert.Throws<ArgumentException>(() => WeaponDefinition.FromId(new DefinitionId("weapons/unknown")));
        }

        [Fact]
        public void DefaultLoadoutKeepsRiflePrimary()
        {
            var slots = WeaponLoadoutPolicy.CreateDefaultLoadout();
            Assert.Equal(4, slots.Length);
            Assert.Equal("weapons/rifle", slots[0].DefinitionId.ToString());
            Assert.Equal("weapons/pistol", slots[1].DefinitionId.ToString());
            Assert.Equal("weapons/knife", slots[2].DefinitionId.ToString());
            Assert.Equal("weapons/grenade", slots[3].DefinitionId.ToString());
            Assert.Equal(30, slots[0].AmmoInMagazine);
            Assert.Equal(90, slots[0].Reserve);
        }

        [Fact]
        public void CarbineLoadoutOccupiesSlotZeroWithoutExpandingSlots()
        {
            var slots = WeaponLoadoutPolicy.CreateLoadout(WeaponLoadoutPolicy.CarbinePrimaryId);
            Assert.Equal(4, slots.Length);
            Assert.Equal("weapons/rifle-carbine", slots[0].DefinitionId.ToString());
            Assert.Equal(25, slots[0].AmmoInMagazine);
            Assert.Equal(75, slots[0].Reserve);
            Assert.Equal("weapons/pistol", slots[1].DefinitionId.ToString());
            Assert.Throws<ArgumentException>(() => WeaponLoadoutPolicy.CreateLoadout(WeaponLoadoutPolicy.PistolId));
        }
    }
}
