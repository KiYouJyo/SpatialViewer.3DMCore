using SpatialViewer.ThreeDm.Core;
using SpatialViewer.ThreeDm.Integration;

namespace SpatialViewer.ThreeDm.Integration.Tests;

public sealed class IntegrationContractTests
{
    [Fact]
    public async Task SessionOpenCloseOwnsDocumentLifecycle()
    {
        var document = CreateDocument(parentVisible: true);
        await using var session = new ThreeDmSession(new ImmediateImporter(document));

        var opened = await session.OpenAsync("model.3dm");

        Assert.Same(document, opened);
        Assert.Equal(ThreeDmSessionState.Open, session.State);
        Assert.Same(document, session.Document);
        Assert.Equal(document.Bounds, session.ModelBounds);
        Assert.Null(session.LastError);

        await session.CloseAsync();

        Assert.Equal(ThreeDmSessionState.Closed, session.State);
        Assert.Null(session.Document);
        Assert.Null(session.ModelBounds);
    }

    [Fact]
    public async Task CancelOpenReturnsSessionToClosedState()
    {
        var importer = new BlockingImporter(CreateDocument(parentVisible: true));
        await using var session = new ThreeDmSession(importer);

        var opening = session.OpenAsync("slow.3dm");
        await importer.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(ThreeDmSessionState.Opening, session.State);

        Assert.True(session.CancelOpen());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => opening);

        Assert.Equal(ThreeDmSessionState.Closed, session.State);
        Assert.Null(session.Document);
        Assert.False(session.CancelOpen());
    }

    [Fact]
    public async Task LayerOverridesCanRevealAndHideGeometryWithoutReimport()
    {
        var document = CreateDocument(parentVisible: false);
        await using var session = new ThreeDmSession(new ImmediateImporter(document));
        await session.OpenAsync("layers.3dm");

        var initialTree = session.GetLayerTree();
        var parent = Assert.Single(initialTree);
        var child = Assert.Single(parent.Children);
        Assert.False(parent.EffectiveVisible);
        Assert.False(child.EffectiveVisible);
        Assert.Empty(session.BuildVisualScene().Meshes);

        session.SetLayerVisibility(parent.Id, true);
        var revealedTree = session.GetLayerTree();
        Assert.True(Assert.Single(revealedTree).EffectiveVisible);
        Assert.True(Assert.Single(Assert.Single(revealedTree).Children).EffectiveVisible);
        Assert.Single(session.BuildVisualScene().Meshes);

        session.SetLayerVisibility(child.Id, false);
        Assert.Empty(session.BuildVisualScene().Meshes);

        session.SetLayerVisibility(child.Id, null);
        Assert.Single(session.BuildVisualScene().Meshes);
        Assert.Single(session.LayerVisibilityOverrides);
    }

    [Fact]
    public async Task CameraFitUsesDocumentBoundsAndProducesFinitePlanes()
    {
        var document = CreateDocument(parentVisible: true);
        await using var session = new ThreeDmSession(new ImmediateImporter(document));
        await session.OpenAsync("camera.3dm");

        var fit = session.GetCameraFit(new ThreeDmCameraFitOptions
        {
            AspectRatio = 1,
            VerticalFieldOfViewRadians = Math.PI / 3,
            Padding = 1.05,
        });

        Assert.Equal(0.5, fit.Target.X, 12);
        Assert.Equal(0.5, fit.Target.Y, 12);
        Assert.Equal(0, fit.Target.Z, 12);
        Assert.True(double.IsFinite(fit.CameraDistance));
        Assert.True(fit.CameraDistance > fit.BoundingRadius);
        Assert.True(fit.NearPlaneDistance > 0);
        Assert.True(fit.FarPlaneDistance > fit.NearPlaneDistance);
    }

    [Fact]
    public async Task SelectionIdsResolveStableSourceProperties()
    {
        var document = CreateDocument(parentVisible: true);
        await using var session = new ThreeDmSession(new ImmediateImporter(document));
        await session.OpenAsync("selection.3dm");

        var scene = session.BuildVisualScene();
        var selection = Assert.Single(session.GetSelectionIds(scene));
        var properties = Assert.IsType<ThreeDmSelectionProperties>(session.GetSelectionProperties(selection));

        var source = Assert.Single(document.Objects);
        Assert.Equal(source.Id, selection.SourceObjectId);
        Assert.Equal("Triangle", properties.Name);
        Assert.Equal(ThreeDmGeometryKind.Mesh, properties.GeometryKind);
        Assert.Equal("Child", properties.LayerName);
        Assert.Equal(source.Bounds, properties.Bounds);
        Assert.True(properties.SourceVisible);
        Assert.Empty(properties.InstancePath);
    }

    [Fact]
    public void IntegrationContractHasStableApiVersion()
    {
        Assert.Equal(1, ThreeDmIntegrationContract.ApiVersion);
        Assert.True(ThreeDmIntegrationContract.AssemblyVersion.Major >= 0);
    }

    private static ThreeDmSceneDocument CreateDocument(bool parentVisible)
    {
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var objectId = Guid.NewGuid();
        var bounds = BoundingBox3d.FromPoints(new Point3d(0, 0, 0), new Point3d(1, 1, 0));
        var geometry = new ThreeDmMeshGeometryData(
            [new Point3d(0, 0, 0), new Point3d(1, 0, 0), new Point3d(0, 1, 0)],
            [new ThreeDmMeshFace(0, 1, 2, null)],
            [new Vector3d(0, 0, 1), new Vector3d(0, 0, 1), new Vector3d(0, 0, 1)],
            Array.Empty<ThreeDmTextureCoordinate>(),
            false,
            bounds);
        var sceneObject = new ThreeDmSceneObject(
            objectId,
            "Triangle",
            childId,
            ThreeDmGeometryKind.Mesh,
            bounds,
            null,
            true,
            0xFF336699,
            "ColorFromLayer",
            "MaterialFromLayer",
            geometry)
        {
            SourceObjectVisible = true,
        };

        return new ThreeDmSceneDocument(
            "fixture.3dm",
            [sceneObject],
            bounds,
            Array.Empty<ThreeDmImportDiagnostic>())
        {
            Layers =
            [
                new ThreeDmLayerInfo(parentId, "Parent", null, parentVisible, false, 0xFFFFFFFF, 0),
                new ThreeDmLayerInfo(childId, "Child", parentId, true, false, 0xFF336699, 0),
            ],
        };
    }

    private sealed class ImmediateImporter(ThreeDmSceneDocument document) : IThreeDmImporter
    {
        public bool CanImport(string path) => true;

        public ValueTask<ThreeDmSceneDocument> ImportAsync(
            string path,
            ThreeDmImportOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(document);
        }
    }

    private sealed class BlockingImporter(ThreeDmSceneDocument document) : IThreeDmImporter
    {
        public TaskCompletionSource<bool> Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool CanImport(string path) => true;

        public async ValueTask<ThreeDmSceneDocument> ImportAsync(
            string path,
            ThreeDmImportOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Started.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return document;
        }
    }
}
