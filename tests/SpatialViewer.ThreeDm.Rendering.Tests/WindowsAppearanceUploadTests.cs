using SpatialViewer.ThreeDm.Rendering;
using SpatialViewer.ThreeDm.Rendering.Windows;

namespace SpatialViewer.ThreeDm.Rendering.Tests;

public sealed class WindowsAppearanceUploadTests
{
    [Fact]
    public void ProjectionCarriesResolvedAppearanceForMeshesCurvesAndPoints()
    {
        var materialId = Guid.NewGuid();
        var appearance = new ThreeDmRenderAppearance(
            0xCC336699,
            0.5,
            materialId,
            0xFFFFFFFF,
            0xFF010203,
            42,
            0.25);
        var sourceId = Guid.NewGuid();
        var mesh = new ThreeDmRenderMesh(
            sourceId,
            [new ThreeDmRenderVertex(0, 0, 0), new ThreeDmRenderVertex(1, 0, 0), new ThreeDmRenderVertex(0, 1, 0)],
            [0, 1, 2])
        {
            Appearance = appearance,
            MaterialId = materialId,
            ColorArgb = appearance.ColorArgb,
        };
        var curve = new ThreeDmRenderCurve(
            sourceId,
            ThreeDmRenderCurveKind.Line,
            [new ThreeDmRenderVertex(0, 0, 0), new ThreeDmRenderVertex(1, 0, 0)],
            false,
            0)
        {
            Appearance = appearance,
        };
        var pointSet = new ThreeDmRenderPointSet(sourceId, [new ThreeDmRenderVertex(0, 0, 0)])
        {
            Appearance = appearance,
        };
        var scene = new ThreeDmRenderScene([mesh])
        {
            Curves = [curve],
            PointSets = [pointSet],
        };

        var upload = WindowsThreeDmUploadProjection.Project(scene, new WindowsRenderOrigin(0, 0, 0));

        AssertAppearance(Assert.Single(upload.Meshes).Appearance, appearance);
        AssertAppearance(Assert.Single(upload.Curves).Appearance, appearance);
        AssertAppearance(Assert.Single(upload.PointSets).Appearance, appearance);
    }

    private static void AssertAppearance(WindowsRenderAppearance actual, ThreeDmRenderAppearance expected)
    {
        Assert.Equal(expected.ColorArgb, actual.ColorArgb);
        Assert.Equal((float)expected.Opacity, actual.Opacity);
        Assert.Equal(expected.MaterialId, actual.MaterialId);
        Assert.Equal(expected.SpecularColorArgb, actual.SpecularColorArgb);
        Assert.Equal(expected.EmissionColorArgb, actual.EmissionColorArgb);
        Assert.Equal((float)expected.Shine, actual.Shine);
        Assert.Equal((float)expected.Reflectivity, actual.Reflectivity);
    }
}
