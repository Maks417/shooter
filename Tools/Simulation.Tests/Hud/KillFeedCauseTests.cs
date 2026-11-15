#nullable enable
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Presentation.Hud;
using Xunit;

namespace Shooter.Simulation.Tests.Hud
{
    public sealed class KillFeedCauseTests
    {
        private static EventId Ev(ushort ordinal) => new EventId(1, 10, new EntityId(1), ordinal);
        private static EntityId Killer => new EntityId(1);
        private static EntityId Victim => new EntityId(2);

        [Fact]
        public void Rifle_headshot_uses_shot_weapon_and_damage_region()
        {
            var events = new List<CombatEvent>
            {
                new CombatEvent(Ev(1), CombatEventKind.ShotAccepted, Killer, default,
                    new ShotPayload(new DefinitionId(KillFeedCause.RifleWeaponId), 1, new Float3(0, 1, 0), new Float3(0, 0, 1), 1, 1)),
                new CombatEvent(Ev(2), CombatEventKind.DamageApplied, Killer, Victim,
                    new DamagePayload(100, 0, KillFeedCause.HeadRegionId)),
                new CombatEvent(Ev(3), CombatEventKind.Death, Killer, Victim, new DeathPayload(Killer))
            };

            KillFeedCause.Resolve(events, 2, out KillFeedWeapon weapon, out bool headshot);
            Assert.Equal(KillFeedWeapon.Rifle, weapon);
            Assert.True(headshot);
        }

        [Fact]
        public void Carbine_and_pistol_and_knife_map_from_weapon_id()
        {
            Assert.Equal(KillFeedWeapon.Carbine, KillFeedCause.MapWeapon(new DefinitionId(KillFeedCause.CarbineWeaponId)));
            Assert.Equal(KillFeedWeapon.Pistol, KillFeedCause.MapWeapon(new DefinitionId(KillFeedCause.PistolWeaponId)));
            Assert.Equal(KillFeedWeapon.Knife, KillFeedCause.MapWeapon(new DefinitionId(KillFeedCause.KnifeWeaponId)));
            Assert.Equal(KillFeedWeapon.Grenade, KillFeedCause.MapWeapon(new DefinitionId(KillFeedCause.GrenadeWeaponId)));
        }

        [Fact]
        public void Knife_kill_without_head_region_is_not_headshot()
        {
            var events = new List<CombatEvent>
            {
                new CombatEvent(Ev(1), CombatEventKind.ShotAccepted, Killer, default,
                    new ShotPayload(new DefinitionId(KillFeedCause.KnifeWeaponId), 1, new Float3(0, 1, 0), new Float3(0, 0, 1), 1, 1)),
                new CombatEvent(Ev(2), CombatEventKind.DamageApplied, Killer, Victim,
                    new DamagePayload(80, 0, 2)),
                new CombatEvent(Ev(3), CombatEventKind.Death, Killer, Victim, new DeathPayload(Killer))
            };

            KillFeedCause.Resolve(events, 2, out KillFeedWeapon weapon, out bool headshot);
            Assert.Equal(KillFeedWeapon.Knife, weapon);
            Assert.False(headshot);
        }

        [Fact]
        public void Grenade_kill_has_damage_without_shot_accepted()
        {
            var events = new List<CombatEvent>
            {
                new CombatEvent(Ev(1), CombatEventKind.DamageApplied, Killer, Victim,
                    new DamagePayload(120, 0, 2)),
                new CombatEvent(Ev(2), CombatEventKind.Death, Killer, Victim, new DeathPayload(Killer))
            };

            KillFeedCause.Resolve(events, 1, out KillFeedWeapon weapon, out bool headshot);
            Assert.Equal(KillFeedWeapon.Grenade, weapon);
            Assert.False(headshot);
        }

        [Fact]
        public void Environment_death_has_no_weapon_or_headshot()
        {
            var events = new List<CombatEvent>
            {
                new CombatEvent(Ev(1), CombatEventKind.Death, default, Victim, new DeathPayload(default))
            };

            KillFeedCause.Resolve(events, 0, out KillFeedWeapon weapon, out bool headshot);
            Assert.Equal(KillFeedWeapon.None, weapon);
            Assert.False(headshot);
        }

        [Fact]
        public void Last_matching_shot_and_damage_win_when_batch_has_noise()
        {
            var other = new EntityId(9);
            var events = new List<CombatEvent>
            {
                new CombatEvent(Ev(1), CombatEventKind.ShotAccepted, Killer, default,
                    new ShotPayload(new DefinitionId(KillFeedCause.PistolWeaponId), 1, new Float3(0, 1, 0), new Float3(0, 0, 1), 1, 1)),
                new CombatEvent(Ev(2), CombatEventKind.DamageApplied, Killer, other,
                    new DamagePayload(10, 90, KillFeedCause.HeadRegionId)),
                new CombatEvent(Ev(3), CombatEventKind.ShotAccepted, Killer, default,
                    new ShotPayload(new DefinitionId(KillFeedCause.RifleWeaponId), 2, new Float3(0, 1, 0), new Float3(0, 0, 1), 2, 1)),
                new CombatEvent(Ev(4), CombatEventKind.DamageApplied, Killer, Victim,
                    new DamagePayload(100, 0, KillFeedCause.HeadRegionId)),
                new CombatEvent(Ev(5), CombatEventKind.Death, Killer, Victim, new DeathPayload(Killer))
            };

            KillFeedCause.Resolve(events, 4, out KillFeedWeapon weapon, out bool headshot);
            Assert.Equal(KillFeedWeapon.Rifle, weapon);
            Assert.True(headshot);
        }
    }
}
