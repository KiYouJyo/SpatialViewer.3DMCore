using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;
using SpatialViewer.Formats.ThreeDm.Rhino3dm;
using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Tests;

public sealed class FundamentalGeometryImportTests
{
    [Fact]
    public async Task ImportAsyncPreservesFundamentalGeometrySemantics()
    {
        var path = Path.Combine(Path.GetTempPath(), $"spatialviewer-phase2-{Guid.NewGuid():N}.3dm");
        try
        {
            using (var model = new File3dm())
            {
                Add(model, new PointCloud(new[]
                {
                    new Rhino.Geometry.Point3d(0, 0, 0),
                    new Rhino.Geometry.Point3d(1, 2, 3),
                    new Rhino.Geometry.Point3d(-2, 4, 1),
                }), "PointCloud");

                Add(model, new LineCurve(
                    new Rhino.Geometry.Point3d(0, 0, 0),
                    new Rhino.Geometry.Point3d(10, 0, 0)), "Line");

                Add(model, new PolylineCurve(new[]
                {
                    new Rhino.Geometry.Point3d(0, 0, 0),
                    new Rhino.Geometry.Point3d(2, 1, 0),
                    new Rhino.Geometry.Point3d(4, 0, 1),
                    new Rhino.Geometry.Point3d(6, 2, 1),
                }), "Polyline");

                Add(model, new ArcCurve(new Circle(Plane.WorldXY, 3)), "Circle");

                var rotatedPlane = new Plane(
                    new Rhino.Geometry.Point3d(5, 6, 7),
                    new Rhino.Geometry.Vector3d(0, 1, 0),
                    new Rhino.Geometry.Vector3d(0, 0, 1));
                using var ellipseCurve = new Ellipse(rotatedPlane, 4, 2).ToNurbsCurve();
                Assert.NotNull(ellipseCurve);
                Add(model, ellipseCurve, "RotatedEllipse");

                Add(model, new PlaneSurface(
                    Plane.WorldXY,
                    new Interval(0, 10),
                    new Interval(0, 5)), "Surface");

                using var profile = new ArcCurve(new Circle(Plane.WorldXY, 2));
                var extrusion = Extrusion.Create(profile, 6, true);
                Assert.NotNull(extrusion);
                Add(model, extrusion, "Extrusion");

                var mesh = new Mesh();
                mesh.Vertices.Add(0, 0, 0);
                mesh.Vertices.Add(4, 0, 0);
                mesh.Vertices.Add(4, 4, 0);
                mesh.Vertices.Add(0, 4, 0);
                mesh.Faces.AddFace(0, 1, 2, 3);
                mesh.TextureCoordinates.Add(0, 0);
                mesh.TextureCoordinates.Add(1, 0);
                mesh.TextureCoordinates.Add(1, 1);
                mesh.TextureCoordinates.Add(0, 1);
                Add(model, mesh, "Mesh");

                Assert.True(model.Write(path, 8));
            }

            var document = await new Rhino3dmThreeDmImporter().ImportAsync(path);

            var pointCloud = Geometry<ThreeDmPointCloudGeometryData>(document, "PointCloud");
            Assert.Equal(3, pointCloud.Points.Count);

            var line = Geometry<ThreeDmCurveGeometryData>(document, "Line");
            Assert.Equal(ThreeDmCurveForm.Line, line.Form);
            Assert.Equal(1, line.Nurbs.Degree);
            Assert.True(line.Nurbs.ControlPoints.Count >= 2);
            Assert.True(double.IsFinite(line.Nurbs.StartSuperfluousKnot));
            Assert.True(double.IsFinite(line.Nurbs.EndSuperfluousKnot));

            var polyline = Geometry<ThreeDmCurveGeometryData>(document, "Polyline");
            Assert.Equal(ThreeDmCurveForm.Polyline, polyline.Form);
            Assert.Equal(4, polyline.PolylinePoints.Count);
            Assert.NotEmpty(polyline.Nurbs.Knots);

            var circle = Geometry<ThreeDmCurveGeometryData>(document, "Circle");
            Assert.Equal(ThreeDmCurveForm.Circle, circle.Form);
            Assert.NotNull(circle.Arc);
            Assert.Equal(3, circle.Arc.Radius, 8);
            Assert.Equal(1, circle.Arc.Plane.XAxis.X, 8);
            Assert.Equal(1, circle.Arc.Plane.YAxis.Y, 8);
            Assert.True(circle.Nurbs.IsRational);

            // Rhino3dm serializes Ellipse.ToNurbsCurve() as a generic NURBS curve.
            // The important invariant is that the exact rational curve survives rather than
            // being flattened to a display polyline; analytic-plane rendering is covered by
            // the pure-core rotated-ellipse tessellation regression.
            var ellipse = Geometry<ThreeDmCurveGeometryData>(document, "RotatedEllipse");
            Assert.Equal(ThreeDmCurveForm.Nurbs, ellipse.Form);
            Assert.Null(ellipse.Ellipse);
            Assert.True(ellipse.Nurbs.IsRational);
            Assert.True(ellipse.Nurbs.IsClosed);
            Assert.True(ellipse.Nurbs.ControlPoints.Count >= 8);
            Assert.NotEmpty(ellipse.Nurbs.Knots);

            var surface = Geometry<ThreeDmNurbsSurfaceGeometryData>(document, "Surface");
            Assert.True(surface.ControlPointCountU >= 2);
            Assert.True(surface.ControlPointCountV >= 2);
            Assert.Equal(surface.ControlPointCountU * surface.ControlPointCountV, surface.ControlPoints.Count);
            Assert.NotEmpty(surface.KnotsU);
            Assert.NotEmpty(surface.KnotsV);

            var extrusionData = Geometry<ThreeDmExtrusionGeometryData>(document, "Extrusion");
            Assert.True(extrusionData.IsSolid);
            Assert.True(extrusionData.IsCappedAtBottom);
            Assert.True(extrusionData.IsCappedAtTop);
            Assert.Single(extrusionData.Profiles);
            Assert.Equal(6, extrusionData.PathEnd.Z - extrusionData.PathStart.Z, 8);

            var meshData = Geometry<ThreeDmMeshGeometryData>(document, "Mesh");
            Assert.Equal(4, meshData.Vertices.Count);
            var face = Assert.Single(meshData.Faces);
            Assert.NotNull(face.D);
            Assert.Equal(4, meshData.TextureCoordinates.Count);

            Assert.DoesNotContain(document.Diagnostics, item => item.Code == "3DM_GEOMETRY_CONVERSION_FAILED");
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
        model.Objects.Add(geometry, new ObjectAttributes { Name = name });
    }

    private static T Geometry<T>(ThreeDmSceneDocument document, string name)
        where T : ThreeDmGeometryData
    {
        var sceneObject = Assert.Single(document.Objects, item => item.Name == name);
        return Assert.IsType<T>(sceneObject.Geometry);
    }
}
