using SpatialViewer.ThreeDm.Core;
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
            0.25)
        {
            PhysicallyBased = new ThreeDmPhysicallyBasedMaterialInfo(
                0.1,
                0.2,
                0.3,
                0.9,
                0.7,
                0.4,
                0.8,
                0.6,
                0.5,
                0.25,
                "GGX"),
            Textures =
            [
                new ThreeDmMaterialTextureInfo(
                    "facade-albedo.png",
                    "Bitmap",
                    true,
                    1,
                    "MappingChannel",
                    "Repeat",
                    "Repeat",
                    "Repeat",
                    2,
                    3,
                    0.1,
                    0.2,
                    0.3),
            ],
        };
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

        var expectedPbr = Assert.IsType<ThreeDmPhysicallyBasedMaterialInfo>(expected.PhysicallyBased);
        var actualPbr = Assert.IsType<WindowsRenderPbrMaterial>(actual.PhysicallyBased);
        Assert.Equal((float)expectedPbr.BaseColorR, actualPbr.BaseColorR);
        Assert.Equal((float)expectedPbr.BaseColorG, actualPbr.BaseColorG);
        Assert.Equal((float)expectedPbr.BaseColorB, actualPbr.BaseColorB);
        Assert.Equal((float)expectedPbr.BaseColorA, actualPbr.BaseColorA);
        Assert.Equal((float)expectedPbr.Metallic, actualPbr.Metallic);
        Assert.Equal((float)expectedPbr.Roughness, actualPbr.Roughness);
        Assert.Equal(expectedPbr.Brdf, actualPbr.Brdf);

        var expectedTexture = Assert.Single(expected.Textures);
        var actualTexture = Assert.Single(actual.Textures);
        Assert.Equal(expectedTexture.FileName, actualTexture.FileName);
        Assert.Equal(expectedTexture.TextureType, actualTexture.TextureType);
        Assert.Equal(expectedTexture.MappingChannelId, actualTexture.MappingChannelId);
        Assert.Equal((float)expectedTexture.RepeatU, actualTexture.RepeatU);
        Assert.Equal((float)expectedTexture.OffsetV, actualTexture.OffsetV);
        Assert.Equal((float)expectedTexture.RotationRadians, actualTexture.RotationRadians);
    }
}
