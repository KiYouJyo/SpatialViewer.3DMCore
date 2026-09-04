using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Rendering;

public sealed record ThreeDmPreparedMeshDrawPolicy(
    int GeometryIndex,
    bool DrawFill,
    bool DrawWireIndices);

public sealed record ThreeDmPreparedRenderScene(
    ThreeDmRenderDisplayMode DisplayMode,
    ThreeDmSharedMeshScene SharedMeshes,
    IReadOnlyList<ThreeDmRenderCurve> Curves,
    IReadOnlyList<ThreeDmRenderPointSet> PointSets,
    IReadOnlyList<ThreeDmRenderDiagnostic> Diagnostics)
{
    public IReadOnlyList<ThreeDmPreparedMeshDrawPolicy> MeshDrawPolicies { get; init; } =
        Array.Empty<ThreeDmPreparedMeshDrawPolicy>();
}

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
            diagnostics)
        {
            MeshDrawPolicies = CreateMeshDrawPolicies(document, shared, settings.DisplayMode),
        };
    }

    private static ThreeDmPreparedMeshDrawPolicy[] CreateMeshDrawPolicies(
        ThreeDmSceneDocument document,
        ThreeDmSharedMeshScene shared,
        ThreeDmRenderDisplayMode displayMode)
    {
        var kinds = document.Objects.ToDictionary(item => item.Id, item => item.GeometryKind);
        return shared.Geometries.Select(geometry =>
        {
            kinds.TryGetValue(geometry.SourceObjectId, out var kind);
            var hasSemanticWire = kind is ThreeDmGeometryKind.Brep or ThreeDmGeometryKind.Extrusion;
            return displayMode switch
            {
                ThreeDmRenderDisplayMode.Shaded =>
                    new ThreeDmPreparedMeshDrawPolicy(geometry.GeometryIndex, true, false),
                ThreeDmRenderDisplayMode.ShadedWithEdges =>
                    new ThreeDmPreparedMeshDrawPolicy(geometry.GeometryIndex, true, !hasSemanticWire),
                ThreeDmRenderDisplayMode.Wireframe =>
                    new ThreeDmPreparedMeshDrawPolicy(geometry.GeometryIndex, false, !hasSemanticWire),
                _ =>
                    new ThreeDmPreparedMeshDrawPolicy(geometry.GeometryIndex, true, false),
            };
        }).ToArray();
    }

    private static ThreeDmRenderCurve[] RemoveFilledSemanticEdges(
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
