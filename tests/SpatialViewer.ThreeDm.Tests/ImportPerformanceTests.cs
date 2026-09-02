using Rhino.FileIO;
using Rhino.Geometry;
using SpatialViewer.Formats.ThreeDm.Rhino3dm;
using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Tests;

public sealed class ImportPerformanceTests
{
    [Fact]
    public async Task ImportAsyncReportsDeterministicProgressStages()
    {
        var path = Path.Combine(Path.GetTempPath(), $"spatialviewer-phase6-progress-{Guid.NewGuid():N}.3dm");
        try
        {
            WritePointFixture(path, 3);
            var progress = new CollectingProgress();
            var options = new ThreeDmImportOptions
            {
                ProgressIntervalObjects = 1,
            };
            var importer = new Rhino3dmThreeDmImporter();

            var document = await importer.ImportAsync(path, options, progress);

            Assert.Equal(3, document.Objects.Count);
            Assert.NotEmpty(progress.Values);
            Assert.Equal(ThreeDmImportStage.ReadingArchive, progress.Values[0].Stage);
            Assert.Contains(progress.Values, item => item.Stage == ThreeDmImportStage.ReadingDocumentTables);
            Assert.Contains(progress.Values, item =>
                item.Stage == ThreeDmImportStage.ConvertingObjects && item.ProcessedObjects == 1 && item.TotalObjects == 3);
            Assert.Contains(progress.Values, item =>
                item.Stage == ThreeDmImportStage.ConvertingObjects && item.ProcessedObjects == 3 && item.TotalObjects == 3);
            var completed = progress.Values[^1];
            Assert.Equal(ThreeDmImportStage.Completed, completed.Stage);
            Assert.Equal(3, completed.ProcessedObjects);
            Assert.Equal(3, completed.TotalObjects);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public async Task ProgressiveImportEmitsHeaderThenBoundedObjectBatchesAndCompletion()
    {
        var path = Path.Combine(Path.GetTempPath(), $"spatialviewer-phase6-progressive-{Guid.NewGuid():N}.3dm");
        try
        {
            WritePointFixture(path, 5);
            var options = new ThreeDmImportOptions
            {
                ProgressiveBatchSize = 2,
            };
            var importer = new Rhino3dmThreeDmImporter();
            var updates = new List<ThreeDmProgressiveImportUpdate>();

            await foreach (var update in importer.ImportProgressivelyAsync(path, options))
            {
                updates.Add(update);
            }

            var header = Assert.IsType<ThreeDmImportHeaderUpdate>(updates[0]);
            Assert.Equal(5, header.TotalObjects);
            Assert.Equal(Path.GetFullPath(path), header.SourcePath);

            var batches = updates.OfType<ThreeDmImportObjectBatchUpdate>().ToArray();
            Assert.Equal(3, batches.Length);
            Assert.Equal([2, 2, 1], batches.Select(item => item.Objects.Count).ToArray());
            Assert.Equal([2, 4, 5], batches.Select(item => item.ProcessedObjects).ToArray());
            Assert.All(batches, item => Assert.Equal(5, item.TotalObjects));
            Assert.True(batches[^1].CumulativeBounds.IsValid);

            var completed = Assert.IsType<ThreeDmImportCompletedUpdate>(updates[^1]);
            Assert.Equal(5, completed.ImportedObjects);
            Assert.Equal(5, completed.TotalObjects);
            Assert.True(completed.Bounds.IsValid);
            Assert.Empty(completed.Diagnostics);

            var standard = await importer.ImportAsync(path, options);
            Assert.Equal(
                standard.Objects.Select(item => item.Id).Order().ToArray(),
                batches.SelectMany(item => item.Objects).Select(item => item.Id).Order().ToArray());
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public async Task ImportAsyncRejectsOversizedFileBeforeRhinoRead()
    {
        var path = Path.Combine(Path.GetTempPath(), $"spatialviewer-phase6-size-{Guid.NewGuid():N}.3dm");
        try
        {
            await File.WriteAllBytesAsync(path, new byte[64]);
            var options = new ThreeDmImportOptions
            {
                Limits = ThreeDmImportLimits.Default with { MaxFileSizeBytes = 32 },
            };
            var importer = new Rhino3dmThreeDmImporter();

            var exception = await Assert.ThrowsAsync<InvalidDataException>(async () =>
                await importer.ImportAsync(path, options));

            Assert.Contains("file size", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public async Task ImportAsyncRejectsObjectCountAboveConfiguredLimit()
    {
        var path = Path.Combine(Path.GetTempPath(), $"spatialviewer-phase6-count-{Guid.NewGuid():N}.3dm");
        try
        {
            WritePointFixture(path, 2);
            var options = new ThreeDmImportOptions
            {
                Limits = ThreeDmImportLimits.Default with { MaxObjectCount = 1 },
            };
            var importer = new Rhino3dmThreeDmImporter();

            var exception = await Assert.ThrowsAsync<InvalidDataException>(async () =>
                await importer.ImportAsync(path, options));

            Assert.Contains("object count", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public async Task ImportAsyncRejectsPointCloudAboveConfiguredPointLimit()
    {
        var path = Path.Combine(Path.GetTempPath(), $"spatialviewer-phase6-pointcloud-limit-{Guid.NewGuid():N}.3dm");
        try
        {
            using var model = new File3dm();
            var pointCloud = new PointCloud();
            pointCloud.Add(0, 0, 0);
            pointCloud.Add(1, 0, 0);
            pointCloud.Add(2, 0, 0);
            pointCloud.Add(3, 0, 0);
            Assert.NotEqual(Guid.Empty, model.Objects.AddPointCloud(pointCloud));
            Assert.True(model.Write(path, 8));

            var options = new ThreeDmImportOptions
            {
                Limits = ThreeDmImportLimits.Default with
                {
                    Geometry = ThreeDmGeometryLimits.Default with { MaxPointCloudPoints = 3 },
                },
            };
            var importer = new Rhino3dmThreeDmImporter();

            var exception = await Assert.ThrowsAsync<InvalidDataException>(async () =>
                await importer.ImportAsync(path, options));

            Assert.Contains("PointCloud point count", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public async Task ImportAsyncRejectsMeshAboveConfiguredVertexLimit()
    {
        var path = Path.Combine(Path.GetTempPath(), $"spatialviewer-phase6-mesh-limit-{Guid.NewGuid():N}.3dm");
        try
        {
            using var model = new File3dm();
            var mesh = new Mesh();
            mesh.Vertices.Add(0, 0, 0);
            mesh.Vertices.Add(1, 0, 0);
            mesh.Vertices.Add(0, 1, 0);
            mesh.Faces.AddFace(0, 1, 2);
            Assert.NotEqual(Guid.Empty, model.Objects.AddMesh(mesh));
            Assert.True(model.Write(path, 8));

            var options = new ThreeDmImportOptions
            {
                Limits = ThreeDmImportLimits.Default with
                {
                    Geometry = ThreeDmGeometryLimits.Default with { MaxMeshVertices = 2 },
                },
            };
            var importer = new Rhino3dmThreeDmImporter();

            var exception = await Assert.ThrowsAsync<InvalidDataException>(async () =>
                await importer.ImportAsync(path, options));

            Assert.Contains("Mesh vertex count", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public async Task ImportAsyncCanCancelDuringObjectConversion()
    {
        var path = Path.Combine(Path.GetTempPath(), $"spatialviewer-phase6-cancel-{Guid.NewGuid():N}.3dm");
        try
        {
            WritePointFixture(path, 64);
            using var cancellation = new CancellationTokenSource();
            var progress = new CancelingProgress(cancellation);
            var options = new ThreeDmImportOptions
            {
                ProgressIntervalObjects = 1,
            };
            var importer = new Rhino3dmThreeDmImporter();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await importer.ImportAsync(path, options, progress, cancellation.Token));

            Assert.True(progress.CancellationRequestedAfterObject);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    private static void WritePointFixture(string path, int count)
    {
        using var model = new File3dm();
        for (var i = 0; i < count; i++)
        {
            Assert.NotEqual(Guid.Empty, model.Objects.AddPoint(i, i * 2, i * 3));
        }

        Assert.True(model.Write(path, 8));
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private sealed class CollectingProgress : IProgress<ThreeDmImportProgress>
    {
        public List<ThreeDmImportProgress> Values { get; } = [];

        public void Report(ThreeDmImportProgress value) => Values.Add(value);
    }

    private sealed class CancelingProgress(CancellationTokenSource cancellation) : IProgress<ThreeDmImportProgress>
    {
        public bool CancellationRequestedAfterObject { get; private set; }

        public void Report(ThreeDmImportProgress value)
        {
            if (value.Stage != ThreeDmImportStage.ConvertingObjects || value.ProcessedObjects < 1)
            {
                return;
            }

            CancellationRequestedAfterObject = true;
            cancellation.Cancel();
        }
    }
}
