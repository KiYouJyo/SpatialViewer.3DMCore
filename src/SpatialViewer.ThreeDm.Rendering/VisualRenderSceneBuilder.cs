using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Rendering;

public sealed class ThreeDmVisualRenderSceneBuilder
{
    private readonly ThreeDmRenderSceneBuilder _geometryBuilder = new();

    public int CacheEntryCount => _geometryBuilder.CacheEntryCount;

    public void ClearCache() => _geometryBuilder.ClearCache();

    public ThreeDmRenderScene Build(
        ThreeDmSceneDocument document,
        ThreeDmVisualRenderSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        settings ??= new ThreeDmVisualRenderSettings();
        var geometryScene = _geometryBuilder.Build(document, settings.Tessellation);
        var appearanceScene = ThreeDmVisualFidelityResolver.Apply(document, geometryScene);
        return ThreeDmDisplayModeResolver.Apply(document, appearanceScene, settings.DisplayMode);
    }
}
