using SpatialViewer.ThreeDm.Core;
using SpatialViewer.ThreeDm.Rendering;

namespace SpatialViewer.ThreeDm.Rendering.Tests;

public sealed class SurfaceRenderSceneIntegrationTests
{
    [Fact]
    public void SceneBuilderTessellatesStandaloneSurfaceAndSeparatesSurfaceBudgetsInCache()
    {
        var sourceId = Guid.NewGuid();
        var materialId = Guid.NewGuid();
        var surface = CurvedPatch();
        var sceneObject = new ThreeDmSceneObject(
            sourceId,
            "CurvedSurface",
            null,
            ThreeDmGeometryKind.Surface,
            surface.Bounds,
            materialId,
            true,
            0xFF336699,
            Geometry: surface);
        var document = new ThreeDmSceneDocument(
            "surface.3dm",
            [sceneObject],
            surface.Bounds,
            Array.Empty<ThreeDmImportDiagnostic>());
        var builder = new ThreeDmRenderSceneBuilder();

        var lowBudget = builder.Build(
            document,
            new ThreeDmTessellationSettings(
                ThreeDmTessellationQuality.High,
                AbsoluteChordTolerance: 1e-12,
                MaxSurfaceSegmentsPerDirection: 2));
        var cacheAfterLow = builder.CacheEntryCount;
        var highBudget = builder.Build(
            document,
            new ThreeDmTessellationSettings(
                ThreeDmTessellationQuality.High,
                AbsoluteChordTolerance: 1e-12,
                MaxSurfaceSegmentsPerDirection: 8));

        var lowMesh = Assert.Single(lowBudget.Meshes);
        var highMesh = Assert.Single(highBudget.Meshes);
        Assert.Equal(sourceId, highMesh.SourceObjectId);
        Assert.Equal(materialId, highMesh.MaterialId);
        Assert.Equal(0xFF336699u, highMesh.ColorArgb);
        Assert.True(highMesh.Vertices.Count > lowMesh.Vertices.Count);
        Assert.Equal(cacheAfterLow + 1, builder.CacheEntryCount);
        Assert.DoesNotContain(highBudget.Diagnostics, item => item.Code == "3DM_RENDER_SURFACE_MESH_PENDING");
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
