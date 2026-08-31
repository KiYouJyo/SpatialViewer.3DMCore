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
        var domainU = NurbsSurfaceEvaluator.GetDomainU(surface);
        var domainV = NurbsSurfaceEvaluator.GetDomainV(surface);

        for (var uIndex = 0; uIndex < uValues.Length; uIndex++)
        {
            for (var vIndex = 0; vIndex < vValues.Length; vIndex++)
            {
                var index = GridIndex(uIndex, vIndex, vValues.Length);
                var point = EvaluateCached(surface, uValues[uIndex], vValues[vIndex], pointCache);
                vertices[index] = new ThreeDmRenderVertex(point.X, point.Y, point.Z);
                textureCoordinates[index] = new ThreeDmRenderTextureCoordinate(
                    NormalizeParameter(uValues[uIndex], domainU.Start, domainU.End),
                    NormalizeParameter(vValues[vIndex], domainV.Start, domainV.End));
            }
        }

        var indices = BuildIndices(uValues.Length, vValues.Length);
        var triangleNormals = BuildAreaWeightedNormals(vertices, indices);
        var normals = BuildParameterNormals(surface, uValues, vValues, pointCache, triangleNormals);

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

    private static ThreeDmRenderNormal[] BuildParameterNormals(
        ThreeDmNurbsSurfaceGeometryData surface,
        double[] uValues,
        double[] vValues,
        Dictionary<(double U, double V), Point3d> pointCache,
        ThreeDmRenderNormal[] triangleFallback)
    {
        var normals = new ThreeDmRenderNormal[checked(uValues.Length * vValues.Length)];
        for (var uIndex = 0; uIndex < uValues.Length; uIndex++)
        {
            var uWindow = ParameterWindow(uValues, uIndex);
            for (var vIndex = 0; vIndex < vValues.Length; vIndex++)
            {
                var vWindow = ParameterWindow(vValues, vIndex);
                var u = uValues[uIndex];
                var v = vValues[vIndex];
                var du = Subtract(
                    EvaluateCached(surface, uWindow.High, v, pointCache),
                    EvaluateCached(surface, uWindow.Low, v, pointCache));
                var dv = Subtract(
                    EvaluateCached(surface, u, vWindow.High, pointCache),
                    EvaluateCached(surface, u, vWindow.Low, pointCache));
                var normal = Normalize(Cross(du, dv));

                if (IsZero(normal))
                {
                    normal = NormalFromInwardFan(surface, uValues, vValues, uIndex, vIndex, pointCache);
                }

                var index = GridIndex(uIndex, vIndex, vValues.Length);
                normals[index] = IsZero(normal) ? triangleFallback[index] : normal;
            }
        }

        return normals;
    }

    private static ThreeDmRenderNormal NormalFromInwardFan(
        ThreeDmNurbsSurfaceGeometryData surface,
        double[] uValues,
        double[] vValues,
        int uIndex,
        int vIndex,
        Dictionary<(double U, double V), Point3d> pointCache)
    {
        var uWindow = ParameterWindow(uValues, uIndex);
        var vWindow = ParameterWindow(vValues, vIndex);
        var u = uValues[uIndex];
        var v = vValues[vIndex];
        var center = EvaluateCached(surface, u, v, pointCache);

        var sampleV = vIndex == 0
            ? v + ((vWindow.High - v) * 0.5)
            : vIndex == vValues.Length - 1
                ? v - ((v - vWindow.Low) * 0.5)
                : v;
        var sampleU = uIndex == 0
            ? u + ((uWindow.High - u) * 0.5)
            : uIndex == uValues.Length - 1
                ? u - ((u - uWindow.Low) * 0.5)
                : u;

        var first = EvaluateCached(surface, uWindow.Low, sampleV, pointCache);
        var second = EvaluateCached(surface, uWindow.High, sampleV, pointCache);
        var normal = Normalize(Cross(Subtract(first, center), Subtract(second, center)));
        if (!IsZero(normal))
        {
            return normal;
        }

        first = EvaluateCached(surface, sampleU, vWindow.Low, pointCache);
        second = EvaluateCached(surface, sampleU, vWindow.High, pointCache);
        return Normalize(Cross(Subtract(first, center), Subtract(second, center)));
    }

    private static ThreeDmRenderNormal[] BuildAreaWeightedNormals(ThreeDmRenderVertex[] vertices, int[] indices)
    {
        var accumulatedX = new double[vertices.Length];
        var accumulatedY = new double[vertices.Length];
        var accumulatedZ = new double[vertices.Length];

        for (var index = 0; index + 2 < indices.Length; index += 3)
        {
            var aIndex = indices[index];
            var bIndex = indices[index + 1];
            var cIndex = indices[index + 2];
            var a = vertices[aIndex];
            var b = vertices[bIndex];
            var c = vertices[cIndex];
            var abX = b.X - a.X;
            var abY = b.Y - a.Y;
            var abZ = b.Z - a.Z;
            var acX = c.X - a.X;
            var acY = c.Y - a.Y;
            var acZ = c.Z - a.Z;
            var normalX = (abY * acZ) - (abZ * acY);
            var normalY = (abZ * acX) - (abX * acZ);
            var normalZ = (abX * acY) - (abY * acX);

            if (!double.IsFinite(normalX) || !double.IsFinite(normalY) || !double.IsFinite(normalZ))
            {
                continue;
            }

            accumulatedX[aIndex] += normalX;
            accumulatedY[aIndex] += normalY;
            accumulatedZ[aIndex] += normalZ;
            accumulatedX[bIndex] += normalX;
            accumulatedY[bIndex] += normalY;
            accumulatedZ[bIndex] += normalZ;
            accumulatedX[cIndex] += normalX;
            accumulatedY[cIndex] += normalY;
            accumulatedZ[cIndex] += normalZ;
        }

        var normals = new ThreeDmRenderNormal[vertices.Length];
        for (var index = 0; index < normals.Length; index++)
        {
            normals[index] = Normalize(new Vector3d(accumulatedX[index], accumulatedY[index], accumulatedZ[index]));
        }

        return normals;
    }

    private static (double Low, double High) ParameterWindow(double[] values, int index)
    {
        var low = index > 0 ? values[index - 1] : values[index];
        var high = index < values.Length - 1 ? values[index + 1] : values[index];
        if (high > low)
        {
            return (low, high);
        }

        return (values[0], values[^1]);
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

    private static bool IsZero(ThreeDmRenderNormal normal) =>
        Math.Abs(normal.X) <= 1e-15 && Math.Abs(normal.Y) <= 1e-15 && Math.Abs(normal.Z) <= 1e-15;
}
