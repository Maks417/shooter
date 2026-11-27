#nullable enable
using System.Collections.Generic;
using System.Linq;
using Shooter.Contracts;
using Shooter.Presentation.View;
using Shooter.Simulation.Combat;
using Xunit;

namespace Shooter.Simulation.Tests.Combat
{
    /// <summary>ADR-118 M870 shell-by-shell reload and pump handling; ADR-119 hip, pellet, cycle and move perks.</summary>
    public sealed class ShotgunAndPerkTests
    {
        private static readonly DefinitionId M870 = new DefinitionId("weapons/m870");
        private static readonly DefinitionId M24 = new DefinitionId("weapons/m24");
        private static readonly DefinitionId M16 = new DefinitionId("weapons/m16");
        private static readonly DefinitionId G17 = new DefinitionId("weapons/g17");

        private sealed class Rig
        {
            public readonly WeaponSimulator Sim = new WeaponSimulator();
            public readonly WeaponState[] Slots;
            public byte Active, Desired;
            public readonly List<AcceptedShot> Shots = new List<AcceptedShot>();
            public readonly List<AcceptedGrenade> Grenades = new List<AcceptedGrenade>();
            public readonly List<CombatEvent> Events = new List<CombatEvent>();
            public EventId Next = new EventId(1, 0, new EntityId(1), 0);
            public readonly WeaponModifiers Modifiers;

            public Rig(DefinitionId primary, WeaponModifiers modifiers = default)
            {
                Modifiers = modifiers;
                Slots = WeaponLoadoutPolicy.CreateLoadout(primary, G17, modifiers);
            }

            public void Step(uint tick, InputButtons buttons) =>
                Sim.Step(tick, buttons, 0, true, false, Slots, ref Active, ref Desired, Shots, Grenades, Events, ref Next, new EntityId(1), Modifiers);
        }

        [Fact]
        public void M870_is_a_tube_fed_eight_pellet_pump_gun()
        {
            var def = WeaponDefinition.FromId(M870);
            Assert.Equal(FireMode.SemiAutomatic, def.FireMode);
            Assert.Equal(8, def.PelletCount);
            Assert.True(def.PelletSpreadRadians > 0f);
            Assert.True(def.ReloadPerRound);
            Assert.True(WeaponModifiers.IsManualAction(def));
            Assert.True(WeaponModifiers.IsManualAction(WeaponDefinition.FromId(M24)));
            Assert.False(WeaponModifiers.IsManualAction(WeaponDefinition.FromId(M16)));
            Assert.True(WeaponVisualProfiles.TryGet(M870, out var profile));
            Assert.Equal(WeaponHandlingStyle.Pump, profile.HandlingStyle);
        }

        [Fact]
        public void Shell_reload_loads_one_round_per_phase_until_full()
        {
            var rig = new Rig(M870);
            var def = WeaponDefinition.FromId(M870);
            var w = rig.Slots[0];
            rig.Slots[0] = new WeaponState(w.DefinitionId, WeaponAction.Ready, 4, w.Reserve, 0, 0, 0, 0, false);
            uint full = 0;
            for (uint tick = 0; tick < 400 && full == 0; tick++)
            {
                rig.Step(tick, tick == 0 ? InputButtons.ReloadPressed : InputButtons.None);
                if (tick > 0 && rig.Slots[0].AmmoInMagazine == def.MagazineSize && rig.Slots[0].Action == WeaponAction.Ready) full = tick;
            }
            Assert.Equal(3u * def.ReloadTicks, full);
            Assert.Equal(3, rig.Events.Count(e => e.Kind == CombatEventKind.ReloadStarted));
            Assert.Equal(3, rig.Events.Count(e => e.Kind == CombatEventKind.ReloadCompleted));
            Assert.Equal(def.ReserveCapacity - 3, rig.Slots[0].Reserve);
        }

        [Fact]
        public void Trigger_press_interrupts_a_shell_reload_and_fires_the_same_tick()
        {
            var rig = new Rig(M870);
            var def = WeaponDefinition.FromId(M870);
            var w = rig.Slots[0];
            rig.Slots[0] = new WeaponState(w.DefinitionId, WeaponAction.Ready, 1, w.Reserve, 0, 0, 0, 0, false);
            rig.Step(0, InputButtons.ReloadPressed);
            for (uint tick = 1; tick <= def.ReloadTicks + 5; tick++) rig.Step(tick, InputButtons.None);
            Assert.Equal(WeaponAction.Reloading, rig.Slots[0].Action);
            Assert.Equal(2, rig.Slots[0].AmmoInMagazine);
            uint press = def.ReloadTicks + 6;
            rig.Step(press, InputButtons.FirePressed | InputButtons.FireHeld);
            Assert.Single(rig.Shots);
            Assert.Equal(1, rig.Slots[0].AmmoInMagazine);
            Assert.Equal(WeaponAction.FiringRecovery, rig.Slots[0].Action);
        }

        [Fact]
        public void Empty_tube_reloads_without_a_press_and_a_press_with_nothing_loaded_keeps_loading()
        {
            var rig = new Rig(M870);
            var def = WeaponDefinition.FromId(M870);
            var w = rig.Slots[0];
            rig.Slots[0] = new WeaponState(w.DefinitionId, WeaponAction.Ready, 0, w.Reserve, 0, 0, 0, 0, false);
            rig.Step(0, InputButtons.None);
            Assert.Equal(WeaponAction.Reloading, rig.Slots[0].Action);
            rig.Step(1, InputButtons.FirePressed | InputButtons.FireHeld);
            Assert.Empty(rig.Shots);
            Assert.Equal(WeaponAction.Reloading, rig.Slots[0].Action);
        }

        [Fact]
        public void Slick_action_speeds_only_bolt_and_pump_guns()
        {
            var slick = new WeaponModifiers(1000, 1000, 1000, 1000, 0, 1000, 1000, 850, 1000);
            Assert.Equal(WeaponModifiers.ScaleTicks(60, 850), slick.Apply(WeaponDefinition.FromId(M24)).FireIntervalTicks);
            Assert.Equal(31u, slick.Apply(WeaponDefinition.FromId(M870)).FireIntervalTicks); // 36 × 0.85 = 30.6
            var m16 = WeaponDefinition.FromId(M16);
            Assert.Equal(m16.FireIntervalTicks, slick.Apply(m16).FireIntervalTicks);
            Assert.Equal(m16.BurstIntervalTicks, slick.Apply(m16).BurstIntervalTicks);
            // The presentation mirror times the bolt and pump the same way.
            Assert.True(WeaponVisualProfiles.TryGet(M870, out var profile));
            Assert.Equal(31u, profile.WithHandling(new WeaponHandlingScale(1000, 1000, 1000, 850)).FireIntervalTicks);
            Assert.True(WeaponVisualProfiles.TryGet(M16, out var rifle));
            Assert.Equal(rifle.FireIntervalTicks, rifle.WithHandling(new WeaponHandlingScale(1000, 1000, 1000, 850)).FireIntervalTicks);
        }

        [Fact]
        public void Hip_choke_and_light_load_scale_their_fields_and_respect_the_caps()
        {
            var perks = new WeaponModifiers(1000, 1000, 1000, 1000, 0, 850, 850, 1000, 1030);
            var shotgun = WeaponDefinition.FromId(M870);
            var buffed = perks.Apply(shotgun);
            Assert.Equal(shotgun.HipSpreadRadians * 850 / 1000f, buffed.HipSpreadRadians);
            Assert.Equal(shotgun.PelletSpreadRadians * 850 / 1000f, buffed.PelletSpreadRadians);
            Assert.Equal(shotgun.MoveSpeedScale * 1030 / 1000f, buffed.MoveSpeedScale);
            Assert.Equal(shotgun.BaseDamage, buffed.BaseDamage);
            Assert.Equal(shotgun.AdsSpreadRadians, buffed.AdsSpreadRadians);
            // Caps: spreads at most -15 %, move speed at most +5 %, and nothing below one counts as a move buff.
            var wild = new WeaponModifiers(1000, 1000, 1000, 1000, 0, 100, 100, 100, 2000);
            Assert.Equal(WeaponModifiers.MinScale, wild.HipSpreadPerMille);
            Assert.Equal(WeaponModifiers.MinScale, wild.PelletSpreadPerMille);
            Assert.Equal(WeaponModifiers.MinScale, wild.ManualCyclePerMille);
            Assert.Equal(WeaponModifiers.MaxMoveScale, wild.MoveSpeedPerMille);
            Assert.Equal(1000, new WeaponModifiers(1000, 1000, 1000, 1000, 0, 1000, 1000, 1000, 900).MoveSpeedPerMille);
            Assert.True(new WeaponModifiers(1000, 1000, 1000, 1000, 0, 1000, 1000, 1000, 1000).IsIdentity);
            Assert.NotEqual(WeaponModifiers.Identity, perks);
        }

        /// <summary>One M870 blast from the origin at a soldier standing <paramref name="meters"/> ahead, aimed at the torso.</summary>
        private static (List<CombatEvent> events, EntityId target) BlastAt(float meters, float pitch)
        {
            var sim = new CombatHarnessSimulation(new Shooter.Simulation.Tests.Motor.StaticPlaneWorld());
            var shooter = sim.SpawnActor(default, 0, 1);
            var target = sim.SpawnActor(new Float3(0, 0, meters), 3.1f, 2);
            Assert.True(sim.ReplacePrimary(shooter, M870, 7, 28));
            var events = new List<CombatEvent>();
            for (uint t = 0; t < 120; t++)
            {
                var buttons = t == 60 ? InputButtons.FireHeld | InputButtons.FirePressed : InputButtons.None;
                sim.Step(t, new[]
                {
                    new InputCommand(shooter, t, t + 1, 0, 0, 0, pitch, buttons, 0),
                    new InputCommand(target, t, t + 1, 0, 0, 3.1f, 0, InputButtons.None, 0)
                });
                using var ev = sim.AcquireEvents();
                for (int i = 0; i < ev.Events.Count; i++) events.Add(ev.Events[i]);
            }
            return (events, target);
        }

        [Fact]
        public void One_blast_is_one_shot_with_an_impact_per_pellet_and_kills_up_close()
        {
            var (events, target) = BlastAt(4f, 0.12f);
            Assert.Single(events, e => e.Kind == CombatEventKind.ShotAccepted);
            int impacts = events.Count(e => e.Kind == CombatEventKind.Impact);
            int hits = events.Count(e => e.Kind == CombatEventKind.DamageApplied);
            // At 4 m, aimed at the torso, the 2.6° cone is about a torso wide: several pellets land on the target, each with
            // its own impact; stray pellets hit the ground or fly on. ADR-127: the first one to four pellets kill a fresh,
            // armored soldier, and pellets resolved after the death apply no damage.
            int onTarget = events.Count(e => e.Kind == CombatEventKind.Impact && e.Target == target);
            Assert.InRange(onTarget, 3, 8);
            Assert.InRange(hits, 1, System.Math.Min(4, onTarget));
            Assert.InRange(impacts, onTarget, 8);
            Assert.Single(events, e => e.Kind == CombatEventKind.Death && e.Target == target);
        }

        [Fact]
        public void M870_blast_kills_an_armored_soldier_at_eight_meters()
        {
            var (events, target) = BlastAt(8f, 0.06f);
            Assert.Single(events, e => e.Kind == CombatEventKind.ShotAccepted);
            Assert.Single(events, e => e.Kind == CombatEventKind.Death && e.Target == target);
        }

        [Fact]
        public void M870_two_torso_pellets_kill_an_armored_soldier_inside_ten_meters()
        {
            // ADR-127: 100 per pellet to 10 m, falling linearly to 10 at 30 m.
            var def = WeaponDefinition.FromId(M870);
            Assert.Equal(100, DamageMath.FalloffDamage(def.BaseDamage, def.FalloffScale(10f)));
            Assert.Equal(77, DamageMath.FalloffDamage(def.BaseDamage, def.FalloffScale(15f))); // 77.5 in float rounds down
            Assert.Equal(55, DamageMath.FalloffDamage(def.BaseDamage, def.FalloffScale(20f)));
            Assert.Equal(10, DamageMath.FalloffDamage(def.BaseDamage, def.FalloffScale(30f)));
            Assert.Equal(10, DamageMath.FalloffDamage(def.BaseDamage, def.FalloffScale(45f)));

            // Fresh soldier: 100 health, 100 armor. Armor takes half of each torso or limb pellet (ADR-110).
            static (ushort health, ushort armor) Hit(ushort health, ushort armor, ushort region, ushort baseDamage)
            {
                ushort amount = DamageMath.RoundDamage(baseDamage, region);
                ushort taken = amount;
                if (DamageMath.ArmorCovers(region) && armor > 0)
                {
                    taken = DamageMath.SplitArmor(amount, armor, out ushort armorTaken);
                    armor = (ushort)(armor - armorTaken);
                }
                DamageMath.Apply(health, taken, out ushort remaining);
                return (remaining, armor);
            }
            var one = Hit(DamageMath.MaxHealth, DamageMath.MaxArmor, 2, 100);
            Assert.Equal((ushort)50, one.health);
            Assert.Equal((ushort)0, Hit(one.health, one.armor, 2, 100).health);
            // One torso and two limb pellets also kill; one head pellet kills alone.
            var limb = Hit(one.health, one.armor, 3, 100);
            Assert.Equal((ushort)0, Hit(limb.health, limb.armor, 4, 100).health);
            Assert.Equal((ushort)0, Hit(DamageMath.MaxHealth, DamageMath.MaxArmor, 1, 100).health);
            // Once the armor is gone, one torso pellet kills.
            Assert.Equal((ushort)0, Hit(DamageMath.MaxHealth, 0, 2, 100).health);
        }

        [Fact]
        public void Identity_modifiers_leave_every_new_gun_bit_identical()
        {
            foreach (var id in new[] { "weapons/m4a1", "weapons/m24", "weapons/mk16", "weapons/m870" })
            {
                var def = WeaponDefinition.FromId(new DefinitionId(id));
                Assert.Equal(def, WeaponModifiers.Identity.Apply(def));
                Assert.Equal(def, new WeaponModifiers(1000, 1000, 1000, 1000, 0, 1000, 1000, 1000, 1000).Apply(def));
            }
        }
    }
}
