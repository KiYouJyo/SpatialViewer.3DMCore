using Rhino.DocObjects;
using Rhino.FileIO;
using SpatialViewer.Formats.ThreeDm.Rhino3dm;
using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Tests;

public sealed class NamedViewLensTests
{
    [Fact]
    public async Task ImportPreservesNamedViewLensAndFrustumAsStoredByRhino()
    {
        var path = Path.Combine(Path.GetTempPath(), $"spatialviewer-view-lens-{Guid.NewGuid():N}.3dm");
        double expectedLens;
        double expectedLeft;
        double expectedRight;
        double expectedBottom;
        double expectedTop;
        double expectedNear;
        double expectedFar;

        try
        {
            using (var model = new File3dm())
            {
                var view = new ViewInfo { Name = "Lens View" };
                Assert.True(view.Viewport.SetFrustum(-2, 2, -1, 1, 5, 500));
                view.Viewport.Camera35mmLensLength = 50;
                expectedLens = view.Viewport.Camera35mmLensLength;
                Assert.True(view.Viewport.GetFrustum(
                    out expectedLeft,
                    out expectedRight,
                    out expectedBottom,
                    out expectedTop,
                    out expectedNear,
                    out expectedFar));

                model.AllNamedViews.Add(view);
                Assert.True(model.Write(path, 8));
            }

            var document = await new Rhino3dmThreeDmImporter().ImportAsync(path);
            var viewInfo = Assert.Single(document.NamedViews);
            Assert.Equal(expectedLens, viewInfo.Camera35mmLensLength, 8);
            var frustum = Assert.IsType<ThreeDmViewFrustumInfo>(viewInfo.Frustum);
            Assert.Equal(expectedLeft, frustum.Left, 8);
            Assert.Equal(expectedRight, frustum.Right, 8);
            Assert.Equal(expectedBottom, frustum.Bottom, 8);
            Assert.Equal(expectedTop, frustum.Top, 8);
            Assert.Equal(expectedNear, frustum.Near, 8);
            Assert.Equal(expectedFar, frustum.Far, 8);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
