namespace SpatialViewer.ThreeDm.Core;

public readonly record struct Point3d(double X, double Y, double Z);

public readonly record struct Vector3d(double X, double Y, double Z);

public readonly record struct BoundingBox3d(Point3d Min, Point3d Max)
{
    public static BoundingBox3d Invalid { get; } = new(
        new Point3d(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity),
        new Point3d(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity));

    public bool IsValid =>
        double.IsFinite(Min.X) &&
        double.IsFinite(Min.Y) &&
        double.IsFinite(Min.Z) &&
        double.IsFinite(Max.X) &&
        double.IsFinite(Max.Y) &&
        double.IsFinite(Max.Z) &&
        Min.X <= Max.X &&
        Min.Y <= Max.Y &&
        Min.Z <= Max.Z;

    public static BoundingBox3d FromPoints(Point3d first, Point3d second) =>
        new(
            new Point3d(
                Math.Min(first.X, second.X),
                Math.Min(first.Y, second.Y),
                Math.Min(first.Z, second.Z)),
            new Point3d(
                Math.Max(first.X, second.X),
                Math.Max(first.Y, second.Y),
                Math.Max(first.Z, second.Z)));

    public BoundingBox3d Union(BoundingBox3d other)
    {
        if (!IsValid)
        {
            return other;
        }

        if (!other.IsValid)
        {
            return this;
        }

        return new BoundingBox3d(
            new Point3d(
                Math.Min(Min.X, other.Min.X),
                Math.Min(Min.Y, other.Min.Y),
                Math.Min(Min.Z, other.Min.Z)),
            new Point3d(
                Math.Max(Max.X, other.Max.X),
                Math.Max(Max.Y, other.Max.Y),
                Math.Max(Max.Z, other.Max.Z)));
    }
}
