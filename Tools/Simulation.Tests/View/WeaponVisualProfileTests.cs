using Shooter.Contracts;
using Shooter.Presentation.View;
using Xunit;

namespace Shooter.Simulation.Tests.View
{
    public sealed class WeaponVisualProfileTests
    {
        [Fact]
        public void ProfilesKeyOpticsAndMeshesByDefinitionId()
        {
            var rifle = WeaponVisualProfiles.Require(new DefinitionId("weapons/rifle"));
            var carbine = WeaponVisualProfiles.Require(new DefinitionId("weapons/rifle-carbine"));
            var pistol = WeaponVisualProfiles.Require(new DefinitionId("weapons/pistol"));

            Assert.Equal(WeaponOpticMode.ScopedPip, rifle.OpticMode);
            Assert.Equal(WeaponOpticMode.ScopedPip, carbine.OpticMode);
            Assert.Equal(WeaponOpticMode.Irons, pistol.OpticMode);
            Assert.Equal(WeaponVisualProfiles.MeshKeyRifleCarbine, carbine.MeshAssetKey);
            Assert.True(AdsOpticVisual.UsesScopedOptic(carbine));
            Assert.True(AdsOpticVisual.HideRifleViewModel(carbine, 1f));
            Assert.False(AdsOpticVisual.UsesScopedOptic(pistol));
            Assert.Equal(WeaponVisualProfiles.MeshKeyRifleCarbine,
                WeaponVisualProfiles.Resolve(new DefinitionId("weapons/rifle-carbine"), 0).MeshAssetKey);
        }

        [Fact]
        public void SlotFallbackStillResolvesLegacyDefaults()
        {
            Assert.Equal("weapons/rifle", WeaponVisualProfiles.Resolve(default, 0).DefinitionId.ToString());
            Assert.Equal("weapons/pistol", WeaponVisualProfiles.Resolve(default, 1).DefinitionId.ToString());
            Assert.True(FirstPersonWeaponMechanics.HasCyclePart(WeaponVisualProfiles.Resolve(new DefinitionId("weapons/rifle-carbine"), 0)));
        }
    }
}
