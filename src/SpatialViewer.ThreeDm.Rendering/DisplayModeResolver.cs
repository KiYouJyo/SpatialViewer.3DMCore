using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Rendering;

internal static class ThreeDmDisplayModeResolver
{
    public static ThreeDmRenderScene Apply(
        ThreeDmSceneDocument document,
        ThreeDmRenderScene scene,
        ThreeDmRenderDisplayMode displayMode)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(scene);

        return displayMode switch
        {
            ThreeDmRenderDisplayMode.Shaded => ApplyShaded(document, scene),
            ThreeDmRenderDisplayMode.ShadedWithEdges => scene,
            ThreeDmRenderDisplayMode.Wireframe => ApplyWireframe(document, scene),
            _ => scene,
        };
    }

    private static ThreeDmRenderScene ApplyShaded(ThreeDmSceneDocument document, ThreeDmRenderScene scene)
    {
        var kindsById = document.Objects.ToDictionary(item => item.Id, item => item.GeometryKind);
        var filledObjectIds = scene.Meshes.Select(item => item.SourceObjectId).ToHashSet();
        var curves = scene.Curves
            .Where(curve => ShouldKeepCurveInShaded(curve, kindsById, filledObjectIds))
            .ToArray();

        return scene with { Curves = curves };
    }

    private static bool ShouldKeepCurveInShaded(
        ThreeDmRenderCurve curve,
        IReadOnlyDictionary<Guid, ThreeDmGeometryKind> kindsById,
        IReadOnlySet<Guid> filledObjectIds)
    {
        if (!kindsById.TryGetValue(curve.SourceObjectId, out var kind))
        {
            return true;
        }

        if (kind is ThreeDmGeometryKind.Brep or ThreeDmGeometryKind.Extrusion)
        {
            return !filledObjectIds.Contains(curve.SourceObjectId);
        }

        return true;
    }

    private static ThreeDmRenderScene ApplyWireframe(ThreeDmSceneDocument document, ThreeDmRenderScene scene)
    {
        var kindsById = document.Objects.ToDictionary(item => item.Id, item => item.GeometryKind);
        var curves = new List<ThreeDmRenderCurve>(scene.Curves);
        var objectsWithSemanticWire = scene.Curves
            .Where(curve => kindsById.TryGetValue(curve.SourceObjectId, out var kind) &&
                kind is ThreeDmGeometryKind.Brep or ThreeDmGeometryKind.Extrusion or ThreeDmGeometryKind.SubD)
            .Select(curve => curve.SourceObjectId)
            .ToHashSet();

        foreach (var mesh in scene.Meshes)
        {
            if (objectsWithSemanticWire.Contains(mesh.SourceObjectId))
            {
                continue;
            }

            AppendMeshEdges(mesh, curves);
        }

        return scene with
        {
            Meshes = Array.Empty<ThreeDmRenderMesh>(),
            Curves = curves,
        };
    }

    private static void AppendMeshEdges(ThreeDmRenderMesh mesh, List<ThreeDmRenderCurve> output)
    {
        var edges = new HashSet<(int A, int B)>();
        for (var i = 0; i + 2 < mesh.Indices.Count; i += 3)
        {
            AddEdge(mesh.Indices[i], mesh.Indices[i + 1], edges);
            AddEdge(mesh.Indices[i + 1], mesh.Indices[i + 2], edges);
            AddEdge(mesh.Indices[i + 2], mesh.Indices[i], edges);
        }

        var edgeIndex = 0;
        foreach (var (a, b) in edges)
        {
            if ((uint)a >= (uint)mesh.Vertices.Count || (uint)b >= (uint)mesh.Vertices.Count)
            {
                continue;
            }

            output.Add(new ThreeDmRenderCurve(
                mesh.SourceObjectId,
                ThreeDmRenderCurveKind.Line,
                [mesh.Vertices[a], mesh.Vertices[b]],
                false,
                0,
                1_000_000 + edgeIndex++)
            {
                Appearance = mesh.Appearance,
                InstancePath = mesh.InstancePath.ToArray(),
            });
        }
    }

    private static void AddEdge(int left, int right, HashSet<(int A, int B)> edges)
    {
        var edge = left <= right ? (left, right) : (right, left);
        edges.Add(edge);
    }
}
