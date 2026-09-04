using SpatialViewer.ThreeDm.Core;
using SpatialViewer.ThreeDm.Integration;
using SpatialViewer.ThreeDm.Rendering;

namespace SpatialViewer.ThreeDm.Integration.Tests;

public sealed class SharedSelectionContractTests
{
    [Fact]
    public void SharedMeshInstancesPreserveSelectionIdentityAndInstancePath()
    {
        var sourceId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();
        var scene = new ThreeDmSharedMeshScene(
            Array.Empty<ThreeDmSharedMeshGeometry>(),
            [
                new ThreeDmSharedMeshInstance(
                    0,
                    sourceId,
                    7,
                    Identity,
                    [instanceId]),
            ]);

        var selection = Assert.Single(ThreeDmSelectionCatalog.Create(scene));

        Assert.Equal(sourceId, selection.SourceObjectId);
        Assert.Equal(7, selection.SourceSubobjectIndex);
        Assert.Equal(instanceId.ToString("N"), selection.InstancePathKey);
    }

    private static readonly Transform3d Identity = new(
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1);
}
