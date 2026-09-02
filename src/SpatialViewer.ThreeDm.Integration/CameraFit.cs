using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Integration;

public sealed record ThreeDmCameraFitOptions
{
    public double VerticalFieldOfViewRadians { get; init; } = Math.PI / 4;

    public double AspectRatio { get; init; } = 16.0 / 9.0;

    public double Padding { get; init; } = 1.1;

    public Vector3d ViewDirection { get; init; } = new(1, -1, -1);

    public Vector3d UpDirection { get; init; } = new(0, 0, 1);

    public void Validate()
    {
        if (!double.IsFinite(VerticalFieldOfViewRadians) ||
            VerticalFieldOfViewRadians <= 0 ||
            VerticalFieldOfViewRadians >= Math.PI)
        {
            throw new ArgumentOutOfRangeException(nameof(VerticalFieldOfViewRadians));
        }

        if (!double.IsFinite(AspectRatio) || AspectRatio <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(AspectRatio));
        }

        if (!double.IsFinite(Padding) || Padding < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Padding));
        }

        _ = Normalize(ViewDirection, nameof(ViewDirection));
        _ = Normalize(UpDirection, nameof(UpDirection));
    }

    private static Vector3d Normalize(Vector3d vector, string parameterName)
    {
        var length = Math.Sqrt((vector.X * vector.X) + (vector.Y * vector.Y) + (vector.Z * vector.Z));
        if (!double.IsFinite(length) || length <= 1e-15)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return new Vector3d(vector.X / length, vector.Y / length, vector.Z / length);
    }
}

public sealed record ThreeDmCameraFit(
    Point3d Target,
    Point3d CameraLocation,
    Vector3d CameraDirection,
    Vector3d CameraUp,
    double BoundingRadius,
    double CameraDistance,
    double NearPlaneDistance,
    double FarPlaneDistance);

public static class ThreeDmCameraFitCalculator
{
    public static ThreeDmCameraFit Calculate(
        BoundingBox3d bounds,
        ThreeDmCameraFitOptions? options = null)
    {
        if (!bounds.IsValid)
        {
            throw new ArgumentException("Camera fit requires valid model bounds.", nameof(bounds));
        }

        options ??= new ThreeDmCameraFitOptions();
        options.Validate();

        var target = new Point3d(
            Midpoint(bounds.Min.X, bounds.Max.X),
            Midpoint(bounds.Min.Y, bounds.Max.Y),
            Midpoint(bounds.Min.Z, bounds.Max.Z));
        var dx = bounds.Max.X - bounds.Min.X;
        var dy = bounds.Max.Y - bounds.Min.Y;
        var dz = bounds.Max.Z - bounds.Min.Z;
        var radius = 0.5 * Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
        radius = Math.Max(radius * options.Padding, 1e-6);

        var direction = Normalize(options.ViewDirection);
        var upReference = Normalize(options.UpDirection);
        var right = Cross(direction, upReference);
        if (Length(right) <= 1e-12)
        {
            upReference = Math.Abs(direction.Z) < 0.9
                ? new Vector3d(0, 0, 1)
                : new Vector3d(0, 1, 0);
            right = Cross(direction, upReference);
        }

        right = Normalize(right);
        var up = Normalize(Cross(right, direction));

        var verticalHalfAngle = options.VerticalFieldOfViewRadians * 0.5;
        var horizontalHalfAngle = Math.Atan(Math.Tan(verticalHalfAngle) * options.AspectRatio);
        var limitingHalfAngle = Math.Min(verticalHalfAngle, horizontalHalfAngle);
        var distance = radius / Math.Sin(limitingHalfAngle);
        var camera = new Point3d(
            target.X - (direction.X * distance),
            target.Y - (direction.Y * distance),
            target.Z - (direction.Z * distance));
        var near = Math.Max(radius * 1e-4, distance - (radius * 1.25));
        var far = Math.Max(near * 2, distance + (radius * 1.25));

        return new ThreeDmCameraFit(
            target,
            camera,
            direction,
            up,
            radius,
            distance,
            near,
            far);
    }

    private static double Midpoint(double a, double b) => a + ((b - a) * 0.5);

    private static double Length(Vector3d vector) =>
        Math.Sqrt((vector.X * vector.X) + (vector.Y * vector.Y) + (vector.Z * vector.Z));

    private static Vector3d Normalize(Vector3d vector)
    {
        var length = Length(vector);
        if (!double.IsFinite(length) || length <= 1e-15)
        {
            throw new InvalidDataException("Camera direction cannot be normalized.");
        }

        return new Vector3d(vector.X / length, vector.Y / length, vector.Z / length);
    }

    private static Vector3d Cross(Vector3d left, Vector3d right) =>
        new(
            (left.Y * right.Z) - (left.Z * right.Y),
            (left.Z * right.X) - (left.X * right.Z),
            (left.X * right.Y) - (left.Y * right.X));
}
