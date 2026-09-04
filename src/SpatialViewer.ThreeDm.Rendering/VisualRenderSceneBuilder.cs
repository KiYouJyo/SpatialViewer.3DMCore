using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Rendering;

public sealed class ThreeDmVisualRenderSceneBuilder
{
    private readonly ThreeDmRenderSceneBuilder _geometryBuilder = new();

    public int CacheEntryCount => _geometryBuilder.CacheEntryCount;

    public void ClearCache() => _geometryBuilder.ClearCache();

    public ThreeDmRenderScene Build(
        ThreeDmSceneDocument document,
        ThreeDmVisualRenderSettings? settings = null) =>
        Build(document, settings, ThreeDmRenderPrimitiveMask.All);

    public ThreeDmRenderScene Build(
        ThreeDmSceneDocument document,
        ThreeDmVisualRenderSettings? settings,
        ThreeDmRenderPrimitiveMask primitiveMask)
    {
        ArgumentNullException.ThrowIfNull(document);
        settings ??= new ThreeDmVisualRenderSettings();

        var geometryDocument = document with
        {
            Objects = document.Objects
                .Select(item => item with { IsVisible = item.SourceObjectVisible ?? item.IsVisible })
                .ToArray(),
        };
        var geometryScene = _geometryBuilder.Build(geometryDocument, settings.Tessellation, primitiveMask);
        var appearanceScene = ThreeDmVisualFidelityResolver.Apply(document, geometryScene);
        return ThreeDmDisplayModeResolver.Apply(document, appearanceScene, settings.DisplayMode);
    }
}
