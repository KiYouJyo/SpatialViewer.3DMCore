using Rhino.FileIO;
using SpatialViewer.Formats.ThreeDm.Rhino3dm;

namespace SpatialViewer.ThreeDm.Tests;

public sealed class ArchiveCompatibilityTests
{
    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public async Task ImportReadsSupportedLegacyArchiveVersions(int archiveVersion)
    {
        var path = Path.Combine(Path.GetTempPath(), $"spatialviewer-archive-v{archiveVersion}-{Guid.NewGuid():N}.3dm");
        try
        {
            using (var model = new File3dm())
            {
                model.ApplicationName = $"Archive v{archiveVersion}";
                Assert.NotEqual(Guid.Empty, model.Objects.AddPoint(1, 2, 3));
                Assert.True(model.Write(path, archiveVersion));
            }

            var document = await new Rhino3dmThreeDmImporter().ImportAsync(path);

            Assert.NotNull(document.Properties);
            Assert.Equal(archiveVersion * 10, document.Properties.ArchiveVersion);
            Assert.Single(document.Objects);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportRejectsCorruptArchiveWithNormalizedInvalidDataException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"spatialviewer-corrupt-{Guid.NewGuid():N}.3dm");
        try
        {
            await File.WriteAllBytesAsync(path, [0x53, 0x56, 0x33, 0x44, 0x4D, 0x00, 0xFF, 0x01]);

            var error = await Assert.ThrowsAsync<InvalidDataException>(async () =>
                await new Rhino3dmThreeDmImporter().ImportAsync(path));

            Assert.Contains("3DM", error.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
