#nullable enable
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.World.Navigation;
using Xunit;

namespace Shooter.Simulation.Tests
{
public sealed class NavPathOutcomeTests
{
    private static (RevisionedPathAdapter adapter, PathTicket ticket) Make(NavPathOutcome outcome, int cornerCount = 2)
    {
        var adapter = new RevisionedPathAdapter(1, id => new Float3(0, 0, 0),
            (a, b, corners) =>
            {
                corners!.Clear();
                for (int i = 0; i < cornerCount; i++) corners.Add(new Float3(i, 0, 0));
                return outcome;
            });
        return (adapter, adapter.RequestPath(new EntityId(1), new Float3(5, 0, 0), 1));
    }

    [Theory]
    [InlineData(NavPathOutcome.Complete, PathStatus.Complete, 0, 0)]
    [InlineData(NavPathOutcome.Partial, PathStatus.Partial, 1, 0)]
    [InlineData(NavPathOutcome.FallbackComplete, PathStatus.Complete, 0, 1)]
    public void Outcome_maps_to_status_and_counters(NavPathOutcome outcome, PathStatus status, int partial, int fallback)
    {
        var (adapter, ticket) = Make(outcome);
        var result = adapter.Poll(ticket, new PathBuffer(16));
        Assert.Equal(status, result.Status);
        Assert.Equal(2, result.WrittenCount);
        Assert.Equal(partial, adapter.PartialPathCount);
        Assert.Equal(fallback, adapter.FallbackPathCount);
    }

    [Fact]
    public void Failed_outcome_reports_failed_without_corners()
    {
        var (adapter, ticket) = Make(NavPathOutcome.Failed);
        var result = adapter.Poll(ticket, new PathBuffer(16));
        Assert.Equal(PathStatus.Failed, result.Status);
        Assert.Equal(0, result.WrittenCount);
        Assert.Equal(0, adapter.PartialPathCount + adapter.FallbackPathCount + adapter.PathsSolved);
    }

    [Fact]
    public void Bool_constructor_still_maps_true_to_complete()
    {
        var adapter = new RevisionedPathAdapter(1, id => new Float3(0, 0, 0), (a, b, c) => { c!.Add(a); return true; });
        var result = adapter.Poll(adapter.RequestPath(new EntityId(1), new Float3(1, 0, 0), 1), new PathBuffer(4));
        Assert.Equal(PathStatus.Complete, result.Status);
    }
}
}
