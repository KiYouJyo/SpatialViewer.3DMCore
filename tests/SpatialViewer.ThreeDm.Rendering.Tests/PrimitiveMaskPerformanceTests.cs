using SpatialViewer.ThreeDm.Core;
using SpatialViewer.ThreeDm.Rendering;

namespace SpatialViewer.ThreeDm.Rendering.Tests;

public sealed class PrimitiveMaskPerformanceTests
{
    [Fact]
    public void CurvesAndPointsOnlyPassSkipsPureMeshTessellationAndCache()
    {
        var id = Guid.NewGuid();
        var bounds = BoundingBox3d.FromPoints(
            new Point3d(0, 0, 0),
            new Point3d(1, 1, 0));
        var mesh = new ThreeDmMeshGeometryData(
            [
                new Point3d(0, 0, 0),
                new Point3d(1, 0, 0),
                new Point3d(0, 1, 0),
            ],
            [new ThreeDmMeshFace(0, 1, 2)],
            Array.Empty<Vector3d>(),
            Array.Empty<ThreeDmTextureCoordinate>(),
            false,
            bounds);
        var document = new ThreeDmSceneDocument(
            "mesh.3dm",
            [new ThreeDmSceneObject(id, "Mesh", null, ThreeDmGeometryKind.Mesh, bounds, Geometry: mesh)],
            bounds,
            Array.Empty<ThreeDmImportDiagnostic>());

        var builder = new ThreeDmRenderSceneBuilder();
        var scene = builder.Build(
            document,
            new ThreeDmTessellationSettings(),
            ThreeDmRenderPrimitiveMask.Curves | ThreeDmRenderPrimitiveMask.PointSets);

        Assert.Empty(scene.Meshes);
        Assert.Empty(scene.Curves);
        Assert.Empty(scene.PointSets);
        Assert.Equal(0, builder.CacheEntryCount);
    }
}
