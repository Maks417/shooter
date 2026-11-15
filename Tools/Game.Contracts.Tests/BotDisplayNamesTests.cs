#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shooter.Content.Maps;
using Shooter.Game.Contracts;
using Shooter.Game.Session;
using Shooter.World.Runtime;
using Xunit;

namespace Shooter.Game.Contracts.Tests
{
    public sealed class BotDisplayNamesTests
    {
        [Fact]
        public void Pool_Has_At_Least_Twenty_Four_Short_Ascii_Names()
        {
            Assert.True(BotDisplayNames.Pool.Length >= 24);
            var seen = new HashSet<string>();
            foreach (string name in BotDisplayNames.Pool)
            {
                Assert.False(string.IsNullOrEmpty(name));
                Assert.InRange(name.Length, 1, GameContractVersion.MaxDisplayName);
                Assert.InRange(name.Length, 1, 16);
                foreach (char c in name)
                    Assert.InRange(c, (char)32, (char)126);
                Assert.True(seen.Add(name), "duplicate pool name: " + name);
            }
        }

        [Fact]
        public void Resolve_Is_Deterministic_For_Same_Seed_And_BotCount()
        {
            const int botCount = 11;
            const ulong seed = 460072030UL;
            var first = Collect(botCount, seed);
            var second = Collect(botCount, seed);
            Assert.Equal(first, second);
        }

        [Fact]
        public void Resolve_Yields_Unique_Names_From_Pool()
        {
            const int botCount = 11;
            var names = Collect(botCount, 460072031UL);
            Assert.Equal(botCount, names.Count);
            Assert.Equal(botCount, new HashSet<string>(names).Count);
            foreach (string name in names)
                Assert.Contains(name, BotDisplayNames.Pool);
        }

        [Fact]
        public void Different_Seeds_Change_Assignment_Order()
        {
            const int botCount = 9;
            var a = Collect(botCount, 1UL);
            var b = Collect(botCount, 2UL);
            var c = Collect(botCount, 99UL);
            Assert.False(SameOrder(a, b) && SameOrder(b, c), "expected at least one seed to change bot name order");
        }

        [Fact]
        public void Resolve_Rejects_Out_Of_Range_Indices()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => BotDisplayNames.Resolve(0, 0, 1UL));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => BotDisplayNames.Resolve(-1, 9, 1UL));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => BotDisplayNames.Resolve(9, 9, 1UL));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => BotDisplayNames.Resolve(0, BotDisplayNames.Pool.Length + 1, 1UL));
        }

        [Fact]
        public async Task OfflineSession_Roster_Uses_Unique_First_Names_Not_BotN()
        {
            var catalog = StaticMapCatalog.CreateReleaseSeed();
            using var session = new OfflineGameSession(catalog, (w, n) => new FakeMapRuntime(w, n), allowFakeMapRuntime: true);
            var request = new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 5, 460072030UL);
            Assert.True((await session.BeginLoadAsync(request, CancellationToken.None)).Succeeded);
            Assert.True(session.Start().Succeeded);
            var snap = session.TryAcquireSnapshot();
            Assert.NotNull(snap);

            var botNames = new HashSet<string>();
            int humans = 0;
            foreach (var p in snap!.Participants)
            {
                if (p.IsHuman)
                {
                    humans++;
                    Assert.Equal("Player", p.DisplayName);
                    continue;
                }

                Assert.DoesNotMatch("^Bot\\d+$", p.DisplayName);
                Assert.Contains(p.DisplayName, BotDisplayNames.Pool);
                Assert.True(botNames.Add(p.DisplayName), "duplicate bot display name: " + p.DisplayName);
            }

            Assert.Equal(1, humans);
            Assert.Equal(9, botNames.Count);
        }

        private static List<string> Collect(int botCount, ulong seed)
        {
            var names = new List<string>(botCount);
            for (int i = 0; i < botCount; i++)
                names.Add(BotDisplayNames.Resolve(i, botCount, seed));
            return names;
        }

        private static bool SameOrder(List<string> a, List<string> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }
}
