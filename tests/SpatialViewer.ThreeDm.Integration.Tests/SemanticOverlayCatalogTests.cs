using SpatialViewer.ThreeDm.Core;
using SpatialViewer.ThreeDm.Integration;

namespace SpatialViewer.ThreeDm.Integration.Tests;

public sealed class SemanticOverlayCatalogTests
{
    [Fact]
    public void CatalogExpandsAnnotationInstanceIdentityWithoutFlatteningSourceGeometry()
    {
        var definitionId = Guid.NewGuid();
        var annotationId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();
        var bounds = BoundingBox3d.FromPoints(new Point3d(0, 0, 0), new Point3d(1, 1, 0));
        var annotation = new ThreeDmSceneObject(
            annotationId,
            "Label",
            null,
            ThreeDmGeometryKind.Annotation,
            bounds,
            Geometry: new ThreeDmAnnotationGeometryData(
                "Text",
                "Room 101",
                "",
                Guid.Empty,
                new Plane3d(
                    new Point3d(0, 0, 0),
                    new Vector3d(1, 0, 0),
                    new Vector3d(0, 1, 0),
                    new Vector3d(0, 0, 1)),
                1,
                0,
                Array.Empty<Point3d>(),
                bounds));
        var instance = new ThreeDmSceneObject(
            instanceId,
            "Block",
            null,
            ThreeDmGeometryKind.InstanceReference,
            bounds,
            Geometry: new ThreeDmInstanceReferenceGeometryData(
                definitionId,
                new Transform3d(
                    1, 0, 0, 10,
                    0, 1, 0, 20,
                    0, 0, 1, 30,
                    0, 0, 0, 1),
                bounds));
        var document = new ThreeDmSceneDocument("overlay.3dm", [annotation, instance], bounds, Array.Empty<ThreeDmImportDiagnostic>())
        {
            InstanceDefinitions = [new ThreeDmInstanceDefinitionInfo(definitionId, "Labels", "", null, [annotationId])],
        };

        var overlay = Assert.Single(ThreeDmSemanticOverlayCatalog.Create(document));

        Assert.Equal(annotationId, overlay.SourceObjectId);
        Assert.Equal(ThreeDmGeometryKind.Annotation, overlay.GeometryKind);
        Assert.Equal([instanceId], overlay.InstancePath);
        Assert.Equal(10, overlay.Transform.M03, 8);
        Assert.Equal(20, overlay.Transform.M13, 8);
        Assert.Equal(30, overlay.Transform.M23, 8);
        Assert.IsType<ThreeDmAnnotationGeometryData>(overlay.Geometry);
    }
}
