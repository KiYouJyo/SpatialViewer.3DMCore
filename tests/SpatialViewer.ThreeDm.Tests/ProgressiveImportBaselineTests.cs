using Rhino.FileIO;
using SpatialViewer.Formats.ThreeDm.Rhino3dm;
using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Tests;

public sealed class ProgressiveImportBaselineTests
{
    [Theory]
    [InlineData(8)]
    [InlineData(128)]
    [InlineData(1024)]
    public async Task ProgressiveImportKeepsBatchesBoundedAcrossFixtureSizes(int objectCount)
    {
        var path = Path.Combine(Path.GetTempPath(), $"spatialviewer-phase6-baseline-{objectCount}-{Guid.NewGuid():N}.3dm");
        try
        {
            WritePointFixture(path, objectCount);
            const int batchSize = 32;
            var importer = new Rhino3dmThreeDmImporter();
            var options = new ThreeDmImportOptions
            {
                ProgressiveBatchSize = batchSize,
            };
            var batches = new List<ThreeDmImportObjectBatchUpdate>();
            ThreeDmImportCompletedUpdate? completed = null;

            await foreach (var update in importer.ImportProgressivelyAsync(path, options))
            {
                switch (update)
                {
                    case ThreeDmImportObjectBatchUpdate batch:
                        batches.Add(batch);
                        break;
                    case ThreeDmImportCompletedUpdate completion:
                        completed = completion;
                        break;
                }
            }

            Assert.NotEmpty(batches);
            Assert.All(batches, batch => Assert.InRange(batch.Objects.Count, 1, batchSize));
            Assert.Equal(objectCount, batches.Sum(batch => batch.Objects.Count));
            Assert.Equal((objectCount + batchSize - 1) / batchSize, batches.Count);
            Assert.NotNull(completed);
            Assert.Equal(objectCount, completed.ImportedObjects);
            Assert.Equal(objectCount, completed.TotalObjects);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static void WritePointFixture(string path, int count)
    {
        using var model = new File3dm();
        for (var i = 0; i < count; i++)
        {
            var id = model.Objects.AddPoint(i, i % 17, i % 31);
            Assert.NotEqual(Guid.Empty, id);
        }

        Assert.True(model.Write(path, 8));
    }
}
