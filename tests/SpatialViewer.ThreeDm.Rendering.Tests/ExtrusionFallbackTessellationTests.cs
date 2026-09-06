using SpatialViewer.ThreeDm.Core;
using SpatialViewer.ThreeDm.Rendering;

namespace SpatialViewer.ThreeDm.Rendering.Tests;

public sealed class ExtrusionFallbackTessellationTests
{
    [Fact]
    public void ClosedExtrusionWithoutEmbeddedRenderMeshProducesFillMesh()
    {
        var objectId = Guid.NewGuid();
        var profile = Polyline(
            new Point3d(0, 0, 0),
            new Point3d(10, 0, 0),
            new Point3d(10, 8, 0),
            new Point3d(0, 8, 0),
            new Point3d(0, 0, 0));
        var extrusion = new ThreeDmExtrusionGeometryData(
            new Point3d(0, 0, 0),
            new Point3d(0, 0, 6),
            new Vector3d(0, 0, 1),
            true,
            true,
            true,
            [profile],
            BoundingBox3d.FromPoints(new Point3d(0, 0, 0), new Point3d(10, 8, 6)));
        var document = new ThreeDmSceneDocument(
            "extrusion.3dm",
            [new ThreeDmSceneObject(
                objectId,
                "Extrusion",
                null,
                ThreeDmGeometryKind.Extrusion,
                extrusion.Bounds,
                Geometry: extrusion)],
            extrusion.Bounds,
            Array.Empty<ThreeDmImportDiagnostic>());

        var scene = new ThreeDmRenderSceneBuilder().Build(document);

        var mesh = Assert.Single(scene.Meshes);
        Assert.Equal(objectId, mesh.SourceObjectId);
        Assert.True(mesh.Vertices.Count >= 8);
        Assert.True(mesh.Indices.Count >= 36);
        Assert.Contains(scene.Diagnostics, item => item.Code == "3DM_RENDER_EXTRUSION_FALLBACK_TESSELLATION");
    }

    private static ThreeDmCurveGeometryData Polyline(params Point3d[] points) =>
        new(
            ThreeDmCurveForm.Polyline,
            new ThreeDmNurbsCurveData(
                1,
                false,
                true,
                false,
                Array.Empty<ThreeDmWeightedPoint3d>(),
                Array.Empty<double>(),
                0,
                1),
            points,
            null,
            null,
            BoundingBox3d.FromPoints(points));
}
