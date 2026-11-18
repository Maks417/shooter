using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Generation;
using Xunit;

/// <summary>ADR-106 modelling kernel: every builder yields a closed, outward, codec-valid solid.</summary>
public sealed class ShapeKitTests
{
    private static List<Float2> Grip() => ShapeKit.RoundedOutline(new[]
    {
        new ProfileCorner(-.02f, .02f, .006f), new ProfileCorner(.025f, .02f, .006f), new ProfileCorner(.03f, -.04f, .012f),
        new ProfileCorner(.012f, -.05f, .004f), new ProfileCorner(.02f, -.07f, .006f), new ProfileCorner(-.025f, -.09f, .01f),
    }, 4);

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(.003f, 1)]
    [InlineData(.012f, 4)]
    public void Extrusion_is_a_closed_outward_solid(float edge, int segments)
    {
        var mesh = ShapeKit.Extrude(Grip(), .03f, edge, segments);
        MeshValidation.Check(mesh, true);
        GeometryCodec.DecodeMesh(GeometryCodec.EncodeMesh(mesh));
    }

    [Fact]
    public void Concave_outline_with_small_fillets_extrudes()
    {
        // L-shaped receiver outline with a concave inner corner.
        var outline = ShapeKit.RoundedOutline(new[]
        {
            new ProfileCorner(-.1f, 0, .002f), new ProfileCorner(.2f, 0, .002f), new ProfileCorner(.2f, .05f, .004f),
            new ProfileCorner(.05f, .05f, .003f), new ProfileCorner(.05f, .02f, .003f), new ProfileCorner(-.1f, .02f, .002f)
        }, 3);
        MeshValidation.Check(ShapeKit.Extrude(outline, .046f, .0025f, 2), true);
    }

    [Fact]
    public void Lathe_with_steps_and_bore_is_closed()
    {
        var profile = new[] { new Float2(.009f, 0), new Float2(.009f, .2f), new Float2(.013f, .2f), new Float2(.013f, .25f), new Float2(.011f, .26f),
            new Float2(.005f, .26f), new Float2(.005f, .24f) };
        MeshValidation.Check(ShapeKit.Lathe(profile, 24), true);
    }

    [Fact]
    public void Loft_is_closed_and_smooth()
    {
        var mesh = ShapeKit.Loft(new[] { new ShapeKit.Section(-.4f, .05f, .02f, .06f, 3), new ShapeKit.Section(-.3f, .055f, .022f, .05f, 3),
            new ShapeKit.Section(-.1f, .06f, .018f, .025f, 4) }, 24);
        MeshValidation.Check(mesh, true);
    }

    [Fact]
    public void Outputs_are_deterministic()
    {
        Assert.Equal(GeometryCodec.EncodeMesh(ShapeKit.Extrude(Grip(), .03f, .012f, 4)), GeometryCodec.EncodeMesh(ShapeKit.Extrude(Grip(), .03f, .012f, 4)));
    }
}
