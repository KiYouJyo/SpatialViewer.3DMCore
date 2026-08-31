using SpatialViewer.ThreeDm.Core;
using SpatialViewer.ThreeDm.Rendering;

namespace SpatialViewer.ThreeDm.Rendering.Tests;

public sealed class EmbeddedRenderMeshTests
{
    [Fact]
    public void SceneBuilderUsesEmbeddedBrepRenderMeshWithoutFillFallback()
    {
        var sourceId = Guid.NewGuid();
        var embeddedMesh = QuadMesh();
        var brep = EmptyBrep(embeddedMesh.Bounds) with
        {
            RenderMeshes = [new ThreeDmEmbeddedRenderMeshData(4, embeddedMesh)],
        };
        var document = BrepDocument(sourceId, brep);

        var scene = new ThreeDmRenderSceneBuilder().Build(document);

        var mesh = Assert.Single(scene.Meshes);
        Assert.Equal(sourceId, mesh.SourceObjectId);
        Assert.Equal(4, mesh.SourceSubobjectIndex);
        Assert.Equal([0, 1, 2, 0, 2, 3], mesh.Indices);
        Assert.DoesNotContain(scene.Diagnostics, item => item.Code == "3DM_RENDER_BREP_FILL_REQUIRES_RENDER_MESH");
    }

    [Fact]
    public void SceneBuilderKeepsExplicitWireFallbackWhenBrepHasNoRenderMesh()
    {
        var sourceId = Guid.NewGuid();
        var bounds = BoundingBox3d.FromPoints(new Point3d(0, 0, 0), new Point3d(1, 1, 1));
        var document = BrepDocument(sourceId, EmptyBrep(bounds));

        var scene = new ThreeDmRenderSceneBuilder().Build(document);

        Assert.Empty(scene.Meshes);
        Assert.Contains(scene.Diagnostics, item => item.Code == "3DM_RENDER_BREP_FILL_REQUIRES_RENDER_MESH");
    }

    [Fact]
    public void SceneBuilderUsesEmbeddedExtrusionRenderMeshWithoutFillFallback()
    {
        var sourceId = Guid.NewGuid();
        var embeddedMesh = QuadMesh();
        var extrusion = EmptyExtrusion(embeddedMesh.Bounds) with
        {
            RenderMeshes = [new ThreeDmEmbeddedRenderMeshData(null, embeddedMesh)],
        };
        var document = ExtrusionDocument(sourceId, extrusion);

        var scene = new ThreeDmRenderSceneBuilder().Build(document);

        var mesh = Assert.Single(scene.Meshes);
        Assert.Equal(sourceId, mesh.SourceObjectId);
        Assert.Equal([0, 1, 2, 0, 2, 3], mesh.Indices);
        Assert.DoesNotContain(scene.Diagnostics, item => item.Code == "3DM_RENDER_EXTRUSION_FILL_REQUIRES_RENDER_MESH");
    }

    [Fact]
    public void SceneBuilderKeepsExplicitWireFallbackWhenExtrusionHasNoRenderMesh()
    {
        var sourceId = Guid.NewGuid();
        var bounds = BoundingBox3d.FromPoints(new Point3d(0, 0, 0), new Point3d(1, 1, 2));
        var document = ExtrusionDocument(sourceId, EmptyExtrusion(bounds));

        var scene = new ThreeDmRenderSceneBuilder().Build(document);

        Assert.Empty(scene.Meshes);
        Assert.Contains(scene.Diagnostics, item => item.Code == "3DM_RENDER_EXTRUSION_FILL_REQUIRES_RENDER_MESH");
    }

    private static ThreeDmMeshGeometryData QuadMesh() =>
        new(
            [
                new Point3d(0, 0, 0),
                new Point3d(4, 0, 0),
                new Point3d(4, 3, 0),
                new Point3d(0, 3, 0),
            ],
            [new ThreeDmMeshFace(0, 1, 2, 3)],
            [
                new Vector3d(0, 0, 1),
                new Vector3d(0, 0, 1),
                new Vector3d(0, 0, 1),
                new Vector3d(0, 0, 1),
            ],
            Array.Empty<ThreeDmTextureCoordinate>(),
            false,
            BoundingBox3d.FromPoints(new Point3d(0, 0, 0), new Point3d(4, 3, 0)));

    private static ThreeDmBrepGeometryData EmptyBrep(BoundingBox3d bounds) =>
        new(
            Array.Empty<ThreeDmBrepVertexData>(),
            Array.Empty<ThreeDmBrepEdgeData>(),
            Array.Empty<ThreeDmBrepTrimData>(),
            Array.Empty<ThreeDmBrepLoopData>(),
            Array.Empty<ThreeDmBrepFaceData>(),
            false,
            bounds);

    private static ThreeDmExtrusionGeometryData EmptyExtrusion(BoundingBox3d bounds) =>
        new(
            new Point3d(0, 0, 0),
            new Point3d(0, 0, 2),
            new Vector3d(0, 0, 1),
            true,
            true,
            true,
            Array.Empty<ThreeDmCurveGeometryData>(),
            bounds);

    private static ThreeDmSceneDocument BrepDocument(Guid sourceId, ThreeDmBrepGeometryData brep) =>
        new(
            "embedded-brep-mesh.3dm",
            [new ThreeDmSceneObject(sourceId, "Brep", null, ThreeDmGeometryKind.Brep, brep.Bounds, Geometry: brep)],
            brep.Bounds,
            Array.Empty<ThreeDmImportDiagnostic>());

    private static ThreeDmSceneDocument ExtrusionDocument(Guid sourceId, ThreeDmExtrusionGeometryData extrusion) =>
        new(
            "embedded-extrusion-mesh.3dm",
            [new ThreeDmSceneObject(sourceId, "Extrusion", null, ThreeDmGeometryKind.Extrusion, extrusion.Bounds, Geometry: extrusion)],
            extrusion.Bounds,
            Array.Empty<ThreeDmImportDiagnostic>());
}
