using SpatialViewer.ThreeDm.Core;
using SpatialViewer.ThreeDm.Rendering;
using SpatialViewer.ThreeDm.Rendering.Windows;

namespace SpatialViewer.ThreeDm.Rendering.Tests;

public sealed class WindowsSharedUploadProjectionTests
{
    [Fact]
    public void ProjectionUploadsGeometryOnceForTwoInstances()
    {
        const double baseX = 1000;
        var sourceId = Guid.NewGuid();
        var geometry = new ThreeDmSharedMeshGeometry(
            0,
            sourceId,
            null,
            [
                new ThreeDmRenderVertex(baseX, 0, 0),
                new ThreeDmRenderVertex(baseX + 1, 0, 0),
                new ThreeDmRenderVertex(baseX, 1, 0),
            ],
            [0, 1, 2],
            [
                new ThreeDmRenderNormal(0, 0, 1),
                new ThreeDmRenderNormal(0, 0, 1),
                new ThreeDmRenderNormal(0, 0, 1),
            ],
            Array.Empty<ThreeDmRenderTextureCoordinate>(),
            BoundingBox3d.FromPoints(
                new Point3d(baseX, 0, 0),
                new Point3d(baseX + 1, 1, 0)));
        var scene = new ThreeDmSharedMeshScene(
            [geometry],
            [
                new ThreeDmSharedMeshInstance(0, sourceId, null, Translation(10, 0, 0), [Guid.NewGuid()]),
                new ThreeDmSharedMeshInstance(0, sourceId, null, Translation(20, 0, 0), [Guid.NewGuid()]),
            ]);

        var upload = WindowsThreeDmSharedUploadProjection.Project(scene);

        var uploadedGeometry = Assert.Single(upload.Geometries);
        Assert.Equal(3, uploadedGeometry.Vertices.Count);
        Assert.InRange(Math.Abs(uploadedGeometry.Vertices[0].X), 0, 1);
        Assert.InRange(Math.Abs(uploadedGeometry.Vertices[1].X), 0, 1);
        Assert.Equal(2, upload.Instances.Count);
        Assert.All(upload.Instances, instance => Assert.Equal(0, instance.GeometryIndex));
        Assert.InRange(Math.Abs(upload.Instances[0].Transform.M03), 0, 6);
        Assert.InRange(Math.Abs(upload.Instances[1].Transform.M03), 0, 6);
        Assert.True(Math.Abs(upload.Origin.X - (baseX + 15.5)) < 1e-6);
    }

    private static Transform3d Translation(double x, double y, double z) =>
        new(
            1, 0, 0, x,
            0, 1, 0, y,
            0, 0, 1, z,
            0, 0, 0, 1);
    [Fact]
    public void SharedUploadProvidesDeduplicatedWireIndicesForEdgeDisplayModes()
    {
        var geometry = new ThreeDmSharedMeshGeometry(
            0,
            Guid.NewGuid(),
            null,
            [
                new ThreeDmRenderVertex(0, 0, 0),
                new ThreeDmRenderVertex(1, 0, 0),
                new ThreeDmRenderVertex(1, 1, 0),
                new ThreeDmRenderVertex(0, 1, 0),
            ],
            [0, 1, 2, 0, 2, 3],
            Array.Empty<ThreeDmRenderNormal>(),
            Array.Empty<ThreeDmRenderTextureCoordinate>(),
            BoundingBox3d.FromPoints(new Point3d(0, 0, 0), new Point3d(1, 1, 0)));
        var scene = new ThreeDmSharedMeshScene(
            [geometry],
            [new ThreeDmSharedMeshInstance(0, geometry.SourceObjectId, null, Identity, Array.Empty<Guid>())]);

        var upload = WindowsThreeDmSharedUploadProjection.Project(scene);
        var projected = Assert.Single(upload.Geometries);

        Assert.Equal(10, projected.WireIndices.Count);
        Assert.Equal(5, projected.WireIndices.Count / 2);
    }

    private static readonly Transform3d Identity = new(
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1);
}
