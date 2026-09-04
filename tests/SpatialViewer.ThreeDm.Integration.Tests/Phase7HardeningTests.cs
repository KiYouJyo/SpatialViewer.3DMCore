using SpatialViewer.ThreeDm.Core;
using SpatialViewer.ThreeDm.Integration;

namespace SpatialViewer.ThreeDm.Integration.Tests;

public sealed class Phase7HardeningTests
{
    [Fact]
    public void HostContractDefinesOneXCompatibilityWindow()
    {
        Assert.Equal("SpatialViewer.ThreeDmHost", ThreeDmIntegrationContract.Name);
        Assert.Equal(1, ThreeDmIntegrationContract.ApiVersion);
        Assert.Equal(new Version(1, 0, 0), ThreeDmIntegrationContract.ContractVersion);
        Assert.True(ThreeDmIntegrationContract.SupportsHost(new Version(1, 0, 0)));
        Assert.True(ThreeDmIntegrationContract.SupportsHost(new Version(1, 9, 9)));
        Assert.False(ThreeDmIntegrationContract.SupportsHost(new Version(2, 0, 0)));
    }

    [Fact]
    public async Task SessionRejectsPathUnsupportedByInjectedImporter()
    {
        await using var session = new ThreeDmSession(new RejectingImporter());

        await Assert.ThrowsAsync<NotSupportedException>(() => session.OpenAsync("model.dwg"));

        Assert.Equal(ThreeDmSessionState.Closed, session.State);
        Assert.Null(session.Document);
        Assert.Null(session.SourcePath);
    }

    [Fact]
    public void LayerTreeKeepsPureParentCyclesInspectableAndHidden()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var document = new ThreeDmSceneDocument(
            "cycle.3dm",
            Array.Empty<ThreeDmSceneObject>(),
            BoundingBox3d.Invalid,
            Array.Empty<ThreeDmImportDiagnostic>())
        {
            Layers =
            [
                new ThreeDmLayerInfo(firstId, "A", secondId, true, false, 0xFFFFFFFF, 0),
                new ThreeDmLayerInfo(secondId, "B", firstId, true, false, 0xFFFFFFFF, 0),
            ],
        };

        var roots = ThreeDmLayerTreeBuilder.Build(document);

        var root = Assert.Single(roots);
        Assert.Equal(firstId, root.Id);
        Assert.False(root.EffectiveVisible);
        var child = Assert.Single(root.Children);
        Assert.Equal(secondId, child.Id);
        Assert.False(child.EffectiveVisible);
    }

    private sealed class RejectingImporter : IThreeDmImporter
    {
        public bool CanImport(string path) => false;

        public ValueTask<ThreeDmSceneDocument> ImportAsync(
            string path,
            ThreeDmImportOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Unsupported paths must be rejected before import.");
    }
}
