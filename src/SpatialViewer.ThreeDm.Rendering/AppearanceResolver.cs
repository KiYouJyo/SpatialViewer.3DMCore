using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Rendering;

public sealed record ThreeDmRenderAppearance(
    uint ColorArgb,
    double Opacity,
    Guid? MaterialId,
    uint? SpecularColorArgb = null,
    uint? EmissionColorArgb = null,
    double Shine = 0,
    double Reflectivity = 0)
{
    public static ThreeDmRenderAppearance Default { get; } = new(0xFFFFFFFF, 1, null);
}

internal static class ThreeDmAppearanceResolver
{
    public static ThreeDmRenderAppearance Resolve(
        ThreeDmSceneObject sceneObject,
        IReadOnlyDictionary<Guid, ThreeDmLayerInfo> layersById,
        IReadOnlyDictionary<Guid, ThreeDmMaterialInfo> materialsById,
        ThreeDmRenderAppearance? inheritedAppearance = null)
    {
        ArgumentNullException.ThrowIfNull(sceneObject);
        ArgumentNullException.ThrowIfNull(layersById);
        ArgumentNullException.ThrowIfNull(materialsById);

        layersById.TryGetValue(sceneObject.LayerId ?? Guid.Empty, out var layer);
        var materialId = ResolveMaterialId(sceneObject, layer, inheritedAppearance);
        materialsById.TryGetValue(materialId ?? Guid.Empty, out var material);
        var color = ResolveColor(sceneObject, layer, material, inheritedAppearance);
        var materialOpacity = material is null ? 1.0 : 1.0 - Math.Clamp(material.Transparency, 0, 1);
        var colorOpacity = ((color >> 24) & 0xFF) / 255.0;

        return new ThreeDmRenderAppearance(
            color,
            Math.Clamp(materialOpacity * colorOpacity, 0, 1),
            materialId,
            material?.SpecularColorArgb,
            material?.EmissionColorArgb,
            material?.Shine ?? 0,
            material?.Reflectivity ?? 0);
    }

    public static bool IsLayerEffectivelyVisible(
        Guid? layerId,
        IReadOnlyDictionary<Guid, ThreeDmLayerInfo> layersById)
    {
        if (layerId is null)
        {
            return true;
        }

        var currentId = layerId;
        var visited = new HashSet<Guid>();
        while (currentId is Guid id && layersById.TryGetValue(id, out var layer))
        {
            if (!visited.Add(id) || !layer.IsVisible)
            {
                return false;
            }

            currentId = layer.ParentLayerId;
        }

        return true;
    }

    private static Guid? ResolveMaterialId(
        ThreeDmSceneObject sceneObject,
        ThreeDmLayerInfo? layer,
        ThreeDmRenderAppearance? inheritedAppearance)
    {
        if (SourceIs(sceneObject.MaterialSource, "MaterialFromParent"))
        {
            return inheritedAppearance?.MaterialId ?? layer?.RenderMaterialId ?? sceneObject.MaterialId;
        }

        if (SourceIs(sceneObject.MaterialSource, "MaterialFromLayer"))
        {
            return layer?.RenderMaterialId;
        }

        if (SourceIs(sceneObject.MaterialSource, "MaterialFromObject"))
        {
            return sceneObject.MaterialId;
        }

        return sceneObject.MaterialId ?? layer?.RenderMaterialId ?? inheritedAppearance?.MaterialId;
    }

    private static uint ResolveColor(
        ThreeDmSceneObject sceneObject,
        ThreeDmLayerInfo? layer,
        ThreeDmMaterialInfo? material,
        ThreeDmRenderAppearance? inheritedAppearance)
    {
        if (SourceIs(sceneObject.ColorSource, "ColorFromParent"))
        {
            return inheritedAppearance?.ColorArgb ?? layer?.ColorArgb ?? sceneObject.ObjectColorArgb ?? ThreeDmRenderAppearance.Default.ColorArgb;
        }

        if (SourceIs(sceneObject.ColorSource, "ColorFromMaterial"))
        {
            return material?.DiffuseColorArgb ?? layer?.ColorArgb ?? sceneObject.ObjectColorArgb ?? ThreeDmRenderAppearance.Default.ColorArgb;
        }

        if (SourceIs(sceneObject.ColorSource, "ColorFromObject"))
        {
            return sceneObject.ObjectColorArgb ?? layer?.ColorArgb ?? ThreeDmRenderAppearance.Default.ColorArgb;
        }

        if (SourceIs(sceneObject.ColorSource, "ColorFromLayer"))
        {
            return layer?.ColorArgb ?? sceneObject.ObjectColorArgb ?? ThreeDmRenderAppearance.Default.ColorArgb;
        }

        return layer?.ColorArgb ?? sceneObject.ObjectColorArgb ?? material?.DiffuseColorArgb ?? inheritedAppearance?.ColorArgb ?? ThreeDmRenderAppearance.Default.ColorArgb;
    }

    private static bool SourceIs(string? source, string value) =>
        string.Equals(source, value, StringComparison.OrdinalIgnoreCase);
}
