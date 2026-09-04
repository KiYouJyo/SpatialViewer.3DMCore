using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Integration;

public enum ThreeDmCameraProjection
{
    Perspective,
    Orthographic,
}

public sealed record ThreeDmCameraFrustum(
    double Left,
    double Right,
    double Bottom,
    double Top,
    double Near,
    double Far);

public sealed record ThreeDmCameraState(
    Point3d Location,
    Point3d Target,
    Vector3d Up,
    ThreeDmCameraProjection Projection,
    double NearPlaneDistance,
    double FarPlaneDistance)
{
    public double VerticalFieldOfViewRadians { get; init; } = Math.PI / 4;
    public double OrthographicHeight { get; init; }
    public ThreeDmCameraFrustum? SourceFrustum { get; init; }
}

public sealed record ThreeDmViewPreset(
    string Key,
    string Name,
    bool IsNamedView,
    ThreeDmCameraState Camera);

public static class ThreeDmViewCatalog
{
    public static IReadOnlyList<ThreeDmViewPreset> CreateNamedViews(
        ThreeDmSceneDocument document,
        double aspectRatio = 16.0 / 9.0,
        double fallbackVerticalFieldOfViewRadians = Math.PI / 4)
    {
        ArgumentNullException.ThrowIfNull(document);
        ValidateAspectRatio(aspectRatio);
        ValidateFov(fallbackVerticalFieldOfViewRadians);
        var radius = BoundingRadius(document.Bounds);

        return document.NamedViews.Select((view, index) =>
        {
            var location = IsFinite(view.CameraLocation) ? view.CameraLocation : Center(document.Bounds);
            var direction = NormalizeOrFallback(view.CameraDirection, new Vector3d(1, -1, -1));
            var target = IsFinite(view.TargetPoint) ? view.TargetPoint : Add(location, direction, Math.Max(radius, 1));
            var distance = Distance(location, target);
            if (!double.IsFinite(distance) || distance <= 1e-9)
            {
                distance = Math.Max(radius * 2, 1);
                target = Add(location, direction, distance);
            }

            var sourceFrustum = view.Frustum is { IsValid: true } value
                ? new ThreeDmCameraFrustum(
                    value.Left,
                    value.Right,
                    value.Bottom,
                    value.Top,
                    value.Near,
                    value.Far)
                : null;
            var (near, far) = sourceFrustum is null
                ? ClipPlanes(distance, radius)
                : (sourceFrustum.Near, sourceFrustum.Far);
            var verticalFov = ResolveVerticalFov(
                view,
                aspectRatio,
                fallbackVerticalFieldOfViewRadians);
            var camera = new ThreeDmCameraState(
                location,
                target,
                NormalizeOrFallback(view.CameraUp, new Vector3d(0, 0, 1)),
                view.IsPerspectiveProjection ? ThreeDmCameraProjection.Perspective : ThreeDmCameraProjection.Orthographic,
                near,
                far)
            {
                VerticalFieldOfViewRadians = verticalFov,
                OrthographicHeight = sourceFrustum is null
                    ? Math.Max(radius * 2.2, 1e-6)
                    : Math.Max(sourceFrustum.Top - sourceFrustum.Bottom, 1e-6),
                SourceFrustum = sourceFrustum,
            };

            return new ThreeDmViewPreset($"named:{index}", view.Name, true, camera);
        }).ToArray();
    }

    public static IReadOnlyList<ThreeDmViewPreset> CreateStandardViews(
        BoundingBox3d bounds,
        double aspectRatio = 16.0 / 9.0,
        double verticalFieldOfViewRadians = Math.PI / 4)
    {
        if (!bounds.IsValid)
        {
            return Array.Empty<ThreeDmViewPreset>();
        }

        var perspective = CreateFit(bounds, new Vector3d(1, -1, -1), new Vector3d(0, 0, 1), false, aspectRatio, verticalFieldOfViewRadians);
        var top = CreateFit(bounds, new Vector3d(0, 0, -1), new Vector3d(0, 1, 0), true, aspectRatio, verticalFieldOfViewRadians);
        var front = CreateFit(bounds, new Vector3d(0, -1, 0), new Vector3d(0, 0, 1), true, aspectRatio, verticalFieldOfViewRadians);
        var right = CreateFit(bounds, new Vector3d(-1, 0, 0), new Vector3d(0, 0, 1), true, aspectRatio, verticalFieldOfViewRadians);

        return
        [
            new ThreeDmViewPreset("standard:perspective", "Perspective", false, perspective),
            new ThreeDmViewPreset("standard:top", "Top", false, top),
            new ThreeDmViewPreset("standard:front", "Front", false, front),
            new ThreeDmViewPreset("standard:right", "Right", false, right),
        ];
    }

    private static ThreeDmCameraState CreateFit(
        BoundingBox3d bounds,
        Vector3d direction,
        Vector3d up,
        bool orthographic,
        double aspectRatio,
        double fov)
    {
        var fit = ThreeDmCameraFitCalculator.Calculate(bounds, new ThreeDmCameraFitOptions
        {
            AspectRatio = aspectRatio,
            VerticalFieldOfViewRadians = fov,
            ViewDirection = direction,
            UpDirection = up,
        });

        return new ThreeDmCameraState(
            fit.CameraLocation,
            fit.Target,
            fit.CameraUp,
            orthographic ? ThreeDmCameraProjection.Orthographic : ThreeDmCameraProjection.Perspective,
            fit.NearPlaneDistance,
            fit.FarPlaneDistance)
        {
            VerticalFieldOfViewRadians = fov,
            OrthographicHeight = Math.Max(fit.BoundingRadius * 2, 1e-6),
        };
    }

    private static (double Near, double Far) ClipPlanes(double distance, double radius)
    {
        radius = Math.Max(radius, 1e-6);
        var near = Math.Max(radius * 1e-4, distance - (radius * 1.25));
        var far = Math.Max(near * 2, distance + (radius * 1.25));
        return (near, far);
    }

    private static double BoundingRadius(BoundingBox3d bounds)
    {
        if (!bounds.IsValid) return 1;
        var dx = bounds.Max.X - bounds.Min.X;
        var dy = bounds.Max.Y - bounds.Min.Y;
        var dz = bounds.Max.Z - bounds.Min.Z;
        return Math.Max(0.5 * Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz)), 1e-6);
    }

    private static Point3d Center(BoundingBox3d bounds) =>
        bounds.IsValid
            ? new Point3d(
                bounds.Min.X + ((bounds.Max.X - bounds.Min.X) * 0.5),
                bounds.Min.Y + ((bounds.Max.Y - bounds.Min.Y) * 0.5),
                bounds.Min.Z + ((bounds.Max.Z - bounds.Min.Z) * 0.5))
            : new Point3d(0, 0, 0);

    private static double Distance(Point3d a, Point3d b)
    {
        var x = b.X - a.X;
        var y = b.Y - a.Y;
        var z = b.Z - a.Z;
        return Math.Sqrt((x * x) + (y * y) + (z * z));
    }

    private static Point3d Add(Point3d point, Vector3d direction, double distance) =>
        new(
            point.X + (direction.X * distance),
            point.Y + (direction.Y * distance),
            point.Z + (direction.Z * distance));

    private static Vector3d NormalizeOrFallback(Vector3d vector, Vector3d fallback)
    {
        var length = Math.Sqrt((vector.X * vector.X) + (vector.Y * vector.Y) + (vector.Z * vector.Z));
        if (!double.IsFinite(length) || length <= 1e-15) return NormalizeOrFallback(fallback, new Vector3d(0, 0, 1));
        return new Vector3d(vector.X / length, vector.Y / length, vector.Z / length);
    }

    private static bool IsFinite(Point3d point) =>
        double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(point.Z);

    private static double ResolveVerticalFov(
        ThreeDmNamedViewInfo view,
        double aspectRatio,
        double fallback)
    {
        if (view.IsPerspectiveProjection && view.Frustum is { IsValid: true } frustum)
        {
            var height = frustum.Top - frustum.Bottom;
            var vertical = 2 * Math.Atan(height / (2 * frustum.Near));
            if (double.IsFinite(vertical) && vertical > 0 && vertical < Math.PI)
            {
                return vertical;
            }
        }

        var lensLength = view.Camera35mmLensLength;
        if (!double.IsFinite(lensLength) || lensLength <= 0)
        {
            return fallback;
        }

        const double filmWidthMillimeters = 36.0;
        var horizontal = 2 * Math.Atan(filmWidthMillimeters / (2 * lensLength));
        var fromLens = 2 * Math.Atan(Math.Tan(horizontal * 0.5) / aspectRatio);
        return double.IsFinite(fromLens) && fromLens > 0 && fromLens < Math.PI ? fromLens : fallback;
    }

    private static void ValidateAspectRatio(double value)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }
    }

    private static void ValidateFov(double value)
    {
        if (!double.IsFinite(value) || value <= 0 || value >= Math.PI)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}

public sealed partial class ThreeDmSession
{
    public IReadOnlyList<ThreeDmViewPreset> GetNamedViewPresets(
        double aspectRatio = 16.0 / 9.0,
        double fallbackVerticalFieldOfViewRadians = Math.PI / 4) =>
        ThreeDmViewCatalog.CreateNamedViews(
            RequireOpenDocument(),
            aspectRatio,
            fallbackVerticalFieldOfViewRadians);

    public IReadOnlyList<ThreeDmViewPreset> GetStandardViewPresets(
        double aspectRatio = 16.0 / 9.0,
        double verticalFieldOfViewRadians = Math.PI / 4) =>
        ThreeDmViewCatalog.CreateStandardViews(
            RequireOpenDocument().Bounds,
            aspectRatio,
            verticalFieldOfViewRadians);
}
