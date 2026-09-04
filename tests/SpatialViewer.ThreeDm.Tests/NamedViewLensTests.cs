using Rhino.DocObjects;
using Rhino.FileIO;
using SpatialViewer.Formats.ThreeDm.Rhino3dm;
using SpatialViewer.ThreeDm.Core;

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
                Assert.True(view.Viewport.SetFrustum(-2, 2, -1, 1, 5, 500));
                model.AllNamedViews.Add(view);
                Assert.True(model.Write(path, 8));
            }

            var document = await new Rhino3dmThreeDmImporter().ImportAsync(path);
            var viewInfo = Assert.Single(document.NamedViews);
            Assert.Equal(50, viewInfo.Camera35mmLensLength, 8);
            var frustum = Assert.IsType<ThreeDmViewFrustumInfo>(viewInfo.Frustum);
            Assert.Equal(-2, frustum.Left, 8);
            Assert.Equal(2, frustum.Right, 8);
            Assert.Equal(-1, frustum.Bottom, 8);
            Assert.Equal(1, frustum.Top, 8);
            Assert.Equal(5, frustum.Near, 8);
            Assert.Equal(500, frustum.Far, 8);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
