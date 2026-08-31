using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Rendering;

internal static class ThreeDmVisualFidelityResolver
{
    public static ThreeDmRenderScene Apply(ThreeDmSceneDocument document, ThreeDmRenderScene scene)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(scene);

        var objectsById = document.Objects.ToDictionary(item => item.Id);
        var layersById = document.Layers.ToDictionary(item => item.Id);
        var materialsById = document.Materials.ToDictionary(item => item.Id);

        var meshes = scene.Meshes
            .Where(item => IsPrimitiveVisible(item.SourceObjectId, item.InstancePath, objectsById, layersById))
            .Select(item =>
            {
                var appearance = ResolvePrimitiveAppearance(
                    item.SourceObjectId,
                    item.InstancePath,
                    objectsById,
                    layersById,
                    materialsById);
                return item with
                {
                    MaterialId = appearance.MaterialId,
                    ColorArgb = appearance.ColorArgb,
                    Appearance = appearance,
                };
            })
            .ToArray();

        var pointSets = scene.PointSets
            .Where(item => IsPrimitiveVisible(item.SourceObjectId, item.InstancePath, objectsById, layersById))
            .Select(item => item with
            {
                Appearance = ResolvePrimitiveAppearance(
                    item.SourceObjectId,
                    item.InstancePath,
                    objectsById,
                    layersById,
                    materialsById),
            })
            .ToArray();

        var curves = scene.Curves
            .Where(item => IsPrimitiveVisible(item.SourceObjectId, item.InstancePath, objectsById, layersById))
            .Select(item => item with
            {
                Appearance = ResolvePrimitiveAppearance(
                    item.SourceObjectId,
                    item.InstancePath,
                    objectsById,
                    layersById,
                    materialsById),
            })
            .ToArray();

        return scene with
        {
            Meshes = meshes,
            PointSets = pointSets,
            Curves = curves,
        };
    }

    private static ThreeDmRenderAppearance ResolvePrimitiveAppearance(
        Guid sourceObjectId,
        IReadOnlyList<Guid> instancePath,
        IReadOnlyDictionary<Guid, ThreeDmSceneObject> objectsById,
        IReadOnlyDictionary<Guid, ThreeDmLayerInfo> layersById,
        IReadOnlyDictionary<Guid, ThreeDmMaterialInfo> materialsById)
    {
        ThreeDmRenderAppearance? inherited = null;
        foreach (var instanceId in instancePath)
        {
            if (objectsById.TryGetValue(instanceId, out var instanceObject))
            {
                inherited = ThreeDmAppearanceResolver.Resolve(instanceObject, layersById, materialsById, inherited);
            }
        }

        return objectsById.TryGetValue(sourceObjectId, out var sourceObject)
            ? ThreeDmAppearanceResolver.Resolve(sourceObject, layersById, materialsById, inherited)
            : inherited ?? ThreeDmRenderAppearance.Default;
    }

    private static bool IsPrimitiveVisible(
        Guid sourceObjectId,
        IReadOnlyList<Guid> instancePath,
        IReadOnlyDictionary<Guid, ThreeDmSceneObject> objectsById,
        IReadOnlyDictionary<Guid, ThreeDmLayerInfo> layersById)
    {
        foreach (var instanceId in instancePath)
        {
            if (objectsById.TryGetValue(instanceId, out var instanceObject) && !IsObjectVisible(instanceObject, layersById))
            {
                return false;
            }
        }

        return !objectsById.TryGetValue(sourceObjectId, out var sourceObject) || IsObjectVisible(sourceObject, layersById);
    }

    private static bool IsObjectVisible(
        ThreeDmSceneObject sceneObject,
        IReadOnlyDictionary<Guid, ThreeDmLayerInfo> layersById) =>
        sceneObject.IsVisible && ThreeDmAppearanceResolver.IsLayerEffectivelyVisible(sceneObject.LayerId, layersById);
}
