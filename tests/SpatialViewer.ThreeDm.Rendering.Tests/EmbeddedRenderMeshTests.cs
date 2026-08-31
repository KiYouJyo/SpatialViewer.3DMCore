using SpatialViewer.ThreeDm.Core;
using SpatialViewer.ThreeDm.Rendering;

namespace SpatialViewer.ThreeDm.Rendering.Tests;

public sealed class EmbeddedRenderMeshTests
{
    [Fact]
    public void SceneBuilderUsesEmbeddedBrepRenderMeshWithoutFillFallback()
    {
        var sourceId = Guid.NewGuid();
        var embeddedMesh = new ThreeDmMeshGeometryData(
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

        var brep = EmptyBrep(embeddedMesh.Bounds) with
        {
            RenderMeshes = [new ThreeDmEmbeddedRenderMeshData(4, embeddedMesh)],
        };
        var document = Document(sourceId, brep);

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
        var document = Document(sourceId, EmptyBrep(bounds));

        var scene = new ThreeDmRenderSceneBuilder().Build(document);

        Assert.Empty(scene.Meshes);
        Assert.Contains(scene.Diagnostics, item => item.Code == "3DM_RENDER_BREP_FILL_REQUIRES_RENDER_MESH");
    }

    private static ThreeDmBrepGeometryData EmptyBrep(BoundingBox3d bounds) =>
        new(
            Array.Empty<ThreeDmBrepVertexData>(),
            Array.Empty<ThreeDmBrepEdgeData>(),
            Array.Empty<ThreeDmBrepTrimData>(),
            Array.Empty<ThreeDmBrepLoopData>(),
            Array.Empty<ThreeDmBrepFaceData>(),
            false,
            bounds);

    private static ThreeDmSceneDocument Document(Guid sourceId, ThreeDmBrepGeometryData brep) =>
        new(
            "embedded-mesh.3dm",
            [new ThreeDmSceneObject(sourceId, "Brep", null, ThreeDmGeometryKind.Brep, brep.Bounds, Geometry: brep)],
            brep.Bounds,
            Array.Empty<ThreeDmImportDiagnostic>());
}
