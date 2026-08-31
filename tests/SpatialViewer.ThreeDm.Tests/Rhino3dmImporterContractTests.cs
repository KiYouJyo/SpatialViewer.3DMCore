using SpatialViewer.Formats.ThreeDm.Rhino3dm;

namespace SpatialViewer.ThreeDm.Tests;

public sealed class Rhino3dmImporterContractTests
{
    [Theory]
    [InlineData("model.3dm")]
    [InlineData("MODEL.3DM")]
    public void CanImportRecognizesThreeDmFiles(string path)
    {
        var importer = new Rhino3dmThreeDmImporter();

        Assert.True(importer.CanImport(path));
        Assert.False(importer.CanImport("model.dwg"));
    }
}
