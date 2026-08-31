using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;
using SpatialViewer.Formats.ThreeDm.Rhino3dm;
using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Tests;

public sealed class AdvancedObjectImportTests
{
    [Fact]
    public async Task ImportAsyncPreservesBrepSubDAnnotationHatchAndNestedInstances()
    {
        var path = Path.Combine(Path.GetTempPath(), $"spatialviewer-phase3-{Guid.NewGuid():N}.3dm");
        try
        {
            using (var model = new File3dm())
            {
                var box = Brep.CreateFromBox(new Rhino.Geometry.BoundingBox(
                    new Rhino.Geometry.Point3d(0, 0, 0),
                    new Rhino.Geometry.Point3d(10, 8, 6)));
                Assert.NotNull(box);
                Add(model, box, "BrepBox");

                using var mesh = Mesh.CreateFromBox(
                    new Rhino.Geometry.BoundingBox(
                        new Rhino.Geometry.Point3d(20, 0, 0),
                        new Rhino.Geometry.Point3d(24, 4, 4)),
                    1,
                    1,
                    1);
                Assert.NotNull(mesh);
                using var subD = SubD.CreateFromMesh(mesh);
                Assert.NotNull(subD);
                Add(model, subD, "SubDBox");

                using var textDot = new TextDot("Door A", new Rhino.Geometry.Point3d(0, 12, 0))
                {
                    SecondaryText = "D-01",
                    FontHeight = 14,
                };
                Add(model, textDot, "TextDot");

                var textId = model.Objects.AddText(
                    "Room 101",
                    new Plane(new Rhino.Geometry.Point3d(0, 16, 0), Vector3d.ZAxis),
                    2.5,
                    "Arial",
                    false,
                    false,
                    new ObjectAttributes { Name = "Annotation" });
                Assert.NotEqual(Guid.Empty, textId);

                using var hatchBoundary = new ArcCurve(new Circle(
                    new Plane(new Rhino.Geometry.Point3d(0, 22, 0), Vector3d.ZAxis),
                    3));
                var hatches = Hatch.Create(hatchBoundary, 0, 0.25, 1.5);
                Assert.NotEmpty(hatches);
                using (var hatch = hatches[0])
                {
                    Add(model, hatch, "Hatch");
                }

                using var childGeometry = new LineCurve(
                    new Rhino.Geometry.Point3d(0, 0, 0),
                    new Rhino.Geometry.Point3d(2, 0, 0));
                var childIndex = model.AllInstanceDefinitions.Add(
                    "ChildBlock",
                    "Phase 3 child definition",
                    Rhino.Geometry.Point3d.Origin,
                    childGeometry);
                Assert.True(childIndex >= 0);
                var childDefinition = model.AllInstanceDefinitions.Single(item => item.Index == childIndex);

                using var childReference = new InstanceReferenceGeometry(
                    childDefinition.Id,
                    Transform.Translation(1, 2, 3));
                var parentIndex = model.AllInstanceDefinitions.Add(
                    "ParentBlock",
                    "Phase 3 nested parent definition",
                    Rhino.Geometry.Point3d.Origin,
                    childReference);
                Assert.True(parentIndex >= 0);

                var topInstanceId = model.Objects.AddInstanceObject(
                    parentIndex,
                    Transform.Translation(10, 20, 30),
                    new ObjectAttributes { Name = "TopInstance" });
                Assert.NotEqual(Guid.Empty, topInstanceId);

                Assert.True(model.Write(path, 8));
            }

            var document = await new Rhino3dmThreeDmImporter().ImportAsync(path);

            var brep = Geometry<ThreeDmBrepGeometryData>(document, "BrepBox");
            Assert.True(brep.IsSolid);
            Assert.Equal(8, brep.Vertices.Count);
            Assert.Equal(12, brep.Edges.Count);
            Assert.Equal(6, brep.Faces.Count);
            Assert.All(brep.Faces, face => Assert.NotEmpty(face.LoopIndices));
            Assert.All(brep.Loops, loop => Assert.NotEmpty(loop.TrimIndices));
            Assert.All(brep.Trims, trim => Assert.NotNull(trim.ParameterCurve));
            Assert.All(brep.Edges, edge =>
            {
                Assert.Contains(brep.Vertices, vertex => vertex.Index == edge.StartVertexIndex);
                Assert.Contains(brep.Vertices, vertex => vertex.Index == edge.EndVertexIndex);
            });

            var subD = Geometry<ThreeDmSubDGeometryData>(document, "SubDBox");
            Assert.NotEmpty(subD.Vertices);
            Assert.NotEmpty(subD.Faces);
            Assert.All(subD.Faces, face =>
                Assert.All(face.VertexIds, vertexId =>
                    Assert.Contains(subD.Vertices, vertex => vertex.Id == vertexId)));

            var textDot = Geometry<ThreeDmTextDotGeometryData>(document, "TextDot");
            Assert.Equal("Door A", textDot.Text);
            Assert.Equal("D-01", textDot.SecondaryText);
            Assert.Equal(14, textDot.FontHeight);

            var annotation = Geometry<ThreeDmAnnotationGeometryData>(document, "Annotation");
            Assert.Equal("Room 101", annotation.PlainText);
            Assert.Equal(2.5, annotation.TextHeight, 8);

            var hatch = Geometry<ThreeDmHatchGeometryData>(document, "Hatch");
            Assert.Equal(0, hatch.PatternIndex);
            Assert.Equal(1.5, hatch.PatternScale, 8);
            Assert.Equal(0.25, hatch.PatternRotationRadians, 8);
            Assert.NotEmpty(hatch.OuterBoundaries);

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
