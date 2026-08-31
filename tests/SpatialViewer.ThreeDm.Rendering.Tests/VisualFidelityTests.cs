using SpatialViewer.ThreeDm.Core;
using SpatialViewer.ThreeDm.Rendering;

namespace SpatialViewer.ThreeDm.Rendering.Tests;

public sealed class VisualFidelityTests
{
    [Fact]
    public void VisualBuilderResolvesLayerColorAndLayerMaterialTransparency()
    {
        var layerId = Guid.NewGuid();
        var materialId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var material = new ThreeDmMaterialInfo(materialId, "Glass", 0xFF22AA44, 0.25)
        {
            SpecularColorArgb = 0xFFFFFFFF,
            EmissionColorArgb = 0xFF010203,
            Shine = 64,
            Reflectivity = 0.35,
        };
        var layer = new ThreeDmLayerInfo(layerId, "Facade", null, true, false, 0xFF3366CC, 0)
        {
            RenderMaterialId = materialId,
        };
        var sceneObject = MeshObject(
            sourceId,
            layerId,
            null,
            "ColorFromLayer",
            "MaterialFromLayer",
            0xFFFF0000);
        var document = Document([sceneObject], [layer], [material]);

        var scene = new ThreeDmVisualRenderSceneBuilder().Build(document);

        var mesh = Assert.Single(scene.Meshes);
        Assert.Equal(0xFF3366CCu, mesh.ColorArgb);
        Assert.Equal(materialId, mesh.MaterialId);
        Assert.Equal(0xFF3366CCu, mesh.Appearance.ColorArgb);
        Assert.Equal(0.75, mesh.Appearance.Opacity, 12);
        Assert.Equal(0xFFFFFFFFu, mesh.Appearance.SpecularColorArgb);
        Assert.Equal(0xFF010203u, mesh.Appearance.EmissionColorArgb);
        Assert.Equal(64, mesh.Appearance.Shine, 12);
        Assert.Equal(0.35, mesh.Appearance.Reflectivity, 12);
    }

    [Fact]
    public void VisualBuilderUsesMaterialDiffuseColorForColorFromMaterial()
    {
        var layerId = Guid.NewGuid();
        var materialId = Guid.NewGuid();
        var material = new ThreeDmMaterialInfo(materialId, "Paint", 0xFFABCDEF, 0);
        var layer = new ThreeDmLayerInfo(layerId, "Layer", null, true, false, 0xFF101010, 0);
        var sceneObject = MeshObject(
            Guid.NewGuid(),
            layerId,
            materialId,
            "ColorFromMaterial",
            "MaterialFromObject",
            0xFFFF0000);
        var document = Document([sceneObject], [layer], [material]);

        var scene = new ThreeDmVisualRenderSceneBuilder().Build(document);

        var mesh = Assert.Single(scene.Meshes);
        Assert.Equal(0xFFABCDEFu, mesh.Appearance.ColorArgb);
        Assert.Equal(materialId, mesh.Appearance.MaterialId);
    }

    [Fact]
    public void VisualBuilderResolvesByParentAppearanceAcrossNestedInstancePath()
    {
        var materialId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();
        var material = new ThreeDmMaterialInfo(materialId, "InstanceMaterial", 0xFF224466, 0.4);
        var member = MeshObject(
            memberId,
            null,
            null,
            "ColorFromParent",
            "MaterialFromParent",
            0xFF00FF00);
        var instance = new ThreeDmSceneObject(
            instanceId,
            "Block A",
            null,
            ThreeDmGeometryKind.InstanceReference,
            UnitBounds,
            materialId,
            true,
            0xFFCC3300,
            "ColorFromObject",
            "MaterialFromObject",
            new ThreeDmInstanceReferenceGeometryData(definitionId, IdentityTransform, UnitBounds));
        var document = Document([member, instance], [], [material]) with
        {
            InstanceDefinitions =
            [
                new ThreeDmInstanceDefinitionInfo(definitionId, "Definition", string.Empty, null, [memberId]),
            ],
        };

        var scene = new ThreeDmVisualRenderSceneBuilder().Build(document);

        var mesh = Assert.Single(scene.Meshes);
        Assert.Equal(memberId, mesh.SourceObjectId);
        Assert.Equal([instanceId], mesh.InstancePath);
        Assert.Equal(0xFFCC3300u, mesh.Appearance.ColorArgb);
        Assert.Equal(materialId, mesh.Appearance.MaterialId);
        Assert.Equal(0.6, mesh.Appearance.Opacity, 12);
    }

    [Fact]
    public void VisualBuilderSuppressesObjectsWhoseAncestorLayerIsHidden()
    {
        var parentLayerId = Guid.NewGuid();
        var childLayerId = Guid.NewGuid();
        var parent = new ThreeDmLayerInfo(parentLayerId, "Hidden parent", null, false, false, 0xFFFFFFFF, 0);
        var child = new ThreeDmLayerInfo(childLayerId, "Visible child", parentLayerId, true, false, 0xFFFFFFFF, 0);
        var sceneObject = MeshObject(
            Guid.NewGuid(),
            childLayerId,
            null,
            "ColorFromLayer",
            "MaterialFromLayer",
            0xFFFFFFFF);
        var document = Document([sceneObject], [parent, child], []);

        var scene = new ThreeDmVisualRenderSceneBuilder().Build(document);

        Assert.Empty(scene.Meshes);
    }

    private static ThreeDmSceneObject MeshObject(
        Guid id,
        Guid? layerId,
        Guid? materialId,
        string colorSource,
        string materialSource,
        uint objectColor) =>
        new(
            id,
            "Mesh",
            layerId,
            ThreeDmGeometryKind.Mesh,
            UnitBounds,
            materialId,
            true,
            objectColor,
            colorSource,
            materialSource,
            UnitTriangle);

    private static ThreeDmSceneDocument Document(
        IReadOnlyList<ThreeDmSceneObject> objects,
        IReadOnlyList<ThreeDmLayerInfo> layers,
        IReadOnlyList<ThreeDmMaterialInfo> materials) =>
        new("visual.3dm", objects, UnitBounds, Array.Empty<ThreeDmImportDiagnostic>())
        {
            Layers = layers,
            Materials = materials,
        };

    private static readonly BoundingBox3d UnitBounds =
        BoundingBox3d.FromPoints(new Point3d(0, 0, 0), new Point3d(1, 1, 0));

    private static readonly ThreeDmMeshGeometryData UnitTriangle = new(
        [new Point3d(0, 0, 0), new Point3d(1, 0, 0), new Point3d(0, 1, 0)],
        [new ThreeDmMeshFace(0, 1, 2, null)],
        [new Vector3d(0, 0, 1), new Vector3d(0, 0, 1), new Vector3d(0, 0, 1)],
        Array.Empty<ThreeDmTextureCoordinate>(),
        false,
        UnitBounds);

    private static readonly Transform3d IdentityTransform = new(
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1);
}
