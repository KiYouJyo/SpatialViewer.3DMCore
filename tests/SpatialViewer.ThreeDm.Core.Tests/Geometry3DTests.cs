using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Core.Tests;

public sealed class Geometry3DTests
{
    [Fact]
    public void FromPointsNormalizesBounds()
    {
        BoundingBox3d bounds = BoundingBox3d.FromPoints(
            new Point3d(10, -2, 5),
            new Point3d(-4, 8, 1));

        Assert.True(bounds.IsValid);
        Assert.Equal(new Point3d(-4, -2, 1), bounds.Min);
        Assert.Equal(new Point3d(10, 8, 5), bounds.Max);
    }
}
