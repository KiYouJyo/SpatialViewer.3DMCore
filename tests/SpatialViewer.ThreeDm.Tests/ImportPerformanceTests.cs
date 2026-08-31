using Rhino.FileIO;
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
