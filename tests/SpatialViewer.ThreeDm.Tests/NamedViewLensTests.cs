using Rhino.DocObjects;
using Rhino.FileIO;
using SpatialViewer.Formats.ThreeDm.Rhino3dm;

namespace SpatialViewer.ThreeDm.Tests;

public sealed class NamedViewLensTests
{
    [Fact]
    public async Task ImportPreservesNamedView35mmLensLength()
    {
        var path = Path.Combine(Path.GetTempPath(), $"spatialviewer-view-lens-{Guid.NewGuid():N}.3dm");
        try
        {
            using (var model = new File3dm())
            {
                var view = new ViewInfo { Name = "Lens View" };
                view.Viewport.Camera35mmLensLength = 50;
                model.AllNamedViews.Add(view);
                Assert.True(model.Write(path, 8));
            }

            var document = await new Rhino3dmThreeDmImporter().ImportAsync(path);
            var viewInfo = Assert.Single(document.NamedViews);
            Assert.Equal(50, viewInfo.Camera35mmLensLength, 8);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
