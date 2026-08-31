namespace SpatialViewer.ThreeDm.Core;

public readonly record struct Point3d(double X, double Y, double Z);

public readonly record struct Vector3d(double X, double Y, double Z);

public readonly record struct BoundingBox3d(Point3d Min, Point3d Max)
{
    public bool IsValid =>
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
}
