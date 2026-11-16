#nullable enable
using System;
using System.Linq;
using Shooter.Contracts;
using Shooter.Generation.Environment;
using Shooter.Presentation.Audio;
using Shooter.Presentation.Hud;
using Shooter.Presentation.View;
using Shooter.Simulation.Combat;
using Xunit;

namespace Shooter.Simulation.Tests.Combat
{
    /// <summary>
    /// ADR-077: both catalogs are generated from Content/Weapons/*.weapon.json. These checks keep the two generated
    /// halves consistent with each other, with packaged meshes, and with the pre-ADR-077 hand-written values.
    /// </summary>
    public sealed class WeaponDefinitionFileTests
    {
        private static float Deg(float degrees) => degrees * (float)Math.PI / 180f;

        [Fact]
        public void CombatAndVisualCatalogsListTheSameWeaponsInTheSameOrder()
        {
            var combat = WeaponCatalog.All.Select(e => e.Definition.Id.ToString()).ToArray();
            var visual = WeaponVisualProfiles.All.Select(p => p.DefinitionId.ToString()).ToArray();
            Assert.Equal(combat, visual);
            foreach (var entry in WeaponCatalog.All)
            {
                var profile = WeaponVisualProfiles.Require(entry.Definition.Id);
                Assert.Equal(entry.Family.ToString(), profile.Family.ToString());
                Assert.Equal(entry.Definition.Id.ToString(), entry.VisualProfileId);
            }
        }

        [Fact]
        public void EveryProfileResolvesToPackagedWeaponMeshes()
        {
            foreach (var profile in WeaponVisualProfiles.All)
            {
                Assert.NotEmpty(MapVisualClosure.LodPathsForMeshKey(profile.MeshAssetKey));
                if (profile.HasMagazineCycle)
                {
                    Assert.NotNull(MapVisualClosure.MagazinePathForMeshKey(profile.MeshAssetKey));
                    Assert.NotNull(MapVisualClosure.CyclePathForMeshKey(profile.MeshAssetKey));
                }
            }
        }

        [Fact]
        public void GeneratedCombatRowsMatchThePreGeneratorValuesBitForBit()
        {
            AssertSame(new WeaponDefinition(
                new DefinitionId("weapons/rifle"), FireMode.Automatic, 6, 144, 24, 30, 90, 30,
                Deg(1.0f), Deg(0.15f), Deg(0.35f), Deg(0.1f)));
            AssertSame(new WeaponDefinition(
                new DefinitionId("weapons/rifle-carbine"), FireMode.Automatic, 5, 132, 22, 25, 75, 28,
                Deg(1.15f), Deg(0.18f), Deg(0.40f), Deg(0.12f)));
            AssertSame(new WeaponDefinition(
                new DefinitionId("weapons/pistol"), FireMode.SemiAutomatic, 10, 108, 18, 15, 45, 25,
                Deg(1.5f), Deg(0.3f), Deg(0.55f), Deg(0.15f)));
            AssertSame(new WeaponDefinition(
                new DefinitionId("weapons/knife"), FireMode.Melee, 24, 0, 12, 1, 0, 55,
                0f, 0f, 0f, 0f, maxRangeMeters: 1.8f, consumesAmmo: false, allowsSprintFire: true));
            AssertSame(new WeaponDefinition(
                new DefinitionId("weapons/grenade"), FireMode.Throwable, 0, 0, 18, 1, 0, 100,
                0f, 0f, 0f, 0f, maxRangeMeters: 0f, fuseTicks: 120, throwSpeedMetersPerSecond: 18f,
                blastRadiusMeters: 5f, consumesAmmo: true, allowsSprintFire: false));
        }

        [Fact]
        public void ShotVoiceAndKillFeedGlyphComeFromTheProfile()
        {
            Assert.Equal(SynthVoiceKind.RifleShot, ShotVoice("weapons/rifle"));
            Assert.Equal(SynthVoiceKind.RifleShot, ShotVoice("weapons/rifle-carbine"));
            Assert.Equal(SynthVoiceKind.PistolShot, ShotVoice("weapons/pistol"));
            Assert.Equal(SynthVoiceKind.KnifeSwing, ShotVoice("weapons/knife"));
            Assert.Equal(SynthVoiceKind.GrenadeThrow, ShotVoice("weapons/grenade"));
            Assert.Equal(SynthVoiceKind.RifleShot, ShotVoice("weapons/unknown-gun"));

            foreach (var profile in WeaponVisualProfiles.All)
            {
                Assert.Equal(profile.ShotVoice, ShotVoice(profile.DefinitionId.ToString()));
                Assert.Equal(profile.KillFeedGlyph, KillFeedCause.MapWeapon(profile.DefinitionId));
            }
            Assert.Equal(KillFeedWeapon.Carbine, KillFeedCause.MapWeapon(new DefinitionId("weapons/rifle-carbine")));
        }

        private static SynthVoiceKind ShotVoice(string weaponId) =>
            ProductAudioEventRouter.MapShotVoice(new CombatEvent(
                new EventId(1, 10, new EntityId(1), 1), CombatEventKind.ShotAccepted, new EntityId(1), default,
                new ShotPayload(new DefinitionId(weaponId), 1, new Float3(0, 1, 0), new Float3(0, 0, 1), 1, 1)));

        private static void AssertSame(WeaponDefinition expected)
        {
            var actual = WeaponCatalog.RequireDefinition(expected.Id);
            Assert.Equal(expected.FireMode, actual.FireMode);
            Assert.Equal(expected.FireIntervalTicks, actual.FireIntervalTicks);
            Assert.Equal(expected.ReloadTicks, actual.ReloadTicks);
            Assert.Equal(expected.EquipTicks, actual.EquipTicks);
            Assert.Equal(expected.MagazineSize, actual.MagazineSize);
            Assert.Equal(expected.ReserveCapacity, actual.ReserveCapacity);
            Assert.Equal(expected.BaseDamage, actual.BaseDamage);
            Assert.Equal(Bits(expected.HipSpreadRadians), Bits(actual.HipSpreadRadians));
            Assert.Equal(Bits(expected.AdsSpreadRadians), Bits(actual.AdsSpreadRadians));
            Assert.Equal(Bits(expected.RecoilPitchRadians), Bits(actual.RecoilPitchRadians));
            Assert.Equal(Bits(expected.RecoilYawHalfRadians), Bits(actual.RecoilYawHalfRadians));
            Assert.Equal(Bits(expected.MaxRangeMeters), Bits(actual.MaxRangeMeters));
            Assert.Equal(expected.FuseTicks, actual.FuseTicks);
            Assert.Equal(Bits(expected.ThrowSpeedMetersPerSecond), Bits(actual.ThrowSpeedMetersPerSecond));
            Assert.Equal(Bits(expected.BlastRadiusMeters), Bits(actual.BlastRadiusMeters));
            Assert.Equal(expected.ConsumesAmmo, actual.ConsumesAmmo);
            Assert.Equal(expected.AllowsSprintFire, actual.AllowsSprintFire);
        }

        private static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
    }
}
