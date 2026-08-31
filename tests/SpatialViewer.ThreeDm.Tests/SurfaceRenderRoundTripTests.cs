using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;
using SpatialViewer.Formats.ThreeDm.Rhino3dm;
using SpatialViewer.ThreeDm.Core;
using SpatialViewer.ThreeDm.Rendering;

namespace SpatialViewer.ThreeDm.Tests;

public sealed class SurfaceRenderRoundTripTests
{
    [Fact]
    public async Task CurvedNurbsSurfaceRoundTripsFromRhino3dmIntoAdaptiveRenderMesh()
    {
        var path = Path.Combine(Path.GetTempPath(), $"spatialviewer-phase4-surface-{Guid.NewGuid():N}.3dm");
        try
        {
            using (var model = new File3dm())
            {
                model.Settings.ModelAbsoluteTolerance = 0.001;
                using var surface = new Sphere(new Rhino.Geometry.Point3d(10, 20, 30), 5).ToNurbsSurface();
                Assert.NotNull(surface);
                var id = model.Objects.Add(surface, new ObjectAttributes { Name = "NurbsSphere" });
                Assert.NotEqual(Guid.Empty, id);
                Assert.True(model.Write(path, 8));
            }

            var document = await new Rhino3dmThreeDmImporter().ImportAsync(path);
            var sceneObject = Assert.Single(document.Objects, item => item.Name == "NurbsSphere");
            var surfaceData = Assert.IsType<ThreeDmNurbsSurfaceGeometryData>(sceneObject.Geometry);
            Assert.True(surfaceData.IsRational);
            Assert.True(surfaceData.ControlPointCountU > surfaceData.DegreeU);
            Assert.True(surfaceData.ControlPointCountV > surfaceData.DegreeV);
            Assert.True(surfaceData.StartSuperfluousKnotU.HasValue);
            Assert.True(surfaceData.EndSuperfluousKnotU.HasValue);
            Assert.True(surfaceData.StartSuperfluousKnotV.HasValue);
            Assert.True(surfaceData.EndSuperfluousKnotV.HasValue);
            Assert.True(double.IsFinite(surfaceData.StartSuperfluousKnotU.Value));
            Assert.True(double.IsFinite(surfaceData.EndSuperfluousKnotU.Value));
            Assert.True(double.IsFinite(surfaceData.StartSuperfluousKnotV.Value));
            Assert.True(double.IsFinite(surfaceData.EndSuperfluousKnotV.Value));

            var renderScene = new ThreeDmRenderSceneBuilder().Build(
                document,
                new ThreeDmTessellationSettings(
                    ThreeDmTessellationQuality.High,
                    AbsoluteChordTolerance: 0.05,
                    MaxSurfaceSegmentsPerDirection: 64));

            var mesh = Assert.Single(renderScene.Meshes);
            Assert.True(mesh.Vertices.Count > 4);
            Assert.NotEmpty(mesh.Indices);
            Assert.Equal(mesh.Vertices.Count, mesh.Normals.Count);
            Assert.Equal(mesh.Vertices.Count, mesh.TextureCoordinates.Count);
            Assert.DoesNotContain(renderScene.Diagnostics, item => item.Code == "3DM_RENDER_TESSELLATION_FAILED");
            Assert.All(mesh.Vertices, vertex =>
            {
                var dx = vertex.X - 10;
                var dy = vertex.Y - 20;
                var dz = vertex.Z - 30;
                var radius = Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
                Assert.Equal(5, radius, 6);
            });
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
