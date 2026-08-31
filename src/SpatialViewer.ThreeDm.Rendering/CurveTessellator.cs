using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Rendering;

public static class ThreeDmCurveTessellator
{
    public static ThreeDmRenderCurve Tessellate(
        Guid sourceObjectId,
        ThreeDmCurveGeometryData curve,
        ThreeDmTessellationSettings settings,
        double modelAbsoluteTolerance = 0,
        int? sourceSubobjectIndex = null)
    {
        ArgumentNullException.ThrowIfNull(curve);
        ArgumentNullException.ThrowIfNull(settings);

        var tolerance = settings.ResolveChordTolerance(curve.Bounds, modelAbsoluteTolerance);
        var points = curve.Form switch
        {
            ThreeDmCurveForm.Line => TessellateLine(curve),
            ThreeDmCurveForm.Polyline => curve.PolylinePoints.ToArray(),
            ThreeDmCurveForm.Arc => TessellateArc(curve.Arc, false, tolerance, settings),
            ThreeDmCurveForm.Circle => TessellateArc(curve.Arc, true, tolerance, settings),
            ThreeDmCurveForm.Ellipse => TessellateEllipse(curve.Ellipse, tolerance, settings),
            ThreeDmCurveForm.Nurbs or ThreeDmCurveForm.Other => TessellateNurbs(curve.Nurbs, tolerance, settings),
            _ => TessellateNurbs(curve.Nurbs, tolerance, settings),
        };

        var renderPoints = points.Select(ToRenderVertex).ToArray();
        var isClosed = curve.Form == ThreeDmCurveForm.Circle ||
            curve.Form == ThreeDmCurveForm.Ellipse ||
            curve.Nurbs.IsClosed;

        if (isClosed && renderPoints.Length > 1 && !SamePoint(renderPoints[0], renderPoints[^1]))
        {
            renderPoints = [.. renderPoints, renderPoints[0]];
        }

        return new ThreeDmRenderCurve(
            sourceObjectId,
            ToRenderKind(curve.Form),
            renderPoints,
            isClosed,
            tolerance,
            sourceSubobjectIndex);
    }

    private static Point3d[] TessellateLine(ThreeDmCurveGeometryData curve)
    {
        if (curve.PolylinePoints.Count >= 2)
        {
            return [curve.PolylinePoints[0], curve.PolylinePoints[^1]];
        }

        if (curve.Nurbs.ControlPoints.Count >= 2)
        {
            return [curve.Nurbs.ControlPoints[0].Position, curve.Nurbs.ControlPoints[^1].Position];
        }

        return curve.Nurbs.ControlPoints.Select(item => item.Position).ToArray();
    }

    private static Point3d[] TessellateArc(
        ThreeDmArcGeometryData? arc,
        bool fullCircle,
        double tolerance,
        ThreeDmTessellationSettings settings)
    {
        if (arc is null || !(arc.Radius > 0) || !double.IsFinite(arc.Radius))
        {
            return Array.Empty<Point3d>();
        }

        var start = fullCircle ? 0 : arc.StartAngleRadians;
        var end = fullCircle ? Math.PI * 2 : arc.EndAngleRadians;
        var sweep = end - start;
        if (fullCircle && Math.Abs(sweep) < 1e-12)
        {
            sweep = Math.PI * 2;
        }

        var segmentCount = ResolveAnalyticSegmentCount(
            Math.Abs(sweep),
            arc.Radius,
            fullCircle,
            tolerance,
            settings);

        var points = new Point3d[segmentCount + 1];
        for (var i = 0; i <= segmentCount; i++)
        {
            var angle = start + (sweep * i / segmentCount);
            points[i] = PointOnPlane(
                arc.Plane,
                arc.Radius * Math.Cos(angle),
                arc.Radius * Math.Sin(angle));
        }

        return points;
    }

    private static Point3d[] TessellateEllipse(
        ThreeDmEllipseGeometryData? ellipse,
        double tolerance,
        ThreeDmTessellationSettings settings)
    {
        if (ellipse is null || !(ellipse.Radius1 > 0) || !(ellipse.Radius2 > 0))
        {
            return Array.Empty<Point3d>();
        }

        var maximumRadius = Math.Max(ellipse.Radius1, ellipse.Radius2);
        var segmentCount = ResolveAnalyticSegmentCount(
            Math.PI * 2,
            maximumRadius,
            true,
            tolerance,
            settings);

        var points = new Point3d[segmentCount + 1];
        for (var i = 0; i <= segmentCount; i++)
        {
            var angle = Math.PI * 2 * i / segmentCount;
            points[i] = PointOnPlane(
                ellipse.Plane,
                ellipse.Radius1 * Math.Cos(angle),
                ellipse.Radius2 * Math.Sin(angle));
        }

        return points;
    }

    private static Point3d[] TessellateNurbs(
        ThreeDmNurbsCurveData nurbs,
        double tolerance,
        ThreeDmTessellationSettings settings)
    {
        var fullKnots = NurbsCurveEvaluator.BuildFullKnotVector(nurbs);
        var degree = nurbs.Degree;
        var n = nurbs.ControlPoints.Count - 1;
        var domainStart = fullKnots[degree];
        var domainEnd = fullKnots[n + 1];
        if (!(domainEnd > domainStart))
        {
            return Array.Empty<Point3d>();
        }

        var points = new List<Point3d>();
        var first = NurbsCurveEvaluator.Evaluate(nurbs, domainStart);
        points.Add(first);

        for (var span = degree; span <= n; span++)
        {
            var start = fullKnots[span];
            var end = fullKnots[span + 1];
            if (!(end > start))
            {
                continue;
            }

            var startPoint = points[^1];
            var endPoint = NurbsCurveEvaluator.Evaluate(nurbs, end);
            AppendAdaptiveSpan(
                nurbs,
                start,
                end,
                startPoint,
                endPoint,
                tolerance,
                settings.MaxCurveSegments,
                0,
                points);

            if (points.Count >= settings.MaxCurveSegments + 1)
            {
                break;
            }
        }

        if (nurbs.IsClosed && points.Count > 1 && Distance(points[0], points[^1]) > tolerance)
        {
            points.Add(points[0]);
        }

        return points.ToArray();
    }

    private static void AppendAdaptiveSpan(
        ThreeDmNurbsCurveData nurbs,
        double startParameter,
        double endParameter,
        Point3d startPoint,
        Point3d endPoint,
        double tolerance,
        int maxSegments,
        int depth,
        List<Point3d> output)
    {
        if (output.Count >= maxSegments + 1)
        {
            return;
        }

        var range = endParameter - startParameter;
        var quarterParameter = startParameter + (range * 0.25);
        var middleParameter = startParameter + (range * 0.5);
        var threeQuarterParameter = startParameter + (range * 0.75);

        var quarterPoint = NurbsCurveEvaluator.Evaluate(nurbs, quarterParameter);
        var middlePoint = NurbsCurveEvaluator.Evaluate(nurbs, middleParameter);
        var threeQuarterPoint = NurbsCurveEvaluator.Evaluate(nurbs, threeQuarterParameter);

        var deviation = Math.Max(
            DistanceToSegment(quarterPoint, startPoint, endPoint),
            Math.Max(
                DistanceToSegment(middlePoint, startPoint, endPoint),
                DistanceToSegment(threeQuarterPoint, startPoint, endPoint)));

        if (deviation <= tolerance || depth >= 20 || Math.Abs(range) <= 1e-14)
        {
            output.Add(endPoint);
            return;
        }

        AppendAdaptiveSpan(
            nurbs,
            startParameter,
            middleParameter,
            startPoint,
            middlePoint,
            tolerance,
            maxSegments,
            depth + 1,
            output);

        AppendAdaptiveSpan(
            nurbs,
            middleParameter,
            endParameter,
            middlePoint,
            endPoint,
            tolerance,
            maxSegments,
            depth + 1,
            output);
    }

    private static int ResolveAnalyticSegmentCount(
        double sweep,
        double radius,
        bool closed,
        double tolerance,
        ThreeDmTessellationSettings settings)
    {
        var ratio = Math.Clamp(1.0 - (tolerance / radius), -1.0, 1.0);
        var angleStep = 2.0 * Math.Acos(ratio);
        if (!(angleStep > 1e-6) || !double.IsFinite(angleStep))
        {
            angleStep = Math.PI / 8.0;
        }

        var segments = Math.Max(1, (int)Math.Ceiling(sweep / angleStep));
        var minimum = closed
            ? settings.MinimumClosedCurveSegments
            : Math.Max(2, settings.MinimumClosedCurveSegments / 4);

        return Math.Clamp(Math.Max(segments, minimum), 1, settings.MaxCurveSegments);
    }

    private static Point3d PointOnPlane(Plane3d plane, double x, double y) =>
        new(
            plane.Origin.X + (plane.XAxis.X * x) + (plane.YAxis.X * y),
            plane.Origin.Y + (plane.XAxis.Y * x) + (plane.YAxis.Y * y),
            plane.Origin.Z + (plane.XAxis.Z * x) + (plane.YAxis.Z * y));

    private static double DistanceToSegment(Point3d point, Point3d start, Point3d end)
    {
        var vx = end.X - start.X;
        var vy = end.Y - start.Y;
        var vz = end.Z - start.Z;
        var lengthSquared = (vx * vx) + (vy * vy) + (vz * vz);
        if (lengthSquared <= 1e-30)
        {
            return Distance(point, start);
        }

        var wx = point.X - start.X;
        var wy = point.Y - start.Y;
        var wz = point.Z - start.Z;
        var t = Math.Clamp(((wx * vx) + (wy * vy) + (wz * vz)) / lengthSquared, 0, 1);
        var projection = new Point3d(start.X + (vx * t), start.Y + (vy * t), start.Z + (vz * t));
        return Distance(point, projection);
    }

    private static double Distance(Point3d a, Point3d b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        var dz = a.Z - b.Z;
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    private static bool SamePoint(ThreeDmRenderVertex a, ThreeDmRenderVertex b) =>
        a.X == b.X && a.Y == b.Y && a.Z == b.Z;

    private static ThreeDmRenderVertex ToRenderVertex(Point3d point) => new(point.X, point.Y, point.Z);

    private static ThreeDmRenderCurveKind ToRenderKind(ThreeDmCurveForm form) => form switch
    {
        ThreeDmCurveForm.Line => ThreeDmRenderCurveKind.Line,
        ThreeDmCurveForm.Polyline => ThreeDmRenderCurveKind.Polyline,
        ThreeDmCurveForm.Arc => ThreeDmRenderCurveKind.Arc,
        ThreeDmCurveForm.Circle => ThreeDmRenderCurveKind.Circle,
        ThreeDmCurveForm.Ellipse => ThreeDmRenderCurveKind.Ellipse,
        ThreeDmCurveForm.Nurbs => ThreeDmRenderCurveKind.Nurbs,
        _ => ThreeDmRenderCurveKind.Other,
    };
}
