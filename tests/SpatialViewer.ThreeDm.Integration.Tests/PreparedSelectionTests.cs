using SpatialViewer.ThreeDm.Core;
using SpatialViewer.ThreeDm.Integration;
using SpatialViewer.ThreeDm.Rendering;

namespace SpatialViewer.ThreeDm.Integration.Tests;

public sealed class PreparedSelectionTests
{
    [Fact]
    public void PreparedSceneSelectionCombinesSharedMeshesAndCurves()
    {
        var meshId = Guid.NewGuid();
        var curveId = Guid.NewGuid();
        var scene = new ThreeDmPreparedRenderScene(
            ThreeDmRenderDisplayMode.ShadedWithEdges,
            new ThreeDmSharedMeshScene(
                Array.Empty<ThreeDmSharedMeshGeometry>(),
                [new ThreeDmSharedMeshInstance(0, meshId, 2, Identity, Array.Empty<Guid>())]),
            [new ThreeDmRenderCurve(
                curveId,
                ThreeDmRenderCurveKind.Line,
                [new ThreeDmRenderVertex(0, 0, 0), new ThreeDmRenderVertex(1, 0, 0)],
                false,
                0.1)],
            Array.Empty<ThreeDmRenderPointSet>(),
            Array.Empty<ThreeDmRenderDiagnostic>());

        var ids = ThreeDmSelectionCatalog.Create(scene);

        Assert.Equal(2, ids.Count);
        Assert.Contains(ids, item => item.SourceObjectId == meshId && item.SourceSubobjectIndex == 2);
        Assert.Contains(ids, item => item.SourceObjectId == curveId);
    }

    private static readonly Transform3d Identity = new(
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1);
}
