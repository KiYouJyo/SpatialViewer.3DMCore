using SpatialViewer.ThreeDm.Core;
using SpatialViewer.ThreeDm.Rendering;

namespace SpatialViewer.ThreeDm.Rendering.Tests;

public sealed class SharedMeshSceneTests
{
    [Fact]
    public void SharedBuilderKeepsOneGeometryPayloadForRepeatedBlockInstances()
    {
        var definitionId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var firstInstanceId = Guid.NewGuid();
        var secondInstanceId = Guid.NewGuid();
        var member = MeshObject(memberId, "ColorFromParent", 0xFF00FF00);
        var firstInstance = InstanceObject(
            firstInstanceId,
            definitionId,
            Translation(10, 20, 30),
            0xFFCC3300);
        var secondInstance = InstanceObject(
            secondInstanceId,
            definitionId,
            Translation(-5, 8, 13),
            0xFF3366CC);
        var document = new ThreeDmSceneDocument(
            "shared.3dm",
            [member, firstInstance, secondInstance],
            UnitBounds,
            Array.Empty<ThreeDmImportDiagnostic>())
        {
            InstanceDefinitions =
            [
                new ThreeDmInstanceDefinitionInfo(
                    definitionId,
                    "Repeated block",
                    string.Empty,
                    null,
                    [memberId]),
            ],
        };
        var builder = new ThreeDmSharedMeshSceneBuilder();

        var scene = builder.Build(document);

        var geometry = Assert.Single(scene.Geometries);
        Assert.Equal(memberId, geometry.SourceObjectId);
        Assert.Equal(3, geometry.Vertices.Count);
        Assert.Equal(new ThreeDmRenderVertex(0, 0, 0), geometry.Vertices[0]);
        Assert.Equal(new ThreeDmRenderVertex(1, 0, 0), geometry.Vertices[1]);
        Assert.Equal(new ThreeDmRenderVertex(0, 1, 0), geometry.Vertices[2]);

        Assert.Equal(2, scene.Instances.Count);
        Assert.All(scene.Instances, instance => Assert.Equal(geometry.GeometryIndex, instance.GeometryIndex));

        var first = Assert.Single(scene.Instances, instance => instance.InstancePath.SequenceEqual([firstInstanceId]));
        Assert.Equal(10, first.Transform.M03, 12);
        Assert.Equal(20, first.Transform.M13, 12);
        Assert.Equal(30, first.Transform.M23, 12);
        Assert.Equal(0xFFCC3300u, first.Appearance.ColorArgb);

        var second = Assert.Single(scene.Instances, instance => instance.InstancePath.SequenceEqual([secondInstanceId]));
        Assert.Equal(-5, second.Transform.M03, 12);
        Assert.Equal(8, second.Transform.M13, 12);
        Assert.Equal(13, second.Transform.M23, 12);
        Assert.Equal(0xFF3366CCu, second.Appearance.ColorArgb);

        Assert.Empty(scene.Diagnostics);
        Assert.Equal(1, builder.CacheEntryCount);
    }

    private static ThreeDmSceneObject MeshObject(Guid id, string colorSource, uint objectColor) =>
        new(
            id,
            "Definition mesh",
            null,
            ThreeDmGeometryKind.Mesh,
            UnitBounds,
            null,
            true,
            objectColor,
            colorSource,
            "MaterialFromParent",
            UnitTriangle);

    private static ThreeDmSceneObject InstanceObject(
        Guid id,
        Guid definitionId,
        Transform3d transform,
        uint color) =>
        new(
            id,
            "Block instance",
            null,
            ThreeDmGeometryKind.InstanceReference,
            UnitBounds,
            null,
            true,
            color,
            "ColorFromObject",
            "MaterialFromObject",
            new ThreeDmInstanceReferenceGeometryData(definitionId, transform, UnitBounds));

    private static Transform3d Translation(double x, double y, double z) =>
        new(
            1, 0, 0, x,
            0, 1, 0, y,
            0, 0, 1, z,
            0, 0, 0, 1);

    private static readonly BoundingBox3d UnitBounds =
        BoundingBox3d.FromPoints(new Point3d(0, 0, 0), new Point3d(1, 1, 0));

    private static readonly ThreeDmMeshGeometryData UnitTriangle = new(
        [new Point3d(0, 0, 0), new Point3d(1, 0, 0), new Point3d(0, 1, 0)],
        [new ThreeDmMeshFace(0, 1, 2, null)],
        [new Vector3d(0, 0, 1), new Vector3d(0, 0, 1), new Vector3d(0, 0, 1)],
        Array.Empty<ThreeDmTextureCoordinate>(),
        false,
        UnitBounds);
}
