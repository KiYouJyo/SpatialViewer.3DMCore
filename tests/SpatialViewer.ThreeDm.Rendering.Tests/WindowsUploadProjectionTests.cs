using SpatialViewer.ThreeDm.Rendering;
using SpatialViewer.ThreeDm.Rendering.Windows;

namespace SpatialViewer.ThreeDm.Rendering.Tests;

public sealed class WindowsUploadProjectionTests
{
    [Fact]
    public void ProjectRebasesLargeWorldCoordinatesBeforeFloatConversion()
    {
        var sourceId = Guid.NewGuid();
        var scene = new ThreeDmRenderScene(
        [
            new ThreeDmRenderMesh(
                sourceId,
                [
                    new ThreeDmRenderVertex(1_000_000_000.125, 2_000_000_000.25, 3_000_000_000.5),
                    new ThreeDmRenderVertex(1_000_000_010.125, 2_000_000_000.25, 3_000_000_000.5),
                    new ThreeDmRenderVertex(1_000_000_000.125, 2_000_000_020.25, 3_000_000_000.5),
                ],
                [0, 1, 2])
            {
                Normals =
                [
                    new ThreeDmRenderNormal(0, 0, 1),
                    new ThreeDmRenderNormal(0, 0, 1),
                    new ThreeDmRenderNormal(0, 0, 1),
                ],
            },
        ]);

        var upload = WindowsThreeDmUploadProjection.Project(scene);

        var mesh = Assert.Single(upload.Meshes);
        Assert.Equal(sourceId, mesh.SourceObjectId);
        Assert.Equal(1_000_000_005.125, upload.Origin.X, 6);
        Assert.Equal(2_000_000_010.25, upload.Origin.Y, 6);
        Assert.Equal(3_000_000_000.5, upload.Origin.Z, 6);
        Assert.Equal(-5f, mesh.Vertices[0].X);
        Assert.Equal(-10f, mesh.Vertices[0].Y);
        Assert.Equal(0f, mesh.Vertices[0].Z);
        Assert.Equal(5f, mesh.Vertices[1].X);
        Assert.Equal(10f, mesh.Vertices[2].Y);
        Assert.Equal(3, mesh.Normals.Count);
    }
}
