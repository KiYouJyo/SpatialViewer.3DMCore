using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;
using SpatialViewer.Formats.ThreeDm.Rhino3dm;
using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Tests;

public sealed class AdvancedObjectImportTests
{
    [Fact]
    public async Task ImportAsyncPreservesBrepAnnotationHatchAndNestedInstances()
    {
        var path = Path.Combine(Path.GetTempPath(), $"spatialviewer-phase3-{Guid.NewGuid():N}.3dm");
        try
        {
            using (var model = new File3dm())
            {
                using var boxSource = Brep.CreateFromBox(new Rhino.Geometry.BoundingBox(
                    new Rhino.Geometry.Point3d(0, 0, 0),
                    new Rhino.Geometry.Point3d(10, 8, 6)));
                Assert.NotNull(boxSource);
                Add(model, boxSource, "BrepBox");

                using var textDotSource = new TextDot("Door A", new Rhino.Geometry.Point3d(0, 12, 0))
                {
                    SecondaryText = "D-01",
                    FontHeight = 14,
                };
                Add(model, textDotSource, "TextDot");

                var textId = model.Objects.AddText(
                    "Room 101",
                    new Plane(new Rhino.Geometry.Point3d(0, 16, 0), Rhino.Geometry.Vector3d.ZAxis),
                    2.5,
                    "Arial",
                    false,
                    false,
                    new ObjectAttributes { Name = "Annotation" });
                Assert.NotEqual(Guid.Empty, textId);

                var leaderId = model.Objects.AddLeader(
                    "Leader A",
                    new Plane(new Rhino.Geometry.Point3d(8, 16, 0), Rhino.Geometry.Vector3d.ZAxis),
                    new[]
                    {
                        new Point2d(0, 0),
                        new Point2d(3, 1),
                        new Point2d(5, 1),
                    },
                    new ObjectAttributes { Name = "Leader" });
                Assert.NotEqual(Guid.Empty, leaderId);

                var hatchPlane = new Plane(new Rhino.Geometry.Point3d(0, 22, 0), Rhino.Geometry.Vector3d.ZAxis);
                using var hatchBoundary = new ArcCurve(new Circle(hatchPlane, 3));
                using var hatchSource = Hatch.Create(
                    hatchPlane,
                    hatchBoundary,
                    Array.Empty<Curve>(),
                    0,
                    0.25,
                    1.5);
                Assert.NotNull(hatchSource);
                Add(model, hatchSource, "Hatch");

                using var childGeometry = new LineCurve(
                    new Rhino.Geometry.Point3d(0, 0, 0),
                    new Rhino.Geometry.Point3d(2, 0, 0));
                var childIndex = model.AllInstanceDefinitions.Add(
                    "ChildBlock",
                    "Phase 3 child definition",
                    Rhino.Geometry.Point3d.Origin,
                    new GeometryBase[] { childGeometry });
                Assert.True(childIndex >= 0);
                var childDefinition = model.AllInstanceDefinitions.Single(item => item.Index == childIndex);

                using var childReference = new InstanceReferenceGeometry(
                    childDefinition.Id,
                    Transform.Translation(1, 2, 3));
                var parentIndex = model.AllInstanceDefinitions.Add(
                    "ParentBlock",
                    "Phase 3 nested parent definition",
                    Rhino.Geometry.Point3d.Origin,
                    new GeometryBase[] { childReference });
                Assert.True(parentIndex >= 0);

                var topInstanceId = model.Objects.AddInstanceObject(
                    parentIndex,
                    Transform.Translation(10, 20, 30),
                    new ObjectAttributes { Name = "TopInstance" });
                Assert.NotEqual(Guid.Empty, topInstanceId);

                Assert.True(model.Write(path, 8));
            }

            var document = await new Rhino3dmThreeDmImporter().ImportAsync(path);

            var brepData = Geometry<ThreeDmBrepGeometryData>(document, "BrepBox");
            Assert.True(brepData.IsSolid);
            Assert.Equal(8, brepData.Vertices.Count);
            Assert.Equal(12, brepData.Edges.Count);
            Assert.Equal(6, brepData.Faces.Count);
            Assert.All(brepData.Faces, face => Assert.NotEmpty(face.LoopIndices));
            Assert.All(brepData.Loops, loop => Assert.NotEmpty(loop.TrimIndices));
            Assert.All(brepData.Trims, trim => Assert.NotNull(trim.ParameterCurve));
            Assert.All(brepData.Edges, edge =>
            {
                Assert.Contains(brepData.Vertices, vertex => vertex.Index == edge.StartVertexIndex);
                Assert.Contains(brepData.Vertices, vertex => vertex.Index == edge.EndVertexIndex);
            });

            var textDotData = Geometry<ThreeDmTextDotGeometryData>(document, "TextDot");
            Assert.Equal("Door A", textDotData.Text);
            Assert.Equal("D-01", textDotData.SecondaryText);
            Assert.Equal(14, textDotData.FontHeight);

            var annotationData = Geometry<ThreeDmAnnotationGeometryData>(document, "Annotation");
            Assert.Equal("Room 101", annotationData.PlainText);
            Assert.Equal(2.5, annotationData.TextHeight, 8);

            var leaderData = Geometry<ThreeDmAnnotationGeometryData>(document, "Leader");
            Assert.Equal("Leader A", leaderData.PlainText);
            Assert.Equal(3, leaderData.LeaderPoints.Count);

            var hatchData = Geometry<ThreeDmHatchGeometryData>(document, "Hatch");
            Assert.Equal(0, hatchData.PatternIndex);
            Assert.Equal(1.5, hatchData.PatternScale, 8);
            Assert.Equal(0.25, hatchData.PatternRotationRadians, 8);
            Assert.NotEmpty(hatchData.OuterBoundaries);

            var childDefinitionData = Assert.Single(document.InstanceDefinitions, item => item.Name == "ChildBlock");
            var parentDefinitionData = Assert.Single(document.InstanceDefinitions, item => item.Name == "ParentBlock");
            Assert.Single(childDefinitionData.ObjectIds);
            var nestedObjectId = Assert.Single(parentDefinitionData.ObjectIds);

            var nestedObject = Assert.Single(document.Objects, item => item.Id == nestedObjectId);
            var nestedReference = Assert.IsType<ThreeDmInstanceReferenceGeometryData>(nestedObject.Geometry);
            Assert.Equal(childDefinitionData.Id, nestedReference.InstanceDefinitionId);
            Assert.Equal(1, nestedReference.Transform.M03, 8);
            Assert.Equal(2, nestedReference.Transform.M13, 8);
            Assert.Equal(3, nestedReference.Transform.M23, 8);

            var topReference = Geometry<ThreeDmInstanceReferenceGeometryData>(document, "TopInstance");
            Assert.Equal(parentDefinitionData.Id, topReference.InstanceDefinitionId);
            Assert.Equal(10, topReference.Transform.M03, 8);
            Assert.Equal(20, topReference.Transform.M13, 8);
            Assert.Equal(30, topReference.Transform.M23, 8);

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
        var id = model.Objects.Add(geometry, new ObjectAttributes { Name = name });
        Assert.NotEqual(Guid.Empty, id);
    }

    private static T Geometry<T>(ThreeDmSceneDocument document, string name)
        where T : ThreeDmGeometryData
    {
        var sceneObject = Assert.Single(document.Objects, item => item.Name == name);
        return Assert.IsType<T>(sceneObject.Geometry);
    }
}
