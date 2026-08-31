using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Rendering;

internal static class NurbsSurfaceEvaluator
{
    public static Point3d Evaluate(ThreeDmNurbsSurfaceGeometryData surface, double u, double v)
    {
        ArgumentNullException.ThrowIfNull(surface);
        Validate(surface);

        var knotsU = BuildFullKnotVector(
            surface.KnotsU,
            surface.ControlPointCountU,
            surface.DegreeU,
            surface.StartSuperfluousKnotU,
            surface.EndSuperfluousKnotU);
        var knotsV = BuildFullKnotVector(
            surface.KnotsV,
            surface.ControlPointCountV,
            surface.DegreeV,
            surface.StartSuperfluousKnotV,
            surface.EndSuperfluousKnotV);

        var clampedU = ClampToDomain(u, knotsU, surface.DegreeU, surface.ControlPointCountU);
        var clampedV = ClampToDomain(v, knotsV, surface.DegreeV, surface.ControlPointCountV);
        var spanU = FindSpan(surface.ControlPointCountU - 1, surface.DegreeU, clampedU, knotsU);
        var spanV = FindSpan(surface.ControlPointCountV - 1, surface.DegreeV, clampedV, knotsV);
        var basisU = BasisFunctions(spanU, clampedU, surface.DegreeU, knotsU);
        var basisV = BasisFunctions(spanV, clampedV, surface.DegreeV, knotsV);

        double x = 0;
        double y = 0;
        double z = 0;
        double weightSum = 0;

        for (var localU = 0; localU <= surface.DegreeU; localU++)
        {
            var controlU = spanU - surface.DegreeU + localU;
            for (var localV = 0; localV <= surface.DegreeV; localV++)
            {
                var controlV = spanV - surface.DegreeV + localV;
                var controlPoint = surface.ControlPoints[(controlU * surface.ControlPointCountV) + controlV];
                var weight = surface.IsRational ? controlPoint.Weight : 1.0;
                var coefficient = basisU[localU] * basisV[localV] * weight;
                x += coefficient * controlPoint.Position.X;
                y += coefficient * controlPoint.Position.Y;
                z += coefficient * controlPoint.Position.Z;
                weightSum += coefficient;
            }
        }

        if (Math.Abs(weightSum) <= 1e-15 || !double.IsFinite(weightSum))
        {
            throw new InvalidDataException("NURBS surface evaluation produced an invalid homogeneous weight.");
        }

        return new Point3d(x / weightSum, y / weightSum, z / weightSum);
    }

    public static (double Start, double End) GetDomainU(ThreeDmNurbsSurfaceGeometryData surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        Validate(surface);
        var knots = BuildFullKnotVector(
            surface.KnotsU,
            surface.ControlPointCountU,
            surface.DegreeU,
            surface.StartSuperfluousKnotU,
            surface.EndSuperfluousKnotU);
        return (knots[surface.DegreeU], knots[surface.ControlPointCountU]);
    }

    public static (double Start, double End) GetDomainV(ThreeDmNurbsSurfaceGeometryData surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        Validate(surface);
        var knots = BuildFullKnotVector(
            surface.KnotsV,
            surface.ControlPointCountV,
            surface.DegreeV,
            surface.StartSuperfluousKnotV,
            surface.EndSuperfluousKnotV);
        return (knots[surface.DegreeV], knots[surface.ControlPointCountV]);
    }

    public static double[] GetSeedParametersU(ThreeDmNurbsSurfaceGeometryData surface)
    {
        var domain = GetDomainU(surface);
        var knots = BuildFullKnotVector(
            surface.KnotsU,
            surface.ControlPointCountU,
            surface.DegreeU,
            surface.StartSuperfluousKnotU,
            surface.EndSuperfluousKnotU);
        return SeedParameters(knots, domain.Start, domain.End);
    }

    public static double[] GetSeedParametersV(ThreeDmNurbsSurfaceGeometryData surface)
    {
        var domain = GetDomainV(surface);
        var knots = BuildFullKnotVector(
            surface.KnotsV,
            surface.ControlPointCountV,
            surface.DegreeV,
            surface.StartSuperfluousKnotV,
            surface.EndSuperfluousKnotV);
        return SeedParameters(knots, domain.Start, domain.End);
    }

    private static void Validate(ThreeDmNurbsSurfaceGeometryData surface)
    {
        if (surface.DegreeU < 1 || surface.DegreeV < 1)
        {
            throw new InvalidDataException("NURBS surface degrees must both be positive.");
        }

        if (surface.ControlPointCountU <= surface.DegreeU || surface.ControlPointCountV <= surface.DegreeV)
        {
            throw new InvalidDataException("NURBS surface degree/control-point counts are invalid.");
        }

        if (surface.ControlPoints.Count != surface.ControlPointCountU * surface.ControlPointCountV)
        {
            throw new InvalidDataException("NURBS surface control-point grid size is inconsistent with its dimensions.");
        }
    }

    private static double[] BuildFullKnotVector(
        IReadOnlyList<double> compactKnots,
        int controlPointCount,
        int degree,
        double? startSuperfluousKnot,
        double? endSuperfluousKnot)
    {
        var expectedCompactCount = controlPointCount + degree - 1;
        if (compactKnots.Count != expectedCompactCount)
        {
            throw new InvalidDataException(
                $"NURBS compact knot count {compactKnots.Count} does not match expected {expectedCompactCount}.");
        }

        if (compactKnots.Count == 0)
        {
            throw new InvalidDataException("NURBS surface knot vector is empty.");
        }

        var full = new double[compactKnots.Count + 2];
        full[0] = startSuperfluousKnot ?? compactKnots[0];
        for (var index = 0; index < compactKnots.Count; index++)
        {
            full[index + 1] = compactKnots[index];
        }

        full[^1] = endSuperfluousKnot ?? compactKnots[^1];
        return full;
    }

    private static double ClampToDomain(double parameter, double[] knots, int degree, int controlPointCount)
    {
        var start = knots[degree];
        var end = knots[controlPointCount];
        if (!(end > start))
        {
            throw new InvalidDataException("NURBS surface has an invalid parameter domain.");
        }

        return Math.Clamp(parameter, start, end);
    }

    private static int FindSpan(int n, int degree, double parameter, double[] knots)
    {
        if (parameter >= knots[n + 1])
        {
            return n;
        }

        if (parameter <= knots[degree])
        {
            return degree;
        }

        var low = degree;
        var high = n + 1;
        var mid = (low + high) / 2;
        while (parameter < knots[mid] || parameter >= knots[mid + 1])
        {
            if (parameter < knots[mid])
            {
                high = mid;
            }
            else
            {
                low = mid;
            }

            mid = (low + high) / 2;
        }

        return mid;
    }

    private static double[] BasisFunctions(int span, double parameter, int degree, double[] knots)
    {
        var basis = new double[degree + 1];
        var left = new double[degree + 1];
        var right = new double[degree + 1];
        basis[0] = 1.0;

        for (var j = 1; j <= degree; j++)
        {
            left[j] = parameter - knots[span + 1 - j];
            right[j] = knots[span + j] - parameter;
            var saved = 0.0;

            for (var r = 0; r < j; r++)
            {
                var denominator = right[r + 1] + left[j - r];
                var temp = Math.Abs(denominator) <= 1e-15 ? 0.0 : basis[r] / denominator;
                basis[r] = saved + (right[r + 1] * temp);
                saved = left[j - r] * temp;
            }

            basis[j] = saved;
        }

        return basis;
    }

    private static double[] SeedParameters(double[] knots, double start, double end)
    {
        var parameters = new SortedSet<double> { start, end };
        foreach (var knot in knots)
        {
            if (knot > start && knot < end && double.IsFinite(knot))
            {
                parameters.Add(knot);
            }
        }

        return parameters.ToArray();
    }
}
