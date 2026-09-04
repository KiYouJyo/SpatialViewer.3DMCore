using SpatialViewer.ThreeDm.Core;
using SpatialViewer.ThreeDm.Rendering;

namespace SpatialViewer.ThreeDm.Integration;

public readonly record struct ThreeDmSelectionId(
    Guid SourceObjectId,
    int? SourceSubobjectIndex,
    string InstancePathKey)
{
    public static ThreeDmSelectionId Create(
        Guid sourceObjectId,
        int? sourceSubobjectIndex,
        IReadOnlyList<Guid>? instancePath = null) =>
        new(
            sourceObjectId,
            sourceSubobjectIndex,
            instancePath is null || instancePath.Count == 0
                ? string.Empty
                : string.Join('/', instancePath.Select(id => id.ToString("N"))));
}

public sealed record ThreeDmSelectionProperties(
    ThreeDmSelectionId SelectionId,
    string? Name,
    ThreeDmGeometryKind GeometryKind,
    BoundingBox3d Bounds,
    Guid? LayerId,
    string? LayerName,
    Guid? MaterialId,
    string? MaterialName,
    bool SourceVisible,
    bool EffectiveVisible,
    IReadOnlyList<Guid> InstancePath,
    IReadOnlyList<string> InstanceNames)
{
    public uint? ObjectColorArgb { get; init; }
    public string? ColorSource { get; init; }
    public string? MaterialSource { get; init; }
    public IReadOnlyDictionary<string, string> GeometryDetails { get; init; } =
        new Dictionary<string, string>();
}

public static class ThreeDmSelectionCatalog
{
    public static IReadOnlyList<ThreeDmSelectionId> Create(ThreeDmRenderScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var result = new HashSet<ThreeDmSelectionId>();
        foreach (var mesh in scene.Meshes)
        {
            result.Add(ThreeDmSelectionId.Create(
                mesh.SourceObjectId,
                mesh.SourceSubobjectIndex,
                mesh.InstancePath));
        }

        foreach (var curve in scene.Curves)
        {
            result.Add(ThreeDmSelectionId.Create(
                curve.SourceObjectId,
                curve.SourceSubobjectIndex,
                curve.InstancePath));
        }

        foreach (var pointSet in scene.PointSets)
        {
            result.Add(ThreeDmSelectionId.Create(
                pointSet.SourceObjectId,
                null,
                pointSet.InstancePath));
        }

        return result
            .OrderBy(item => item.SourceObjectId)
            .ThenBy(item => item.SourceSubobjectIndex)
            .ThenBy(item => item.InstancePathKey, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<ThreeDmSelectionId> Create(ThreeDmSharedMeshScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        return scene.Instances
            .Select(instance => ThreeDmSelectionId.Create(
                instance.SourceObjectId,
                instance.SourceSubobjectIndex,
                instance.InstancePath))
            .Distinct()
            .OrderBy(item => item.SourceObjectId)
            .ThenBy(item => item.SourceSubobjectIndex)
            .ThenBy(item => item.InstancePathKey, StringComparer.Ordinal)
            .ToArray();
    }

    public static ThreeDmSelectionProperties? Resolve(
        ThreeDmSceneDocument document,
        ThreeDmSelectionId selectionId,
        ThreeDmLayerVisibilityOverrides? overrides = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var sceneObject = document.Objects.FirstOrDefault(item => item.Id == selectionId.SourceObjectId);
        if (sceneObject is null)
        {
            return null;
        }

        var layer = sceneObject.LayerId is Guid layerId
            ? document.Layers.FirstOrDefault(item => item.Id == layerId)
            : null;
        var material = sceneObject.MaterialId is Guid materialId
            ? document.Materials.FirstOrDefault(item => item.Id == materialId)
            : null;
        var instancePath = ParseInstancePath(selectionId.InstancePathKey);
        var instanceNames = instancePath
            .Select(id => document.Objects.FirstOrDefault(item => item.Id == id)?.Name ?? id.ToString("D"))
            .ToArray();
        var effectiveVisible =
            IsObjectEffectivelyVisible(sceneObject, document, overrides) &&
            instancePath.All(id =>
                document.Objects.FirstOrDefault(item => item.Id == id) is not { } instanceObject ||
                IsObjectEffectivelyVisible(instanceObject, document, overrides));

        return new ThreeDmSelectionProperties(
            selectionId,
            sceneObject.Name,
            sceneObject.GeometryKind,
            sceneObject.Bounds,
            sceneObject.LayerId,
            layer?.Name,
            sceneObject.MaterialId,
            material?.Name,
            sceneObject.SourceObjectVisible ?? sceneObject.IsVisible,
            effectiveVisible,
            instancePath,
            instanceNames)
        {
            ObjectColorArgb = sceneObject.ObjectColorArgb,
            ColorSource = sceneObject.ColorSource,
            MaterialSource = sceneObject.MaterialSource,
            GeometryDetails = ThreeDmInspection.CreateGeometryDetails(sceneObject.Geometry),
        };
    }

    private static bool IsObjectEffectivelyVisible(
        ThreeDmSceneObject sceneObject,
        ThreeDmSceneDocument document,
        ThreeDmLayerVisibilityOverrides? overrides)
    {
        if (!(sceneObject.SourceObjectVisible ?? sceneObject.IsVisible))
        {
            return false;
        }

        return sceneObject.LayerId is not Guid layerId ||
               ThreeDmLayerTreeBuilder.IsEffectivelyVisible(layerId, document, overrides);
    }

    private static Guid[] ParseInstancePath(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return [];
        }

        var parts = key.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var result = new Guid[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!Guid.TryParseExact(parts[i], "N", out result[i]))
            {
                throw new ArgumentException("Selection instance path is invalid.", nameof(key));
            }
        }

        return result;
    }
}
