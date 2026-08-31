using SpatialViewer.ThreeDm.Rendering;

namespace SpatialViewer.ThreeDm.Rendering.Tests;

public sealed class RenderSceneTests
{
    [Fact]
    public void RenderMeshKeepsSourceObjectIdentity()
    {
        Guid sourceId = Guid.NewGuid();
        var mesh = new ThreeDmRenderMesh(
            sourceId,
            [new ThreeDmRenderVertex(0, 0, 0)],
            [0]);

        Assert.Equal(sourceId, mesh.SourceObjectId);
    }
}
