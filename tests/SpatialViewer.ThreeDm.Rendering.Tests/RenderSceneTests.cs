using SpatialViewer.ThreeDm.Core;
using SpatialViewer.ThreeDm.Rendering;

namespace SpatialViewer.ThreeDm.Rendering.Tests;

public sealed class RenderSceneTests
{
    [Fact]
    public void TessellationQualityTightensChordTolerance()
    {
        var bounds = BoundingBox3d.FromPoints(new Point3d(0, 0, 0), new Point3d(100, 100, 100));

        var draft = new ThreeDmTessellationSettings(ThreeDmTessellationQuality.Draft).ResolveChordTolerance(bounds);
        var normal = new ThreeDmTessellationSettings(ThreeDmTessellationQuality.Normal).ResolveChordTolerance(bounds);
        var high = new ThreeDmTessellationSettings(ThreeDmTessellationQuality.High).ResolveChordTolerance(bounds);

        Assert.True(high < normal);
        Assert.True(normal < draft);
    }

    [Fact]
    public void CircleHighQualityUsesMoreSegmentsAndKeepsAnalyticRadius()
    {
        var curve = AnalyticCurve(
            ThreeDmCurveForm.Circle,
            arc: new ThreeDmArcGeometryData(WorldXy, 10, 0, Math.PI * 2),
            bounds: BoundingBox3d.FromPoints(new Point3d(-10, -10, 0), new Point3d(10, 10, 0)));
        var sourceId = Guid.NewGuid();

        var draft = ThreeDmCurveTessellator.Tessellate(
            sourceId,
            curve,
            new ThreeDmTessellationSettings(ThreeDmTessellationQuality.Draft));
        var high = ThreeDmCurveTessellator.Tessellate(
            sourceId,
            curve,
            new ThreeDmTessellationSettings(ThreeDmTessellationQuality.High));

        Assert.True(high.Points.Count > draft.Points.Count);
        Assert.True(high.IsClosed);
        Assert.Equal(high.Points[0], high.Points[^1]);
        Assert.All(high.Points, point =>
        {
            var radius = Math.Sqrt((point.X * point.X) + (point.Y * point.Y));
            Assert.Equal(10, radius, 9);
            Assert.Equal(0, point.Z, 9);
        });
    }

    [Fact]
    public void RotatedEllipseUsesStoredPlaneBasis()
    {
        var plane = new Plane3d(
            new Point3d(5, 6, 7),
            new Vector3d(0, 1, 0),
            new Vector3d(0, 0, 1),
            new Vector3d(1, 0, 0));
        var curve = AnalyticCurve(
            ThreeDmCurveForm.Ellipse,
            ellipse: new ThreeDmEllipseGeometryData(plane, 4, 2),
            bounds: BoundingBox3d.FromPoints(new Point3d(5, 2, 5), new Point3d(5, 10, 9)));

        var rendered = ThreeDmCurveTessellator.Tessellate(
            Guid.NewGuid(),
            curve,
            new ThreeDmTessellationSettings(AbsoluteChordTolerance: 0.01));

        var first = rendered.Points[0];
        Assert.Equal(5, first.X, 9);
        Assert.Equal(10, first.Y, 9);
        Assert.Equal(7, first.Z, 9);
        Assert.True(rendered.IsClosed);
    }

    [Fact]
    public void QuadraticNurbsCurveIsEvaluatedIndependentlyOfRhino3dm()
    {
        var nurbs = new ThreeDmNurbsCurveData(
            2,
            false,
            false,
            false,
            [
                new ThreeDmWeightedPoint3d(new Point3d(0, 0, 0), 1),
                new ThreeDmWeightedPoint3d(new Point3d(1, 1, 0), 1),
                new ThreeDmWeightedPoint3d(new Point3d(2, 0, 0), 1),
            ],
            [0, 0, 1, 1],
            0,
            1);
        var curve = new ThreeDmCurveGeometryData(
            ThreeDmCurveForm.Nurbs,
            nurbs,
            Array.Empty<Point3d>(),
            null,
            null,
            BoundingBox3d.FromPoints(new Point3d(0, 0, 0), new Point3d(2, 1, 0)));

        var rendered = ThreeDmCurveTessellator.Tessellate(
            Guid.NewGuid(),
            curve,
            new ThreeDmTessellationSettings(AbsoluteChordTolerance: 0.001));

        Assert.True(rendered.Points.Count > 3);
        Assert.Contains(rendered.Points, point => Math.Abs(point.X - 1) < 1e-9 && Math.Abs(point.Y - 0.5) < 1e-9);
        Assert.Equal(0, rendered.Points[0].X, 9);
        Assert.Equal(2, rendered.Points[^1].X, 9);
    }

    [Fact]
    public void MeshTessellatorTriangulatesQuadsAndPreservesVertexData()
    {
        var sourceId = Guid.NewGuid();
        var mesh = new ThreeDmMeshGeometryData(
            [
                new Point3d(0, 0, 0),
                new Point3d(2, 0, 0),
                new Point3d(2, 2, 0),
                new Point3d(0, 2, 0),
            ],
            [new ThreeDmMeshFace(0, 1, 2, 3)],
            [
                new Vector3d(0, 0, 1),
                new Vector3d(0, 0, 1),
                new Vector3d(0, 0, 1),
                new Vector3d(0, 0, 1),
            ],
            [
                new ThreeDmTextureCoordinate(0, 0),
                new ThreeDmTextureCoordinate(1, 0),
                new ThreeDmTextureCoordinate(1, 1),
                new ThreeDmTextureCoordinate(0, 1),
            ],
            false,
            BoundingBox3d.FromPoints(new Point3d(0, 0, 0), new Point3d(2, 2, 0)));

        var rendered = ThreeDmMeshTessellator.Tessellate(sourceId, mesh);

        Assert.Equal(sourceId, rendered.SourceObjectId);
        Assert.Equal([0, 1, 2, 0, 2, 3], rendered.Indices);
        Assert.Equal(4, rendered.Vertices.Count);
        Assert.Equal(4, rendered.Normals.Count);
        Assert.Equal(4, rendered.TextureCoordinates.Count);
    }

    [Fact]
    public void SceneBuilderComposesNestedInstanceTransformsWithoutDuplicatingDefinitionGeometry()
    {
        var meshId = Guid.NewGuid();
        var childReferenceId = Guid.NewGuid();
        var topReferenceId = Guid.NewGuid();
        var childDefinitionId = Guid.NewGuid();
        var parentDefinitionId = Guid.NewGuid();

        var mesh = new ThreeDmMeshGeometryData(
            [
                new Point3d(0, 0, 0),
                new Point3d(1, 0, 0),
                new Point3d(0, 1, 0),
            ],
            [new ThreeDmMeshFace(0, 1, 2)],
            Array.Empty<Vector3d>(),
            Array.Empty<ThreeDmTextureCoordinate>(),
            false,
            BoundingBox3d.FromPoints(new Point3d(0, 0, 0), new Point3d(1, 1, 0)));

        var meshObject = new ThreeDmSceneObject(
            meshId,
            "DefinitionMesh",
            null,
            ThreeDmGeometryKind.Mesh,
            mesh.Bounds,
            Geometry: mesh);
        var childReference = new ThreeDmSceneObject(
            childReferenceId,
            "NestedChild",
            null,
            ThreeDmGeometryKind.InstanceReference,
            mesh.Bounds,
            Geometry: new ThreeDmInstanceReferenceGeometryData(
                childDefinitionId,
                Translation(1, 2, 3),
                mesh.Bounds));
        var topReference = new ThreeDmSceneObject(
            topReferenceId,
            "TopInstance",
            null,
            ThreeDmGeometryKind.InstanceReference,
            mesh.Bounds,
            Geometry: new ThreeDmInstanceReferenceGeometryData(
                parentDefinitionId,
                Translation(10, 20, 30),
                mesh.Bounds));

        var document = new ThreeDmSceneDocument(
            "nested.3dm",
            [meshObject, childReference, topReference],
            mesh.Bounds,
            Array.Empty<ThreeDmImportDiagnostic>())
        {
            InstanceDefinitions =
            [
                new ThreeDmInstanceDefinitionInfo(childDefinitionId, "Child", string.Empty, null, [meshId]),
                new ThreeDmInstanceDefinitionInfo(parentDefinitionId, "Parent", string.Empty, null, [childReferenceId]),
            ],
        };

        var builder = new ThreeDmRenderSceneBuilder();
        var firstBuild = builder.Build(document);
        var cacheCount = builder.CacheEntryCount;
        var secondBuild = builder.Build(document);

        var rendered = Assert.Single(firstBuild.Meshes);
        Assert.Equal(meshId, rendered.SourceObjectId);
        Assert.Equal([topReferenceId, childReferenceId], rendered.InstancePath);
        Assert.Equal(11, rendered.Vertices[0].X, 9);
        Assert.Equal(22, rendered.Vertices[0].Y, 9);
        Assert.Equal(33, rendered.Vertices[0].Z, 9);
        Assert.Equal(cacheCount, builder.CacheEntryCount);
        Assert.Single(secondBuild.Meshes);
    }

    private static ThreeDmCurveGeometryData AnalyticCurve(
        ThreeDmCurveForm form,
        ThreeDmArcGeometryData? arc = null,
        ThreeDmEllipseGeometryData? ellipse = null,
        BoundingBox3d? bounds = null)
    {
        var actualBounds = bounds ?? BoundingBox3d.FromPoints(new Point3d(-1, -1, -1), new Point3d(1, 1, 1));
        return new ThreeDmCurveGeometryData(
            form,
            DummyNurbs(form is ThreeDmCurveForm.Circle or ThreeDmCurveForm.Ellipse),
            Array.Empty<Point3d>(),
            arc,
            ellipse,
            actualBounds);
    }

    private static ThreeDmNurbsCurveData DummyNurbs(bool closed) =>
        new(
            1,
            false,
            closed,
            false,
            [
                new ThreeDmWeightedPoint3d(new Point3d(0, 0, 0), 1),
                new ThreeDmWeightedPoint3d(new Point3d(1, 0, 0), 1),
            ],
            [0, 1],
            0,
            1);

    private static Transform3d Translation(double x, double y, double z) =>
        new(
            1, 0, 0, x,
            0, 1, 0, y,
            0, 0, 1, z,
            0, 0, 0, 1);

    private static readonly Plane3d WorldXy = new(
        new Point3d(0, 0, 0),
        new Vector3d(1, 0, 0),
        new Vector3d(0, 1, 0),
        new Vector3d(0, 0, 1));
}
