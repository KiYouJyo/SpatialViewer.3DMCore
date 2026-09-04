using SpatialViewer.ThreeDm.Core;
using SpatialViewer.ThreeDm.Rendering;

namespace SpatialViewer.ThreeDm.Rendering.Tests;

public sealed class PreparedDrawPolicyTests
{
    [Theory]
    [InlineData(ThreeDmRenderDisplayMode.Shaded, true, false)]
    [InlineData(ThreeDmRenderDisplayMode.ShadedWithEdges, true, true)]
    [InlineData(ThreeDmRenderDisplayMode.Wireframe, false, true)]
    public void GenericMeshGetsExplicitFillAndWirePolicy(
        ThreeDmRenderDisplayMode mode,
        bool drawFill,
        bool drawWire)
    {
        var id = Guid.NewGuid();
        var bounds = BoundingBox3d.FromPoints(new Point3d(0, 0, 0), new Point3d(1, 1, 0));
        var mesh = new ThreeDmMeshGeometryData(
            [new Point3d(0, 0, 0), new Point3d(1, 0, 0), new Point3d(0, 1, 0)],
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

        var scene = new ThreeDmPreparedRenderSceneBuilder().Build(
            document,
            new ThreeDmVisualRenderSettings(mode));

        var policy = Assert.Single(scene.MeshDrawPolicies);
        Assert.Equal(drawFill, policy.DrawFill);
        Assert.Equal(drawWire, policy.DrawWireIndices);
    }
}
