using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Rendering;

public enum ThreeDmTessellationQuality
{
    Draft,
    Normal,
    High,
}

public sealed record ThreeDmTessellationSettings(
    ThreeDmTessellationQuality Quality = ThreeDmTessellationQuality.Normal,
    double WorldUnitsPerPixel = 0,
    double? AbsoluteChordTolerance = null,
    bool IncludeBrepEdges = true,
    int MaxCurveSegments = 4096,
    int MaxSurfaceSegmentsPerDirection = 256)
{
    public double TargetPixelError => Quality switch
    {
        ThreeDmTessellationQuality.Draft => 1.5,
        ThreeDmTessellationQuality.Normal => 0.75,
        ThreeDmTessellationQuality.High => 0.35,
        _ => 0.75,
    };

    public int MinimumClosedCurveSegments => Quality switch
    {
        ThreeDmTessellationQuality.Draft => 12,
        ThreeDmTessellationQuality.Normal => 24,
        ThreeDmTessellationQuality.High => 48,
        _ => 24,
    };

    public int MaximumSurfaceRefinementDepth => Quality switch
    {
        ThreeDmTessellationQuality.Draft => 6,
        ThreeDmTessellationQuality.Normal => 8,
        ThreeDmTessellationQuality.High => 10,
        _ => 8,
    };

    public double ResolveChordTolerance(BoundingBox3d bounds, double modelAbsoluteTolerance = 0)
    {
        if (AbsoluteChordTolerance is > 0)
        {
            return Math.Max(AbsoluteChordTolerance.Value, PositiveModelFloor(modelAbsoluteTolerance));
        }

        if (WorldUnitsPerPixel > 0 && double.IsFinite(WorldUnitsPerPixel))
        {
            return Math.Max(WorldUnitsPerPixel * TargetPixelError, PositiveModelFloor(modelAbsoluteTolerance));
        }

        var diagonal = DiagonalLength(bounds);
        var relative = Quality switch
        {
            ThreeDmTessellationQuality.Draft => 0.0025,
            ThreeDmTessellationQuality.Normal => 0.00075,
            ThreeDmTessellationQuality.High => 0.0002,
            _ => 0.00075,
        };

        var fallback = diagonal > 0 && double.IsFinite(diagonal)
            ? diagonal * relative
            : 1e-6;

        return Math.Max(fallback, PositiveModelFloor(modelAbsoluteTolerance));
    }

    public double ResolveCacheChordTolerance(BoundingBox3d bounds, double modelAbsoluteTolerance = 0)
    {
        var resolved = ResolveChordTolerance(bounds, modelAbsoluteTolerance);
        if (AbsoluteChordTolerance is > 0 || !(resolved > 0) || !double.IsFinite(resolved))
        {
            return resolved;
        }

        var exponent = Math.Floor(Math.Log2(resolved));
        var bucket = Math.Pow(2, exponent);
        if (!(bucket > 0) || !double.IsFinite(bucket))
        {
            return resolved;
        }

        return Math.Max(bucket, PositiveModelFloor(modelAbsoluteTolerance));
    }

    private static double PositiveModelFloor(double modelAbsoluteTolerance) =>
        modelAbsoluteTolerance > 0 && double.IsFinite(modelAbsoluteTolerance)
            ? modelAbsoluteTolerance * 0.25
            : 1e-9;

    private static double DiagonalLength(BoundingBox3d bounds)
    {
        if (!bounds.IsValid)
        {
            return 0;
        }

        var dx = bounds.Max.X - bounds.Min.X;
        var dy = bounds.Max.Y - bounds.Min.Y;
        var dz = bounds.Max.Z - bounds.Min.Z;
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }
}
