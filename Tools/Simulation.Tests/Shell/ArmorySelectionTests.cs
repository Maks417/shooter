#nullable enable
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Presentation.Audio;
using Shooter.Presentation.Menus;
using Shooter.Presentation.View;
using Xunit;

namespace Shooter.Simulation.Tests.Shell
{
    /// <summary>ADR-107 Armory page model, navigation and ADS presentation through a mounted sight.</summary>
    public sealed class ArmorySelectionTests
    {
        private static readonly DefinitionId Ak47 = new DefinitionId("weapons/ak47");
        private static readonly DefinitionId M16 = new DefinitionId("weapons/m16");
        private static readonly DefinitionId Mk14 = new DefinitionId("weapons/mk14");
        private static readonly DefinitionId G17 = new DefinitionId("weapons/g17");
        private static readonly DefinitionId Eagle = new DefinitionId("weapons/golden-eagle");
        private static readonly DefinitionId Collimator = new DefinitionId("sights/collimator");
        private static readonly DefinitionId Prism = new DefinitionId("sights/scope2");
        private static readonly DefinitionId Scope4 = new DefinitionId("sights/scope4");
        private static readonly DefinitionId Scope8 = new DefinitionId("sights/scope8");

        private static ArmorySelection Create(string? primary = null, string? sidearm = null, IReadOnlyDictionary<string, string>? sights = null) =>
            new ArmorySelection(new[] { Ak47, M16, Mk14 }, new[] { G17, Eagle }, primary, sidearm, sights);

        [Fact]
        public void Stale_or_unknown_ids_fall_back_to_the_first_offered_gun_and_factory_sight()
        {
            var armory = Create("weapons/rifle", "weapons/m16", new Dictionary<string, string>
            {
                ["weapons/ak47"] = "sights/scope8", ["weapons/zzz"] = "sights/collimator", ["NOT AN ID"] = "x"
            });
            Assert.Equal(Ak47, armory.Primary);
            Assert.Equal(G17, armory.Sidearm);
            Assert.Equal(new DefinitionId("sights/irons"), armory.PrimarySight);
            Assert.Empty(armory.SightChoices);
        }

        [Fact]
        public void Each_primary_remembers_its_own_sight()
        {
            var armory = Create("weapons/m16", "weapons/g17");
            Assert.Equal(Prism, armory.PrimarySight);
            int revision = armory.Revision;
            Assert.True(armory.SelectSight(Collimator));
            Assert.True(armory.Revision > revision);
            Assert.True(armory.SelectWeapon(Mk14));
            Assert.Equal(Scope4, armory.PrimarySight);
            Assert.False(armory.SelectSight(Scope8));
            Assert.True(armory.SelectWeapon(M16));
            Assert.Equal(Collimator, armory.PrimarySight);
            Assert.Equal("sights/collimator", armory.SightChoices["weapons/m16"]);
        }

        [Fact]
        public void Selecting_a_sidearm_switches_slot_and_offers_no_sights()
        {
            var armory = Create();
            Assert.True(armory.SelectWeapon(Eagle));
            Assert.Equal(ArmorySlot.Sidearm, armory.Slot);
            Assert.Equal(Eagle, armory.Sidearm);
            Assert.Empty(armory.SightsFor(Eagle));
            Assert.False(armory.SightFor(Eagle).IsValid);
            Assert.False(armory.SelectWeapon(new DefinitionId("weapons/knife")));
        }

        [Fact]
        public void Sight_lists_follow_the_weapon_definitions()
        {
            var armory = Create();
            Assert.Equal(new[] { "irons", "collimator", "scope2" }, Names(armory.SightsFor(Ak47)));
            Assert.Equal(new[] { "irons", "collimator", "scope2", "scope4" }, Names(armory.SightsFor(Mk14)));
        }

        [Fact]
        public void Summary_names_gun_sight_and_sidearm()
        {
            var armory = Create("weapons/ak47", "weapons/golden-eagle", new Dictionary<string, string> { ["weapons/ak47"] = "sights/collimator" });
            Assert.Equal("AK-47 · COLLIMATOR / GOLDEN EAGLE .50", armory.Summary);
        }

        [Fact]
        public void Armory_opens_from_main_or_setup_and_returns_there()
        {
            var nav = new ShellNavigation();
            nav.OpenArmory();
            Assert.Equal(ShellScreen.Armory, nav.Screen);
            Assert.True(nav.OwnsMenuFocus);
            nav.CloseArmory();
            Assert.Equal(ShellScreen.Main, nav.Screen);
            nav.OpenSetup();
            nav.OpenArmory();
            nav.CloseArmory();
            Assert.Equal(ShellScreen.Setup, nav.Screen);
            Assert.True(ShellMotion.SlidesOnEnter(ShellScreen.Armory));
            Assert.Equal(MusicContext.Menu, MusicDirector.ContextFor(ShellScreen.Armory, ShellScreen.Main));
        }

        [Fact]
        public void Mounted_sight_sets_optic_mode_and_ads_field_of_view()
        {
            var m16 = WeaponVisualProfiles.Require(M16);
            Assert.Equal(WeaponOpticMode.ScopedPip, m16.OpticMode);
            var dot = WeaponSightChoices.Apply(m16, Collimator);
            Assert.Equal(WeaponOpticMode.Collimator, dot.OpticMode);
            Assert.False(AdsOpticVisual.KeepsHipCameraFov(dot));
            Assert.False(AdsOpticVisual.HideRifleViewModel(dot, 1f));
            Assert.False(AdsOpticVisual.UsesLensCamera(dot, 1f, false));
            Assert.Equal(0f, AdsOpticVisual.CollimatorDotOpacity(dot, 0.3f));
            Assert.True(AdsOpticVisual.CollimatorDotOpacity(dot, 1f) > 0.99f);
            Assert.Equal(80f / 1.2f, AdsOpticVisual.AdsFieldOfViewDegrees(80f, dot), 3);
            Assert.True(WeaponSightChoices.TryGetSight(Collimator, out var collimatorSight));
            Assert.Equal(1.2f, collimatorSight.Magnification, 3);
            // ADR-117: one small dot, no bloom - about 3 px at 1080p.
            Assert.InRange(AdsOpticVisual.CollimatorDotRadiusPixels(1080f), 1.5f, 3.5f);
            var ak = WeaponSightChoices.Apply(WeaponVisualProfiles.Require(Ak47), Prism);
            Assert.Equal(WeaponOpticMode.ScopedPip, ak.OpticMode);
            Assert.Equal(40f, AdsOpticVisual.AdsFieldOfViewDegrees(80f, ak), 3);
            // A sight the gun cannot take presents as its factory sight.
            Assert.Equal(WeaponOpticMode.Irons, WeaponSightChoices.Apply(WeaponVisualProfiles.Require(Ak47), Scope8).OpticMode);
            // Guns without sight choices are untouched.
            Assert.Equal(WeaponVisualProfiles.Require(G17).OpticMode, WeaponSightChoices.Apply(WeaponVisualProfiles.Require(G17), Collimator).OpticMode);
        }

        [Fact]
        public void Sight_mesh_keys_follow_the_mount_and_ak_irons_have_none()
        {
            Assert.True(WeaponSightChoices.TryGet(Ak47, out var ak));
            Assert.Null(ak.MeshKeyFor(new DefinitionId("sights/irons")));
            Assert.Equal("sight-collimator-ak", ak.MeshKeyFor(Collimator));
            Assert.True(WeaponSightChoices.TryGet(Mk14, out var mk));
            Assert.Equal("sight-scope4-dmr", mk.MeshKeyFor(Scope4));
            Assert.Null(mk.MeshKeyFor(Scope8));
        }

        private static string[] Names(IReadOnlyList<SightVisualProfile> sights)
        {
            var names = new string[sights.Count];
            for (int i = 0; i < names.Length; i++) names[i] = sights[i].Name;
            return names;
        }
    }
}
