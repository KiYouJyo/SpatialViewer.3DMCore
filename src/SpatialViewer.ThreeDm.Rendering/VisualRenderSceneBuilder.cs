using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Rendering;

public sealed class ThreeDmVisualRenderSceneBuilder
{
    private readonly ThreeDmRenderSceneBuilder _geometryBuilder = new();

    public int CacheEntryCount => _geometryBuilder.CacheEntryCount;

    public void ClearCache() => _geometryBuilder.ClearCache();

    public ThreeDmRenderScene Build(
        ThreeDmSceneDocument document,
        ThreeDmTessellationSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var geometryScene = _geometryBuilder.Build(document, settings);
        return ThreeDmVisualFidelityResolver.Apply(document, geometryScene);
    }
}
