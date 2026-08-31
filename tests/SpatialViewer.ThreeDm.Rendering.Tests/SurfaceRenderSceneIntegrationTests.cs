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

    [Fact]
    public void CameraRelativeToleranceBucketsReuseNearbyZoomMeshes()
    {
        var surface = CurvedPatch();
        var sourceId = Guid.NewGuid();
        var document = new ThreeDmSceneDocument(
            "zoom-cache.3dm",
            [new ThreeDmSceneObject(sourceId, "Surface", null, ThreeDmGeometryKind.Surface, surface.Bounds, Geometry: surface)],
            surface.Bounds,
            Array.Empty<ThreeDmImportDiagnostic>());
        var builder = new ThreeDmRenderSceneBuilder();

        var firstSettings = new ThreeDmTessellationSettings(
            ThreeDmTessellationQuality.Normal,
            WorldUnitsPerPixel: 1.0,
            MaxSurfaceSegmentsPerDirection: 32);
        var nearbySettings = firstSettings with { WorldUnitsPerPixel = 0.95 };
        var closerSettings = firstSettings with { WorldUnitsPerPixel = 0.4 };

        var firstBucket = firstSettings.ResolveCacheChordTolerance(surface.Bounds);
        var nearbyBucket = nearbySettings.ResolveCacheChordTolerance(surface.Bounds);
        var closerBucket = closerSettings.ResolveCacheChordTolerance(surface.Bounds);
        Assert.Equal(0.5, firstBucket, 12);
        Assert.Equal(firstBucket, nearbyBucket, 12);
        Assert.Equal(0.25, closerBucket, 12);

        var first = builder.Build(document, firstSettings);
        var firstCacheCount = builder.CacheEntryCount;
        var nearby = builder.Build(document, nearbySettings);
        Assert.Equal(firstCacheCount, builder.CacheEntryCount);
        Assert.Equal(Assert.Single(first.Meshes).Vertices.Count, Assert.Single(nearby.Meshes).Vertices.Count);

        var closer = builder.Build(document, closerSettings);
        Assert.Equal(firstCacheCount + 1, builder.CacheEntryCount);
        Assert.True(Assert.Single(closer.Meshes).Vertices.Count >= Assert.Single(first.Meshes).Vertices.Count);
    }

    [Fact]
    public void ExplicitAbsoluteToleranceIsNeverBucketed()
    {
        var surface = CurvedPatch();
        var settings = new ThreeDmTessellationSettings(AbsoluteChordTolerance: 0.3);

        Assert.Equal(0.3, settings.ResolveCacheChordTolerance(surface.Bounds), 12);
    }

    [Fact]
    public void CacheToleranceNeverDropsBelowModelToleranceFloor()
    {
        var surface = CurvedPatch();
        var settings = new ThreeDmTessellationSettings(
            ThreeDmTessellationQuality.High,
            WorldUnitsPerPixel: 0.01);

        var cacheTolerance = settings.ResolveCacheChordTolerance(surface.Bounds, modelAbsoluteTolerance: 4.0);

        Assert.Equal(1.0, cacheTolerance, 12);
        Assert.Equal(settings.ResolveChordTolerance(surface.Bounds, 4.0), cacheTolerance, 12);
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
