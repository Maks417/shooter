#nullable enable
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Presentation.View;
using Shooter.Simulation.Combat;
using Xunit;

namespace Shooter.Simulation.Tests.Combat
{
    /// <summary>ADR-108 career buffs: integer per-mille scales on firearms only, identity bit-identical.</summary>
    public sealed class CareerBuffTests
    {
        private static readonly DefinitionId M16 = new DefinitionId("weapons/m16");
        private static readonly DefinitionId G17 = new DefinitionId("weapons/g17");
        private static readonly WeaponModifiers Top = new WeaponModifiers(850, 850, 850, 800, 2);

        [Fact]
        public void Identity_leaves_every_catalog_definition_unchanged()
        {
            foreach (var entry in WeaponCatalog.All)
            {
                var def = entry.Definition;
                Assert.True(WeaponModifiers.Identity.IsIdentity);
                Assert.Equal(def, WeaponModifiers.Identity.Apply(def));
                Assert.Equal(def.ReserveCapacity, WeaponModifiers.Identity.ReserveFor(def));
            }
            Assert.Equal(WeaponModifiers.Identity, new WeaponModifiers(1000, 1000, 1000, 1000, 0));
        }

        [Fact]
        public void Buffs_scale_recoil_reload_ads_and_equip_but_never_damage_rate_or_movement()
        {
            var def = WeaponDefinition.FromId(M16);
            var buffed = Top.Apply(def);
            Assert.Equal(WeaponModifiers.ScaleTicks(def.ReloadTicks, 850), buffed.ReloadTicks);
            Assert.Equal(122u, buffed.ReloadTicks); // 144 × 0.85 = 122.4
            Assert.Equal(19u, buffed.EquipTicks);   // 24 × 0.8 = 19.2
            Assert.Equal(def.RecoilPitchRadians * 850 / 1000f, buffed.RecoilPitchRadians);
            Assert.Equal(def.RecoilYawHalfRadians * 850 / 1000f, buffed.RecoilYawHalfRadians);
            Assert.Equal(def.AdsSpreadRadians * 850 / 1000f, buffed.AdsSpreadRadians);
            Assert.Equal(def.HipSpreadRadians, buffed.HipSpreadRadians);
            Assert.Equal(def.BaseDamage, buffed.BaseDamage);
            Assert.Equal(def.FireIntervalTicks, buffed.FireIntervalTicks);
            Assert.Equal(def.BurstIntervalTicks, buffed.BurstIntervalTicks);
            Assert.Equal(def.MoveSpeedScale, buffed.MoveSpeedScale);
            Assert.Equal(def.MagazineSize, buffed.MagazineSize);
            Assert.Equal(def.ReserveCapacity + 2 * def.MagazineSize, buffed.ReserveCapacity);
        }

        [Fact]
        public void Knife_and_grenade_are_never_buffed()
        {
            Assert.Equal(WeaponDefinition.Knife, Top.Apply(WeaponDefinition.Knife));
            Assert.Equal(WeaponDefinition.Grenade, Top.Apply(WeaponDefinition.Grenade));
            Assert.Equal(WeaponDefinition.Grenade.ReserveCapacity, Top.ReserveFor(WeaponDefinition.Grenade));
        }

        [Fact]
        public void Scales_are_clamped_to_the_caps()
        {
            var greedy = new WeaponModifiers(1, 100, 500, 10, 9);
            Assert.Equal(WeaponModifiers.MinScale, greedy.RecoilPerMille);
            Assert.Equal(WeaponModifiers.MinScale, greedy.ReloadPerMille);
            Assert.Equal(WeaponModifiers.MinScale, greedy.AdsSpreadPerMille);
            Assert.Equal(WeaponModifiers.MinEquipScale, greedy.EquipPerMille);
            Assert.Equal(WeaponModifiers.MaxExtraMagazines, greedy.ExtraMagazines);
            Assert.Equal(1u, WeaponModifiers.ScaleTicks(1, 850));
            Assert.Equal(0u, WeaponModifiers.ScaleTicks(0, 850));
        }

        [Fact]
        public void Buffed_reload_completes_sooner_in_the_weapon_step()
        {
            Assert.Equal(144u, ReloadDuration(WeaponModifiers.Identity));
            Assert.Equal(132u, ReloadDuration(new WeaponModifiers(1000, 920, 1000, 1000, 0)));
            Assert.Equal(122u, ReloadDuration(Top));
        }

        [Fact]
        public void Buffed_loadout_carries_spare_magazines_for_both_guns()
        {
            var slots = WeaponLoadoutPolicy.CreateLoadout(M16, G17, Top);
            Assert.Equal(90 + 60, slots[0].Reserve);
            Assert.Equal(51 + 34, slots[1].Reserve);
            var plain = WeaponLoadoutPolicy.CreateLoadout(M16, G17);
            Assert.Equal(90, plain[0].Reserve);
            Assert.Equal(plain[2], slots[2]);
            Assert.Equal(plain[3], slots[3]);
        }

        [Fact]
        public void Loadout_with_modifiers_keeps_guns_and_compares_by_value()
        {
            var plain = PlayerLoadout.Create(M16, SightCatalog.Irons, G17);
            var buffed = plain.WithModifiers(Top);
            Assert.Equal(plain.Primary, buffed.Primary);
            Assert.Equal(SightCatalog.Irons, buffed.PrimarySight);
            Assert.NotEqual(plain, buffed);
            Assert.Equal(buffed, PlayerLoadout.Create(M16, SightCatalog.Irons, G17).WithModifiers(Top));
            Assert.True(plain.Modifiers.IsIdentity);
        }

        [Fact]
        public void Presentation_handling_mirrors_the_simulation_rounding()
        {
            for (uint ticks = 0; ticks < 400; ticks++)
                foreach (ushort pm in new ushort[] { 800, 850, 920, 1000 })
                    Assert.Equal(WeaponModifiers.ScaleTicks(ticks, pm), WeaponHandlingScale.ScaleTicks(ticks, pm));
            Assert.True(WeaponVisualProfiles.TryGet(M16, out var profile));
            var handled = profile.WithHandling(new WeaponHandlingScale(850, 850, 800));
            var def = Top.Apply(WeaponDefinition.FromId(M16));
            Assert.Equal(def.ReloadTicks, handled.ReloadTicks);
            Assert.Equal(def.EquipTicks, handled.EquipTicks);
            Assert.Equal(profile.RecoilPitchDegrees * .85f, handled.RecoilPitchDegrees, 4);
            Assert.True(WeaponVisualProfiles.TryGet(new DefinitionId("weapons/knife"), out var knife));
            Assert.Equal(knife.EquipTicks, knife.WithHandling(new WeaponHandlingScale(850, 850, 800)).EquipTicks);
        }

        /// <summary>Ticks from a reload press on a part-empty M16 until the magazine is full again.</summary>
        private static uint ReloadDuration(WeaponModifiers modifiers)
        {
            var sim = new WeaponSimulator();
            var slots = WeaponLoadoutPolicy.CreateLoadout(M16, G17, modifiers);
            var w = slots[0];
            slots[0] = new WeaponState(w.DefinitionId, WeaponAction.Ready, 10, w.Reserve, 0, 0, 0, 0, false);
            byte active = 0, desired = 0;
            var shots = new List<AcceptedShot>(); var grenades = new List<AcceptedGrenade>(); var events = new List<CombatEvent>();
            var next = new EventId(1, 0, new EntityId(1), 0);
            for (uint tick = 0; tick < 400; tick++)
            {
                var buttons = tick == 0 ? InputButtons.ReloadPressed : InputButtons.None;
                sim.Step(tick, buttons, 0, true, false, slots, ref active, ref desired, shots, grenades, events, ref next, new EntityId(1), modifiers);
                if (tick > 0 && slots[0].AmmoInMagazine == 30) return tick;
            }
            return uint.MaxValue;
        }
    }
}
