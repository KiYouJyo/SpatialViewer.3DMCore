using SpatialViewer.ThreeDm.Core;
using SpatialViewer.ThreeDm.Integration;

namespace SpatialViewer.ThreeDm.Integration.Tests;

public sealed class ProductHostCompletionTests
{
    [Fact]
    public async Task ProgressiveOpenPublishesHeaderAndBatchesBeforeFinalDocument()
    {
        var document = Fixture();
        await using var session = new ThreeDmSession(new ProgressiveImporter(document));
        var updates = new List<ThreeDmProgressiveImportUpdate>();

        var opened = await session.OpenProgressivelyAsync(
            "model.3dm",
            (update, _) =>
            {
                updates.Add(update);
                return ValueTask.CompletedTask;
            });

        Assert.Equal(ThreeDmSessionState.Open, session.State);
        Assert.Same(opened, session.Document);
        Assert.IsType<ThreeDmImportHeaderUpdate>(updates[0]);
        Assert.Contains(updates, item => item is ThreeDmImportObjectBatchUpdate);
        Assert.IsType<ThreeDmImportCompletedUpdate>(updates[^1]);
        Assert.Single(opened.Objects);
    }

    [Fact]
    public async Task StandardAndNamedViewsExposePerspectiveAndOrthographicCameraContracts()
    {
        var document = Fixture() with
        {
            NamedViews =
            [
                new ThreeDmNamedViewInfo(
                    "Saved",
                    new Point3d(5, 5, 5),
                    new Vector3d(-1, -1, -1),
                    new Vector3d(0, 0, 1),
                    new Point3d(0.5, 0.5, 0),
                    true),
            ],
        };
        await using var session = new ThreeDmSession(new ImmediateImporter(document));
        await session.OpenAsync("views.3dm");

        var named = Assert.Single(session.GetNamedViewPresets());
        Assert.True(named.IsNamedView);
        Assert.Equal(ThreeDmCameraProjection.Perspective, named.Camera.Projection);

        var standard = session.GetStandardViewPresets();
        Assert.Equal(4, standard.Count);
        Assert.Contains(standard, item => item.Key == "standard:top" && item.Camera.Projection == ThreeDmCameraProjection.Orthographic);
    }

    [Fact]
    public async Task InspectionProvidesDocumentAndGeometryDetails()
    {
        var document = Fixture();
        await using var session = new ThreeDmSession(new ImmediateImporter(document));
        await session.OpenAsync("inspect.3dm");

        var summary = session.GetDocumentSummary();
        Assert.Equal(1, summary.ObjectCount);

        var scene = session.BuildVisualScene();
        var id = Assert.Single(session.GetSelectionIds(scene));
        var properties = Assert.IsType<ThreeDmSelectionProperties>(session.GetSelectionProperties(id));
        Assert.Equal("3", properties.GeometryDetails["VertexCount"]);
        Assert.Equal("1", properties.GeometryDetails["FaceCount"]);
    }

    private static ThreeDmSceneDocument Fixture()
    {
        var objectId = Guid.NewGuid();
        var bounds = BoundingBox3d.FromPoints(new Point3d(0, 0, 0), new Point3d(1, 1, 0));
        var mesh = new ThreeDmMeshGeometryData(
            [new Point3d(0, 0, 0), new Point3d(1, 0, 0), new Point3d(0, 1, 0)],
            [new ThreeDmMeshFace(0, 1, 2)],
            [new Vector3d(0, 0, 1), new Vector3d(0, 0, 1), new Vector3d(0, 0, 1)],
            Array.Empty<ThreeDmTextureCoordinate>(),
            false,
            bounds);
        return new ThreeDmSceneDocument(
            "fixture.3dm",
            [new ThreeDmSceneObject(objectId, "Mesh", null, ThreeDmGeometryKind.Mesh, bounds, Geometry: mesh)],
            bounds,
            Array.Empty<ThreeDmImportDiagnostic>())
        {
            Properties = new ThreeDmDocumentProperties(8, null, null, null, null, null, 1, "Millimeters", 0.01, 0.01, 0.01),
        };
    }

    private sealed class ImmediateImporter(ThreeDmSceneDocument document) : IThreeDmImporter
    {
        public bool CanImport(string path) => true;
        public ValueTask<ThreeDmSceneDocument> ImportAsync(string path, ThreeDmImportOptions? options = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(document);
    }

    private sealed class ProgressiveImporter(ThreeDmSceneDocument document) : IThreeDmProgressiveImporter
    {
        public bool CanImport(string path) => true;
        public ValueTask<ThreeDmSceneDocument> ImportAsync(string path, ThreeDmImportOptions? options = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(document);
        public ValueTask<ThreeDmSceneDocument> ImportAsync(string path, ThreeDmImportOptions? options, IProgress<ThreeDmImportProgress>? progress, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(document);

        public async IAsyncEnumerable<ThreeDmProgressiveImportUpdate> ImportProgressivelyAsync(
            string path,
            ThreeDmImportOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var properties = document.Properties ?? throw new InvalidOperationException();
            yield return new ThreeDmImportHeaderUpdate(
                path,
                properties,
                document.Layers,
                document.Materials,
                document.NamedViews,
                document.InstanceDefinitions,
                document.Objects.Count);
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ThreeDmImportObjectBatchUpdate(
                document.Objects,
                document.Bounds,
                Array.Empty<ThreeDmImportDiagnostic>(),
                document.Objects.Count,
                document.Objects.Count);
            yield return new ThreeDmImportCompletedUpdate(
                document.Bounds,
                document.Diagnostics,
                document.Objects.Count,
                document.Objects.Count);
        }
    }
}
