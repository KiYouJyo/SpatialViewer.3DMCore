using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Rendering;

public sealed record ThreeDmPreparedRenderScene(
    ThreeDmRenderDisplayMode DisplayMode,
    ThreeDmSharedMeshScene SharedMeshes,
    IReadOnlyList<ThreeDmRenderCurve> Curves,
    IReadOnlyList<ThreeDmRenderPointSet> PointSets,
    IReadOnlyList<ThreeDmRenderDiagnostic> Diagnostics);

public sealed class ThreeDmPreparedRenderSceneBuilder
{
    private readonly ThreeDmSharedMeshSceneBuilder _sharedBuilder = new();
    private readonly ThreeDmVisualRenderSceneBuilder _visualBuilder = new();

    public int SharedMeshCacheEntryCount => _sharedBuilder.CacheEntryCount;
    public int PrimitiveCacheEntryCount => _visualBuilder.CacheEntryCount;

    public void ClearCache()
    {
        _sharedBuilder.ClearCache();
        _visualBuilder.ClearCache();
    }

    public ThreeDmPreparedRenderScene Build(
        ThreeDmSceneDocument document,
        ThreeDmVisualRenderSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        settings ??= new ThreeDmVisualRenderSettings();

        var shared = _sharedBuilder.Build(document, settings.Tessellation);
        var nonMesh = _visualBuilder.Build(
            document,
            settings,
            ThreeDmRenderPrimitiveMask.Curves | ThreeDmRenderPrimitiveMask.PointSets);
        var curves = settings.DisplayMode == ThreeDmRenderDisplayMode.Shaded
            ? RemoveFilledSemanticEdges(document, shared, nonMesh.Curves)
            : nonMesh.Curves;

        var diagnostics = shared.Diagnostics
            .Concat(nonMesh.Diagnostics)
            .Distinct()
            .ToArray();

        return new ThreeDmPreparedRenderScene(
            settings.DisplayMode,
            shared,
            curves,
            nonMesh.PointSets,
            diagnostics);
    }

    private static IReadOnlyList<ThreeDmRenderCurve> RemoveFilledSemanticEdges(
        ThreeDmSceneDocument document,
        ThreeDmSharedMeshScene shared,
        IReadOnlyList<ThreeDmRenderCurve> curves)
    {
        var kinds = document.Objects.ToDictionary(item => item.Id, item => item.GeometryKind);
        var filledIds = shared.Instances.Select(item => item.SourceObjectId).ToHashSet();
        return curves.Where(curve =>
        {
            if (!filledIds.Contains(curve.SourceObjectId) ||
                !kinds.TryGetValue(curve.SourceObjectId, out var kind))
            {
                return true;
            }

            return kind is not ThreeDmGeometryKind.Brep and not ThreeDmGeometryKind.Extrusion;
        }).ToArray();
    }
}
