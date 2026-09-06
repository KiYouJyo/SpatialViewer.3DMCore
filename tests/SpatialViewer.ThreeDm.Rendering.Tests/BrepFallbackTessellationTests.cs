using SpatialViewer.ThreeDm.Core;
using SpatialViewer.ThreeDm.Rendering;

namespace SpatialViewer.ThreeDm.Rendering.Tests;

public sealed class BrepFallbackTessellationTests
{
    [Fact]
    public void BrepWithoutEmbeddedRenderMeshProducesFaceMesh()
    {
        var objectId = Guid.NewGuid();
        var surface = PlaneSurface();
        var face = new ThreeDmBrepFaceData(7, 0, false, Array.Empty<int>(), surface);
        var brep = new ThreeDmBrepGeometryData(
            Array.Empty<ThreeDmBrepVertexData>(),
            Array.Empty<ThreeDmBrepEdgeData>(),
            Array.Empty<ThreeDmBrepTrimData>(),
            Array.Empty<ThreeDmBrepLoopData>(),
            [face],
            false,
            surface.Bounds);
        var document = new ThreeDmSceneDocument(
            "fallback.3dm",
            [new ThreeDmSceneObject(
                objectId,
                "Brep",
                null,
                ThreeDmGeometryKind.Brep,
                surface.Bounds,
                Geometry: brep)],
            surface.Bounds,
            Array.Empty<ThreeDmImportDiagnostic>());

        var scene = new ThreeDmRenderSceneBuilder().Build(document);

        var mesh = Assert.Single(scene.Meshes);
        Assert.Equal(objectId, mesh.SourceObjectId);
        Assert.Equal(7, mesh.SourceSubobjectIndex);
        Assert.NotEmpty(mesh.Vertices);
        Assert.NotEmpty(mesh.Indices);
        Assert.Contains(scene.Diagnostics, item => item.Code == "3DM_RENDER_BREP_FALLBACK_TESSELLATION");
    }

    private static ThreeDmNurbsSurfaceGeometryData PlaneSurface() =>
        new(
            1,
            1,
            2,
            2,
            false,
            false,
            false,
            false,
            false,
            [
                new ThreeDmWeightedPoint3d(new Point3d(0, 0, 0), 1),
                new ThreeDmWeightedPoint3d(new Point3d(0, 10, 0), 1),
                new ThreeDmWeightedPoint3d(new Point3d(10, 0, 0), 1),
                new ThreeDmWeightedPoint3d(new Point3d(10, 10, 0), 1),
            ],
            [0, 1],
            [0, 1],
            BoundingBox3d.FromPoints(new Point3d(0, 0, 0), new Point3d(10, 10, 0)))
        {
            StartSuperfluousKnotU = 0,
            EndSuperfluousKnotU = 1,
            StartSuperfluousKnotV = 0,
            EndSuperfluousKnotV = 1,
        };
}
