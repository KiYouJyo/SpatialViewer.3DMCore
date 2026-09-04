using System.Drawing;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;
using SpatialViewer.Formats.ThreeDm.Rhino3dm;
using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Tests;

public sealed class AdvancedFixtureCoverageTests
{
    [Fact]
    public async Task GeneratedRhino8FileRoundTripsSubDLightAndClippingPlane()
    {
        var path = Path.Combine(Path.GetTempPath(), $"spatialviewer-advanced-{Guid.NewGuid():N}.3dm");
        try
        {
            using (var model = new File3dm())
            {
                using var controlMesh = new Mesh();
                controlMesh.Vertices.Add(0, 0, 0);
                controlMesh.Vertices.Add(4, 0, 0);
                controlMesh.Vertices.Add(4, 4, 0);
                controlMesh.Vertices.Add(0, 4, 0);
                controlMesh.Faces.AddFace(0, 1, 2, 3);
                using var subD = SubD.CreateFromMesh(controlMesh);
                Assert.NotNull(subD);
                Add(model, subD, "SubD");

                using var clippingPlane = new ClippingPlaneSurface(
                    new Plane(new Rhino.Geometry.Point3d(0, 0, 2), Rhino.Geometry.Vector3d.ZAxis));
                Add(model, clippingPlane, "Clip");

                using var light = new Rhino.Geometry.Light
                {
                    Name = "Key Light",
                    Location = new Rhino.Geometry.Point3d(2, 2, 8),
                    Direction = new Rhino.Geometry.Vector3d(0, 0, -1),
                    Diffuse = Color.FromArgb(255, 240, 220, 200),
                    Intensity = 0.75,
                    IsEnabled = true,
                };
                Add(model, light, "Light");

                Assert.True(model.Write(path, 8));
            }

            var document = await new Rhino3dmThreeDmImporter().ImportAsync(path);

            var subD = Assert.IsType<ThreeDmSubDGeometryData>(
                Assert.Single(document.Objects, item => item.Name == "SubD").Geometry);
            Assert.NotEmpty(subD.Vertices);
            Assert.NotEmpty(subD.Faces);

            var clip = Assert.IsType<ThreeDmClippingPlaneGeometryData>(
                Assert.Single(document.Objects, item => item.Name == "Clip").Geometry);
            Assert.True(clip.Bounds.IsValid);

            var light = Assert.IsType<ThreeDmLightGeometryData>(
                Assert.Single(document.Objects, item => item.Name == "Light").Geometry);
            Assert.Equal("Key Light", light.Name);
            Assert.Equal(0.75, light.Intensity, 8);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static void Add(File3dm model, GeometryBase geometry, string name)
    {
        var id = model.Objects.Add(geometry, new ObjectAttributes { Name = name });
        Assert.NotEqual(Guid.Empty, id);
    }
}
