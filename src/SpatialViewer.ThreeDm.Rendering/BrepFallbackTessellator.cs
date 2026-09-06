using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Rendering;

internal static class ThreeDmBrepFallbackTessellator
{
    public static ThreeDmRenderMesh[] Tessellate(
        Guid sourceObjectId,
        ThreeDmBrepGeometryData brep,
        ThreeDmTessellationSettings settings,
        double modelAbsoluteTolerance,
        Guid? materialId,
        uint? colorArgb)
    {
        ArgumentNullException.ThrowIfNull(brep);
        ArgumentNullException.ThrowIfNull(settings);

        var trims = brep.Trims.ToDictionary(item => item.Index);
        var loops = brep.Loops.ToDictionary(item => item.Index);
        var result = new List<ThreeDmRenderMesh>(brep.Faces.Count);

        foreach (var face in brep.Faces)
        {
            var mesh = ThreeDmNurbsSurfaceTessellator.Tessellate(
                sourceObjectId,
                face.Surface,
                settings,
                modelAbsoluteTolerance,
                materialId,
                colorArgb) with
            {
                SourceSubobjectIndex = face.Index,
            };

            mesh = ApplyTrimLoops(sourceObjectId, mesh, face, loops, trims, settings, modelAbsoluteTolerance);
            if (face.OrientationIsReversed)
            {
                mesh = ReverseOrientation(mesh);
            }

            if (mesh.Indices.Count >= 3)
            {
                result.Add(mesh);
            }
        }

        return result.ToArray();
    }

    private static ThreeDmRenderMesh ApplyTrimLoops(
        Guid sourceObjectId,
        ThreeDmRenderMesh mesh,
        ThreeDmBrepFaceData face,
        Dictionary<int, ThreeDmBrepLoopData> loops,
        Dictionary<int, ThreeDmBrepTrimData> trims,
        ThreeDmTessellationSettings settings,
        double modelAbsoluteTolerance)
    {
        if (mesh.TextureCoordinates.Count != mesh.Vertices.Count || face.LoopIndices.Count == 0)
        {
            return mesh;
        }

        var outer = new List<NormalizedLoop>();
        var inner = new List<NormalizedLoop>();
        var domainU = NurbsSurfaceEvaluator.GetDomainU(face.Surface);
        var domainV = NurbsSurfaceEvaluator.GetDomainV(face.Surface);

        foreach (var loopIndex in face.LoopIndices)
        {
            if (!loops.TryGetValue(loopIndex, out var loop))
            {
                continue;
            }

            var polygon = BuildLoopPolygon(
                sourceObjectId,
                loop,
                trims,
                settings,
                modelAbsoluteTolerance,
                domainU,
                domainV);
            if (polygon.Count < 3)
            {
                continue;
            }

            if (loop.LoopType.Contains("Inner", StringComparison.OrdinalIgnoreCase))
            {
                inner.Add(new NormalizedLoop(polygon));
            }
            else if (loop.LoopType.Contains("Outer", StringComparison.OrdinalIgnoreCase))
            {
                outer.Add(new NormalizedLoop(polygon));
            }
        }

        if (outer.Count == 0 && inner.Count == 0)
        {
            return mesh;
        }

        var indices = new List<int>(mesh.Indices.Count);
        for (var index = 0; index + 2 < mesh.Indices.Count; index += 3)
        {
            var a = mesh.Indices[index];
            var b = mesh.Indices[index + 1];
            var c = mesh.Indices[index + 2];
            var uvA = mesh.TextureCoordinates[a];
            var uvB = mesh.TextureCoordinates[b];
            var uvC = mesh.TextureCoordinates[c];
            var center = new UvPoint(
                (uvA.U + uvB.U + uvC.U) / 3,
                (uvA.V + uvB.V + uvC.V) / 3);

            var insideOuter = outer.Count == 0 || outer.Any(loop => Contains(loop.Points, center));
            var insideInner = inner.Any(loop => Contains(loop.Points, center));
            if (!insideOuter || insideInner)
            {
                continue;
            }

            indices.Add(a);
            indices.Add(b);
            indices.Add(c);
        }

        return indices.Count >= 3 ? mesh with { Indices = indices.ToArray() } : mesh;
    }

    private static List<UvPoint> BuildLoopPolygon(
        Guid sourceObjectId,
        ThreeDmBrepLoopData loop,
        Dictionary<int, ThreeDmBrepTrimData> trims,
        ThreeDmTessellationSettings settings,
        double modelAbsoluteTolerance,
        (double Start, double End) domainU,
        (double Start, double End) domainV)
    {
        var polygon = new List<UvPoint>();
        foreach (var trimIndex in loop.TrimIndices)
        {
            if (!trims.TryGetValue(trimIndex, out var trim) || trim.ParameterCurve is null)
            {
                continue;
            }

            var rendered = ThreeDmCurveTessellator.Tessellate(
                sourceObjectId,
                trim.ParameterCurve,
                settings,
                modelAbsoluteTolerance,
                trim.Index);
            var points = trim.IsReversed
                ? rendered.Points.Reverse()
                : rendered.Points;

            foreach (var point in points)
            {
                var normalized = new UvPoint(
                    Normalize(point.X, domainU.Start, domainU.End),
                    Normalize(point.Y, domainV.Start, domainV.End));
                if (polygon.Count == 0 || DistanceSquared(polygon[^1], normalized) > 1e-18)
                {
                    polygon.Add(normalized);
                }
            }
        }

        if (polygon.Count > 2 && DistanceSquared(polygon[0], polygon[^1]) <= 1e-18)
        {
            polygon.RemoveAt(polygon.Count - 1);
        }

        return polygon;
    }

    private static ThreeDmRenderMesh ReverseOrientation(ThreeDmRenderMesh mesh)
    {
        var indices = mesh.Indices.ToArray();
        for (var index = 0; index + 2 < indices.Length; index += 3)
        {
            (indices[index + 1], indices[index + 2]) = (indices[index + 2], indices[index + 1]);
        }

        var normals = mesh.Normals
            .Select(normal => new ThreeDmRenderNormal(-normal.X, -normal.Y, -normal.Z))
            .ToArray();
        return mesh with { Indices = indices, Normals = normals };
    }

    private static bool Contains(IReadOnlyList<UvPoint> polygon, UvPoint point)
    {
        var inside = false;
        for (var i = 0; i < polygon.Count; i++)
        {
            var j = i == 0 ? polygon.Count - 1 : i - 1;
            var a = polygon[i];
            var b = polygon[j];
            var intersects = ((a.V > point.V) != (b.V > point.V)) &&
                point.U < ((b.U - a.U) * (point.V - a.V) / ((b.V - a.V) + double.Epsilon)) + a.U;
            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static double Normalize(double value, double start, double end) =>
        end > start ? Math.Clamp((value - start) / (end - start), 0, 1) : 0;

    private static double DistanceSquared(UvPoint a, UvPoint b)
    {
        var u = a.U - b.U;
        var v = a.V - b.V;
        return (u * u) + (v * v);
    }

    private readonly record struct UvPoint(double U, double V);
    private sealed record NormalizedLoop(IReadOnlyList<UvPoint> Points);
}
