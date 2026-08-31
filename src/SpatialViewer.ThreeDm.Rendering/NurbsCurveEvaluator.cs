using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Rendering;

internal static class NurbsCurveEvaluator
{
    public static Point3d Evaluate(ThreeDmNurbsCurveData curve, double parameter)
    {
        ArgumentNullException.ThrowIfNull(curve);

        var controlPointCount = curve.ControlPoints.Count;
        var degree = curve.Degree;
        if (degree < 1 || controlPointCount <= degree)
        {
            throw new InvalidDataException("NURBS curve degree/control-point count is invalid.");
        }

        var knots = BuildFullKnotVector(curve);
        var n = controlPointCount - 1;
        var domainStart = knots[degree];
        var domainEnd = knots[n + 1];
        if (!(domainEnd > domainStart))
        {
            throw new InvalidDataException("NURBS curve has an invalid parameter domain.");
        }

        var u = Math.Clamp(parameter, domainStart, domainEnd);
        var span = FindSpan(n, degree, u, knots);
        var basis = BasisFunctions(span, u, degree, knots);

        double x = 0;
        double y = 0;
        double z = 0;
        double weightSum = 0;

        for (var j = 0; j <= degree; j++)
        {
            var index = span - degree + j;
            var controlPoint = curve.ControlPoints[index];
            var weight = curve.IsRational ? controlPoint.Weight : 1.0;
            var coefficient = basis[j] * weight;
            x += coefficient * controlPoint.Position.X;
            y += coefficient * controlPoint.Position.Y;
            z += coefficient * controlPoint.Position.Z;
            weightSum += coefficient;
        }

        if (Math.Abs(weightSum) <= 1e-15 || !double.IsFinite(weightSum))
        {
            throw new InvalidDataException("NURBS curve evaluation produced an invalid homogeneous weight.");
        }

        return new Point3d(x / weightSum, y / weightSum, z / weightSum);
    }

    public static double[] BuildFullKnotVector(ThreeDmNurbsCurveData curve)
    {
        var expectedCompactCount = curve.ControlPoints.Count + curve.Degree - 1;
        if (curve.Knots.Count != expectedCompactCount)
        {
            throw new InvalidDataException(
                $"NURBS compact knot count {curve.Knots.Count} does not match expected {expectedCompactCount}.");
        }

        var full = new double[curve.Knots.Count + 2];
        full[0] = curve.StartSuperfluousKnot;
        for (var i = 0; i < curve.Knots.Count; i++)
        {
            full[i + 1] = curve.Knots[i];
        }

        full[^1] = curve.EndSuperfluousKnot;
        return full;
    }

    private static int FindSpan(int n, int degree, double u, double[] knots)
    {
        if (u >= knots[n + 1])
        {
            return n;
        }

        if (u <= knots[degree])
        {
            return degree;
        }

        var low = degree;
        var high = n + 1;
        var mid = (low + high) / 2;
        while (u < knots[mid] || u >= knots[mid + 1])
        {
            if (u < knots[mid])
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

    private static double[] BasisFunctions(int span, double u, int degree, double[] knots)
    {
        var basis = new double[degree + 1];
        var left = new double[degree + 1];
        var right = new double[degree + 1];
        basis[0] = 1.0;

        for (var j = 1; j <= degree; j++)
        {
            left[j] = u - knots[span + 1 - j];
            right[j] = knots[span + j] - u;
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
}
