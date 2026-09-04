using System.Drawing;
using Rhino.Geometry;
using SpatialViewer.Formats.ThreeDm.Rhino3dm;
using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Tests;

public sealed class AdvancedFixtureCoverageTests
{
    [Fact]
    public void ConverterPreservesLightAndClippingPlaneSemantics()
    {
        using var clippingPlane = new ClippingPlaneSurface(
            new Plane(new Rhino.Geometry.Point3d(0, 0, 2), Rhino.Geometry.Vector3d.ZAxis));
        var convertedClip = Assert.IsType<ThreeDmClippingPlaneGeometryData>(
            Rhino3dmGeometryConverter.Convert(clippingPlane));
        Assert.True(convertedClip.Bounds.IsValid);

        using var light = new Rhino.Geometry.Light
        {
            Name = "Key Light",
            Location = new Rhino.Geometry.Point3d(2, 2, 8),
            Direction = new Rhino.Geometry.Vector3d(0, 0, -1),
            Diffuse = Color.FromArgb(255, 240, 220, 200),
            Intensity = 0.75,
            IsEnabled = true,
        };
        var convertedLight = Assert.IsType<ThreeDmLightGeometryData>(
            Rhino3dmGeometryConverter.Convert(light));
        Assert.Equal("Key Light", convertedLight.Name);
        Assert.Equal(0.75, convertedLight.Intensity, 8);
        Assert.True(convertedLight.IsEnabled);
    }

    [Fact]
    public void ConverterRecognizesSubDGeometryEvenWhenPinnedBindingsCannotSynthesizeAValidFixture()
    {
        using var subD = new SubD();

        var converted = Assert.IsType<ThreeDmSubDGeometryData>(
            Rhino3dmGeometryConverter.Convert(subD));

        Assert.Empty(converted.Vertices);
        Assert.Empty(converted.Faces);
    }
}
