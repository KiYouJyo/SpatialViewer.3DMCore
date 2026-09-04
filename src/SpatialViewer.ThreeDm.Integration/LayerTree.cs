using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Integration;

public sealed record ThreeDmLayerNode(
    Guid Id,
    string Name,
    Guid? ParentLayerId,
    bool SourceVisible,
    bool? VisibilityOverride,
    bool EffectiveVisible,
    bool IsLocked,
    uint ColorArgb,
    IReadOnlyList<ThreeDmLayerNode> Children);

public sealed class ThreeDmLayerVisibilityOverrides
{
    private readonly Dictionary<Guid, bool> _values = [];

    public int Count => _values.Count;

    public IReadOnlyDictionary<Guid, bool> Snapshot =>
        new Dictionary<Guid, bool>(_values);

    public bool? Get(Guid layerId) =>
        _values.TryGetValue(layerId, out var value) ? value : null;

    public void Set(Guid layerId, bool? visible)
    {
        if (layerId == Guid.Empty)
        {
            throw new ArgumentException("Layer id cannot be empty.", nameof(layerId));
        }

        if (visible is bool value)
        {
            _values[layerId] = value;
        }
        else
        {
            _values.Remove(layerId);
        }
    }

    public void Clear() => _values.Clear();
}

public static class ThreeDmLayerTreeBuilder
{
    public static IReadOnlyList<ThreeDmLayerNode> Build(
        ThreeDmSceneDocument document,
        ThreeDmLayerVisibilityOverrides? overrides = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        overrides ??= new ThreeDmLayerVisibilityOverrides();

        var layersById = document.Layers.ToDictionary(item => item.Id);
        var childIds = document.Layers
            .Where(item => item.ParentLayerId is Guid)
            .GroupBy(item => item.ParentLayerId!.Value)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Id).ToArray());
        var emitted = new HashSet<Guid>();
        var roots = new List<ThreeDmLayerNode>();

        foreach (var layer in document.Layers.Where(layer =>
                     layer.ParentLayerId is not Guid parentId ||
                     parentId == layer.Id ||
                     !layersById.ContainsKey(parentId)))
        {
            if (emitted.Contains(layer.Id))
            {
                continue;
            }

            roots.Add(BuildNode(
                layer,
                layersById,
                childIds,
                overrides,
                new HashSet<Guid>(),
                emitted));
        }

        // Malformed files can contain a pure parent cycle, leaving no natural root.
        // Keep those layers inspectable instead of silently dropping the cycle.
        foreach (var layer in document.Layers)
        {
            if (emitted.Contains(layer.Id))
            {
                continue;
            }

            roots.Add(BuildNode(
                layer,
                layersById,
                childIds,
                overrides,
                new HashSet<Guid>(),
                emitted));
        }

        return roots;
    }

    public static ThreeDmSceneDocument ApplyOverrides(
        ThreeDmSceneDocument document,
        ThreeDmLayerVisibilityOverrides overrides)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(overrides);

        return document with
        {
            Layers = document.Layers
                .Select(layer => layer with { IsVisible = overrides.Get(layer.Id) ?? layer.IsVisible })
                .ToArray(),
        };
    }

    public static bool IsEffectivelyVisible(
        Guid layerId,
        ThreeDmSceneDocument document,
        ThreeDmLayerVisibilityOverrides? overrides = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        overrides ??= new ThreeDmLayerVisibilityOverrides();
        var layersById = document.Layers.ToDictionary(item => item.Id);
        return IsEffectivelyVisible(layerId, layersById, overrides);
    }

    private static ThreeDmLayerNode BuildNode(
        ThreeDmLayerInfo layer,
        IReadOnlyDictionary<Guid, ThreeDmLayerInfo> layersById,
        IReadOnlyDictionary<Guid, Guid[]> childIds,
        ThreeDmLayerVisibilityOverrides overrides,
        HashSet<Guid> path,
        HashSet<Guid> emitted)
    {
        path.Add(layer.Id);
        emitted.Add(layer.Id);

        var children = childIds.TryGetValue(layer.Id, out var ids)
            ? ids
                .Where(id => layersById.ContainsKey(id) && !path.Contains(id) && !emitted.Contains(id))
                .Select(id => BuildNode(
                    layersById[id],
                    layersById,
                    childIds,
                    overrides,
                    new HashSet<Guid>(path),
                    emitted))
                .ToArray()
            : Array.Empty<ThreeDmLayerNode>();

        return new ThreeDmLayerNode(
            layer.Id,
            layer.Name,
            layer.ParentLayerId,
            layer.IsVisible,
            overrides.Get(layer.Id),
            IsEffectivelyVisible(layer.Id, layersById, overrides),
            layer.IsLocked,
            layer.ColorArgb,
            children);
    }

    private static bool IsEffectivelyVisible(
        Guid layerId,
        IReadOnlyDictionary<Guid, ThreeDmLayerInfo> layersById,
        ThreeDmLayerVisibilityOverrides overrides)
    {
        var current = layerId;
        var visited = new HashSet<Guid>();
        while (layersById.TryGetValue(current, out var layer))
        {
            if (!visited.Add(current))
            {
                return false;
            }

            if (!(overrides.Get(current) ?? layer.IsVisible))
            {
                return false;
            }

            if (layer.ParentLayerId is not Guid parentId)
            {
                return true;
            }

            current = parentId;
        }

        return true;
    }
}
