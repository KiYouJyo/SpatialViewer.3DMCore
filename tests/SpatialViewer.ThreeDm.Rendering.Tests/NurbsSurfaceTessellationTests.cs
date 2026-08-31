using SpatialViewer.ThreeDm.Core;
using SpatialViewer.ThreeDm.Rendering;

namespace SpatialViewer.ThreeDm.Rendering.Tests;

public sealed class NurbsSurfaceTessellationTests
{
    [Fact]
    public void CurvedQuadraticPatchRefinesByChordErrorAndGeneratesNormalsAndUvs()
    {
        var surface = CurvedPatch();
        var sourceId = Guid.NewGuid();

        var coarse = ThreeDmNurbsSurfaceTessellator.Tessellate(
            sourceId,
            surface,
            new ThreeDmTessellationSettings(AbsoluteChordTolerance: 2.0));
        var fine = ThreeDmNurbsSurfaceTessellator.Tessellate(
            sourceId,
            surface,
            new ThreeDmTessellationSettings(
                ThreeDmTessellationQuality.High,
                AbsoluteChordTolerance: 0.02,
                MaxSurfaceSegmentsPerDirection: 64));

        Assert.Equal(4, coarse.Vertices.Count);
        Assert.Equal(6, coarse.Indices.Count);
        Assert.True(fine.Vertices.Count > coarse.Vertices.Count);
        Assert.Equal(fine.Vertices.Count, fine.Normals.Count);
        Assert.Equal(fine.Vertices.Count, fine.TextureCoordinates.Count);
        Assert.True(fine.Indices.Count > coarse.Indices.Count);

        var centerIndex = Enumerable.Range(0, fine.Vertices.Count)
            .Single(index =>
                Math.Abs(fine.Vertices[index].X - 5) < 1e-9 &&
                Math.Abs(fine.Vertices[index].Y - 5) < 1e-9);
        var center = fine.Vertices[centerIndex];
        var centerNormal = fine.Normals[centerIndex];
        Assert.Equal(1.5, center.Z, 9);
        Assert.True(centerNormal.Z > 0.99);
        Assert.Equal(0.5, fine.TextureCoordinates[centerIndex].U, 9);
        Assert.Equal(0.5, fine.TextureCoordinates[centerIndex].V, 9);
    }

    [Fact]
    public void SurfaceTessellatorHonorsPerDirectionBudget()
    {
        var surface = CurvedPatch();

        var rendered = ThreeDmNurbsSurfaceTessellator.Tessellate(
            Guid.NewGuid(),
            surface,
            new ThreeDmTessellationSettings(
                ThreeDmTessellationQuality.High,
                AbsoluteChordTolerance: 1e-12,
                MaxSurfaceSegmentsPerDirection: 4));

        Assert.True(rendered.Vertices.Count <= 25);
        Assert.True(rendered.Indices.Count <= 4 * 4 * 6);
    }

    private static ThreeDmNurbsSurfaceGeometryData CurvedPatch() =>
        new(
            2,
            2,
            3,
            3,
            false,
            false,
            false,
            false,
            false,
            [
                new ThreeDmWeightedPoint3d(new Point3d(0, 0, 0), 1),
                new ThreeDmWeightedPoint3d(new Point3d(0, 5, 0), 1),
                new ThreeDmWeightedPoint3d(new Point3d(0, 10, 0), 1),
                new ThreeDmWeightedPoint3d(new Point3d(5, 0, 0), 1),
                new ThreeDmWeightedPoint3d(new Point3d(5, 5, 6), 1),
                new ThreeDmWeightedPoint3d(new Point3d(5, 10, 0), 1),
                new ThreeDmWeightedPoint3d(new Point3d(10, 0, 0), 1),
                new ThreeDmWeightedPoint3d(new Point3d(10, 5, 0), 1),
                new ThreeDmWeightedPoint3d(new Point3d(10, 10, 0), 1),
            ],
            [0, 0, 1, 1],
            [0, 0, 1, 1],
            BoundingBox3d.FromPoints(new Point3d(0, 0, 0), new Point3d(10, 10, 1.5)))
        {
            StartSuperfluousKnotU = 0,
            EndSuperfluousKnotU = 1,
            StartSuperfluousKnotV = 0,
            EndSuperfluousKnotV = 1,
        };
}
