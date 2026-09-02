using SpatialViewer.ThreeDm.Core;
using SpatialViewer.ThreeDm.Rendering;

namespace SpatialViewer.ThreeDm.Rendering.Tests;

public sealed class SharedMeshStatisticsTests
{
    [Fact]
    public void HundredInstancesKeepOneTrianglePayloadInsteadOfHundredCopies()
    {
        var sourceId = Guid.NewGuid();
        var geometry = new ThreeDmSharedMeshGeometry(
            0,
            sourceId,
            null,
            [
                new ThreeDmRenderVertex(0, 0, 0),
                new ThreeDmRenderVertex(1, 0, 0),
                new ThreeDmRenderVertex(0, 1, 0),
            ],
            [0, 1, 2],
            Array.Empty<ThreeDmRenderNormal>(),
            Array.Empty<ThreeDmRenderTextureCoordinate>(),
            BoundingBox3d.FromPoints(new Point3d(0, 0, 0), new Point3d(1, 1, 0)));
        var instances = Enumerable.Range(0, 100)
            .Select(index => new ThreeDmSharedMeshInstance(
                0,
                sourceId,
                null,
                Translation(index, 0, 0),
                [Guid.NewGuid()]))
            .ToArray();
        var scene = new ThreeDmSharedMeshScene([geometry], instances);

        var statistics = ThreeDmSharedMeshSceneStatistics.Measure(scene);

        Assert.Equal(1, statistics.UniqueGeometryCount);
        Assert.Equal(100, statistics.InstanceCount);
        Assert.Equal(3, statistics.UniqueVertexCount);
        Assert.Equal(300, statistics.ExpandedVertexCount);
        Assert.Equal(3, statistics.UniqueIndexCount);
        Assert.Equal(300, statistics.ExpandedIndexCount);
        Assert.Equal(0.01, statistics.VertexReuseRatio, 12);
        Assert.Equal(0.01, statistics.IndexReuseRatio, 12);
    }

    private static Transform3d Translation(double x, double y, double z) =>
        new(
            1, 0, 0, x,
            0, 1, 0, y,
            0, 0, 1, z,
            0, 0, 0, 1);
}
