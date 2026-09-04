using SpatialViewer.ThreeDm.Core;
using SpatialViewer.ThreeDm.Rendering;
using SpatialViewer.ThreeDm.Rendering.Windows;

namespace SpatialViewer.ThreeDm.Rendering.Tests;

public sealed class PreparedRenderSceneTests
{
    [Fact]
    public void PreparedSceneKeepsSharedMeshesWithoutExpandingRepeatedInstances()
    {
        var definitionId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var firstInstanceId = Guid.NewGuid();
        var secondInstanceId = Guid.NewGuid();
        var bounds = BoundingBox3d.FromPoints(new Point3d(0, 0, 0), new Point3d(1, 1, 0));
        var mesh = new ThreeDmMeshGeometryData(
            [new Point3d(0, 0, 0), new Point3d(1, 0, 0), new Point3d(0, 1, 0)],
            [new ThreeDmMeshFace(0, 1, 2)],
            [new Vector3d(0, 0, 1), new Vector3d(0, 0, 1), new Vector3d(0, 0, 1)],
            Array.Empty<ThreeDmTextureCoordinate>(),
            false,
            bounds);
        var member = new ThreeDmSceneObject(memberId, "Member", null, ThreeDmGeometryKind.Mesh, bounds, Geometry: mesh);
        var first = Instance(firstInstanceId, definitionId, 0);
        var second = Instance(secondInstanceId, definitionId, 10);
        var document = new ThreeDmSceneDocument("blocks.3dm", [member, first, second], bounds, Array.Empty<ThreeDmImportDiagnostic>())
        {
            InstanceDefinitions = [new ThreeDmInstanceDefinitionInfo(definitionId, "Block", "", null, [memberId])],
        };

        var prepared = new ThreeDmPreparedRenderSceneBuilder().Build(document);

        Assert.Single(prepared.SharedMeshes.Geometries);
        Assert.Equal(2, prepared.SharedMeshes.Instances.Count);
        Assert.Empty(prepared.Curves);
    }

    [Fact]
    public void WindowsBackendProjectsPreparedSceneIntoOneSharedOrigin()
    {
        var prepared = new ThreeDmPreparedRenderScene(
            ThreeDmRenderDisplayMode.ShadedWithEdges,
            new ThreeDmSharedMeshScene(Array.Empty<ThreeDmSharedMeshGeometry>(), Array.Empty<ThreeDmSharedMeshInstance>()),
            [new ThreeDmRenderCurve(Guid.NewGuid(), ThreeDmRenderCurveKind.Line,
                [new ThreeDmRenderVertex(1000, 2000, 3000), new ThreeDmRenderVertex(1001, 2000, 3000)], false, 0.1)],
            Array.Empty<ThreeDmRenderPointSet>(),
            Array.Empty<ThreeDmRenderDiagnostic>());

        var backend = new WindowsThreeDmRenderingBackend();
        var upload = backend.Project(prepared, new WindowsRenderOrigin(1000, 2000, 3000));

        Assert.Equal(1, backend.ApiVersion);
        Assert.True(backend.Capabilities.SupportsSharedMeshInstances);
        var curve = Assert.Single(upload.Curves);
        Assert.Equal(0f, curve.Points[0].X);
        Assert.Equal(1f, curve.Points[1].X);
    }

    private static ThreeDmSceneObject Instance(Guid id, Guid definitionId, double x) =>
        new(
            id,
            "Instance",
            null,
            ThreeDmGeometryKind.InstanceReference,
            BoundingBox3d.FromPoints(new Point3d(x, 0, 0), new Point3d(x + 1, 1, 0)),
            Geometry: new ThreeDmInstanceReferenceGeometryData(
                definitionId,
                new Transform3d(
                    1, 0, 0, x,
                    0, 1, 0, 0,
                    0, 0, 1, 0,
                    0, 0, 0, 1),
                BoundingBox3d.FromPoints(new Point3d(x, 0, 0), new Point3d(x + 1, 1, 0))));
}
