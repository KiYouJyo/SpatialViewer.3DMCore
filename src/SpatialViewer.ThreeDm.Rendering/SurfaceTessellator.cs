using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Rendering;

public static class ThreeDmNurbsSurfaceTessellator
{
    public static ThreeDmRenderMesh Tessellate(
        Guid sourceObjectId,
        ThreeDmNurbsSurfaceGeometryData surface,
        ThreeDmTessellationSettings settings,
        double modelAbsoluteTolerance = 0,
        Guid? materialId = null,
        uint? colorArgb = null)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(settings);

        var tolerance = settings.ResolveChordTolerance(surface.Bounds, modelAbsoluteTolerance);
        var maxSegments = Math.Max(1, settings.MaxSurfaceSegmentsPerDirection);
        var uParameters = new SortedSet<double>(NurbsSurfaceEvaluator.GetSeedParametersU(surface));
        var vParameters = new SortedSet<double>(NurbsSurfaceEvaluator.GetSeedParametersV(surface));
        var pointCache = new Dictionary<(double U, double V), Point3d>();

        RefineParameterGrid(
            surface,
            uParameters,
            vParameters,
            tolerance,
            maxSegments,
            settings.MaximumSurfaceRefinementDepth,
            pointCache);

        var uValues = uParameters.ToArray();
        var vValues = vParameters.ToArray();
        var vertexCount = checked(uValues.Length * vValues.Length);
        var vertices = new ThreeDmRenderVertex[vertexCount];
        var textureCoordinates = new ThreeDmRenderTextureCoordinate[vertexCount];
        var evaluatedPoints = new Point3d[vertexCount];
        var domainU = NurbsSurfaceEvaluator.GetDomainU(surface);
        var domainV = NurbsSurfaceEvaluator.GetDomainV(surface);

        for (var uIndex = 0; uIndex < uValues.Length; uIndex++)
        {
            for (var vIndex = 0; vIndex < vValues.Length; vIndex++)
            {
                var index = GridIndex(uIndex, vIndex, vValues.Length);
                var point = EvaluateCached(surface, uValues[uIndex], vValues[vIndex], pointCache);
                evaluatedPoints[index] = point;
                vertices[index] = new ThreeDmRenderVertex(point.X, point.Y, point.Z);
                textureCoordinates[index] = new ThreeDmRenderTextureCoordinate(
                    NormalizeParameter(uValues[uIndex], domainU.Start, domainU.End),
                    NormalizeParameter(vValues[vIndex], domainV.Start, domainV.End));
            }
        }

        var normals = BuildNormals(evaluatedPoints, uValues.Length, vValues.Length);
        var indices = BuildIndices(uValues.Length, vValues.Length);

        return new ThreeDmRenderMesh(sourceObjectId, vertices, indices)
        {
            Normals = normals,
            TextureCoordinates = textureCoordinates,
            MaterialId = materialId,
            ColorArgb = colorArgb,
        };
    }

    private static void RefineParameterGrid(
        ThreeDmNurbsSurfaceGeometryData surface,
        SortedSet<double> uParameters,
        SortedSet<double> vParameters,
        double tolerance,
        int maxSegmentsPerDirection,
        int maxDepth,
        Dictionary<(double U, double V), Point3d> pointCache)
    {
        for (var depth = 0; depth < maxDepth; depth++)
        {
            var uValues = uParameters.ToArray();
            var vValues = vParameters.ToArray();
            var splitU = new SortedSet<double>();
            var splitV = new SortedSet<double>();

            for (var uIndex = 0; uIndex < uValues.Length - 1; uIndex++)
            {
                var u0 = uValues[uIndex];
                var u1 = uValues[uIndex + 1];
                for (var vIndex = 0; vIndex < vValues.Length - 1; vIndex++)
                {
                    var v0 = vValues[vIndex];
                    var v1 = vValues[vIndex + 1];
                    if (!CellExceedsTolerance(surface, u0, u1, v0, v1, tolerance, pointCache))
                    {
                        continue;
                    }

                    if (uParameters.Count - 1 < maxSegmentsPerDirection)
                    {
                        splitU.Add((u0 + u1) * 0.5);
                    }

                    if (vParameters.Count - 1 < maxSegmentsPerDirection)
                    {
                        splitV.Add((v0 + v1) * 0.5);
                    }
                }
            }

            if (splitU.Count == 0 && splitV.Count == 0)
            {
                break;
            }

            AddUntilBudget(uParameters, splitU, maxSegmentsPerDirection + 1);
            AddUntilBudget(vParameters, splitV, maxSegmentsPerDirection + 1);
        }
    }

    private static bool CellExceedsTolerance(
        ThreeDmNurbsSurfaceGeometryData surface,
        double u0,
        double u1,
        double v0,
        double v1,
        double tolerance,
        Dictionary<(double U, double V), Point3d> pointCache)
    {
        var um = (u0 + u1) * 0.5;
        var vm = (v0 + v1) * 0.5;
        var p00 = EvaluateCached(surface, u0, v0, pointCache);
        var p10 = EvaluateCached(surface, u1, v0, pointCache);
        var p01 = EvaluateCached(surface, u0, v1, pointCache);
        var p11 = EvaluateCached(surface, u1, v1, pointCache);

        var bottomMid = EvaluateCached(surface, um, v0, pointCache);
        var topMid = EvaluateCached(surface, um, v1, pointCache);
        var leftMid = EvaluateCached(surface, u0, vm, pointCache);
        var rightMid = EvaluateCached(surface, u1, vm, pointCache);
        var center = EvaluateCached(surface, um, vm, pointCache);

        var maximumDeviation = 0.0;
        maximumDeviation = Math.Max(maximumDeviation, Distance(bottomMid, Midpoint(p00, p10)));
        maximumDeviation = Math.Max(maximumDeviation, Distance(topMid, Midpoint(p01, p11)));
        maximumDeviation = Math.Max(maximumDeviation, Distance(leftMid, Midpoint(p00, p01)));
        maximumDeviation = Math.Max(maximumDeviation, Distance(rightMid, Midpoint(p10, p11)));
        maximumDeviation = Math.Max(maximumDeviation, Distance(center, Average(p00, p10, p01, p11)));
        return maximumDeviation > tolerance;
    }

    private static Point3d EvaluateCached(
        ThreeDmNurbsSurfaceGeometryData surface,
        double u,
        double v,
        Dictionary<(double U, double V), Point3d> pointCache)
    {
        var key = (u, v);
        if (pointCache.TryGetValue(key, out var point))
        {
            return point;
        }

        point = NurbsSurfaceEvaluator.Evaluate(surface, u, v);
        pointCache.Add(key, point);
        return point;
    }

    private static void AddUntilBudget(SortedSet<double> destination, SortedSet<double> candidates, int maximumCount)
    {
        foreach (var candidate in candidates)
        {
            if (destination.Count >= maximumCount)
            {
                break;
            }

            destination.Add(candidate);
        }
    }

    private static int[] BuildIndices(int countU, int countV)
    {
        if (countU < 2 || countV < 2)
        {
            return Array.Empty<int>();
        }

        var indices = new int[checked((countU - 1) * (countV - 1) * 6)];
        var cursor = 0;
        for (var u = 0; u < countU - 1; u++)
        {
            for (var v = 0; v < countV - 1; v++)
            {
                var i00 = GridIndex(u, v, countV);
                var i10 = GridIndex(u + 1, v, countV);
                var i11 = GridIndex(u + 1, v + 1, countV);
                var i01 = GridIndex(u, v + 1, countV);

                indices[cursor++] = i00;
                indices[cursor++] = i10;
                indices[cursor++] = i11;
                indices[cursor++] = i00;
                indices[cursor++] = i11;
                indices[cursor++] = i01;
            }
        }

        return indices;
    }

    private static ThreeDmRenderNormal[] BuildNormals(Point3d[] points, int countU, int countV)
    {
        var normals = new ThreeDmRenderNormal[points.Length];
        for (var u = 0; u < countU; u++)
        {
            for (var v = 0; v < countV; v++)
            {
                var left = points[GridIndex(Math.Max(0, u - 1), v, countV)];
                var right = points[GridIndex(Math.Min(countU - 1, u + 1), v, countV)];
                var bottom = points[GridIndex(u, Math.Max(0, v - 1), countV)];
                var top = points[GridIndex(u, Math.Min(countV - 1, v + 1), countV)];
                var du = Subtract(right, left);
                var dv = Subtract(top, bottom);
                normals[GridIndex(u, v, countV)] = Normalize(Cross(du, dv));
            }
        }

        return normals;
    }

    private static int GridIndex(int u, int v, int countV) => checked((u * countV) + v);

    private static double NormalizeParameter(double value, double start, double end) =>
        end > start ? Math.Clamp((value - start) / (end - start), 0, 1) : 0;

    private static Point3d Midpoint(Point3d left, Point3d right) =>
        new((left.X + right.X) * 0.5, (left.Y + right.Y) * 0.5, (left.Z + right.Z) * 0.5);

    private static Point3d Average(Point3d p00, Point3d p10, Point3d p01, Point3d p11) =>
        new(
            (p00.X + p10.X + p01.X + p11.X) * 0.25,
            (p00.Y + p10.Y + p01.Y + p11.Y) * 0.25,
            (p00.Z + p10.Z + p01.Z + p11.Z) * 0.25);

    private static double Distance(Point3d left, Point3d right)
    {
        var dx = left.X - right.X;
        var dy = left.Y - right.Y;
        var dz = left.Z - right.Z;
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    private static Vector3d Subtract(Point3d left, Point3d right) =>
        new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

    private static Vector3d Cross(Vector3d left, Vector3d right) =>
        new(
            (left.Y * right.Z) - (left.Z * right.Y),
            (left.Z * right.X) - (left.X * right.Z),
            (left.X * right.Y) - (left.Y * right.X));

    private static ThreeDmRenderNormal Normalize(Vector3d vector)
    {
        var length = Math.Sqrt((vector.X * vector.X) + (vector.Y * vector.Y) + (vector.Z * vector.Z));
        return length > 1e-15 && double.IsFinite(length)
            ? new ThreeDmRenderNormal(vector.X / length, vector.Y / length, vector.Z / length)
            : new ThreeDmRenderNormal(0, 0, 0);
    }
}
