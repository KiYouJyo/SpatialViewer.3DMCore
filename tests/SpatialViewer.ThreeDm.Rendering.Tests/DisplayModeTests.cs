using SpatialViewer.ThreeDm.Core;
using SpatialViewer.ThreeDm.Rendering;

namespace SpatialViewer.ThreeDm.Rendering.Tests;

public sealed class DisplayModeTests
{
    [Fact]
    public void WireframeConvertsPlainMeshTrianglesToUniqueEdges()
    {
        var sourceId = Guid.NewGuid();
        var document = Document(new ThreeDmSceneObject(
            sourceId,
            "Mesh",
            null,
            ThreeDmGeometryKind.Mesh,
            UnitBounds,
            Geometry: UnitTriangle));

        var scene = new ThreeDmVisualRenderSceneBuilder().Build(
            document,
            new ThreeDmVisualRenderSettings(ThreeDmRenderDisplayMode.Wireframe));

        Assert.Empty(scene.Meshes);
        Assert.Equal(3, scene.Curves.Count);
        Assert.All(scene.Curves, curve =>
        {
            Assert.Equal(sourceId, curve.SourceObjectId);
            Assert.Equal(ThreeDmRenderCurveKind.Line, curve.Kind);
            Assert.Equal(2, curve.Points.Count);
        });
    }

    [Fact]
    public void ShadedSuppressesBrepEdgeOverlayWhenFillMeshExists()
    {
        var sourceId = Guid.NewGuid();
        var brep = EmptyBrep([LineEdge()]) with
        {
            RenderMeshes = [new ThreeDmEmbeddedRenderMeshData(0, UnitTriangle)],
        };
        var document = Document(new ThreeDmSceneObject(
            sourceId,
            "Brep",
            null,
            ThreeDmGeometryKind.Brep,
            UnitBounds,
            Geometry: brep));

        var scene = new ThreeDmVisualRenderSceneBuilder().Build(
            document,
            new ThreeDmVisualRenderSettings(ThreeDmRenderDisplayMode.Shaded));

        Assert.Single(scene.Meshes);
        Assert.Empty(scene.Curves);
    }

    [Fact]
    public void ShadedKeepsExactBrepWireFallbackWhenFillMeshIsMissing()
    {
        var sourceId = Guid.NewGuid();
        var document = Document(new ThreeDmSceneObject(
            sourceId,
            "Brep",
            null,
            ThreeDmGeometryKind.Brep,
            UnitBounds,
            Geometry: EmptyBrep([LineEdge()])));

        var scene = new ThreeDmVisualRenderSceneBuilder().Build(
            document,
            new ThreeDmVisualRenderSettings(ThreeDmRenderDisplayMode.Shaded));

        Assert.Empty(scene.Meshes);
        Assert.Single(scene.Curves);
        Assert.Contains(scene.Diagnostics, item => item.Code == "3DM_RENDER_BREP_FILL_REQUIRES_RENDER_MESH");
    }

    private static ThreeDmBrepGeometryData EmptyBrep(IReadOnlyList<ThreeDmBrepEdgeData> edges) =>
        new(
            [
                new ThreeDmBrepVertexData(0, new Point3d(0, 0, 0), 0),
                new ThreeDmBrepVertexData(1, new Point3d(1, 0, 0), 0),
            ],
            edges,
            Array.Empty<ThreeDmBrepTrimData>(),
            Array.Empty<ThreeDmBrepLoopData>(),
            Array.Empty<ThreeDmBrepFaceData>(),
            false,
            UnitBounds);

    private static ThreeDmBrepEdgeData LineEdge() =>
        new(0, 0, 1, 0, LineCurve());

    private static ThreeDmCurveGeometryData LineCurve() =>
        new(
            ThreeDmCurveForm.Line,
            new ThreeDmNurbsCurveData(
                1,
                false,
                false,
                false,
                [
                    new ThreeDmWeightedPoint3d(new Point3d(0, 0, 0), 1),
                    new ThreeDmWeightedPoint3d(new Point3d(1, 0, 0), 1),
                ],
                [0, 1],
                0,
                1),
            Array.Empty<Point3d>(),
            null,
            null,
            BoundingBox3d.FromPoints(new Point3d(0, 0, 0), new Point3d(1, 0, 0)));

    private static ThreeDmSceneDocument Document(ThreeDmSceneObject sceneObject) =>
        new("display.3dm", [sceneObject], UnitBounds, Array.Empty<ThreeDmImportDiagnostic>());

    private static readonly BoundingBox3d UnitBounds =
        BoundingBox3d.FromPoints(new Point3d(0, 0, 0), new Point3d(1, 1, 0));

    private static readonly ThreeDmMeshGeometryData UnitTriangle = new(
        [new Point3d(0, 0, 0), new Point3d(1, 0, 0), new Point3d(0, 1, 0)],
        [new ThreeDmMeshFace(0, 1, 2, null)],
        [new Vector3d(0, 0, 1), new Vector3d(0, 0, 1), new Vector3d(0, 0, 1)],
        Array.Empty<ThreeDmTextureCoordinate>(),
        false,
        UnitBounds);
}
