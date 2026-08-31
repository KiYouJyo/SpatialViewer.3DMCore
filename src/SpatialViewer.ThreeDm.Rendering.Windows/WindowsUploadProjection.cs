using SpatialViewer.ThreeDm.Rendering;

namespace SpatialViewer.ThreeDm.Rendering.Windows;

public readonly record struct WindowsRenderOrigin(double X, double Y, double Z);

public readonly record struct WindowsRenderFloat3(float X, float Y, float Z);

public readonly record struct WindowsRenderFloat2(float X, float Y);

public sealed record WindowsRenderMeshUpload(
    Guid SourceObjectId,
    IReadOnlyList<WindowsRenderFloat3> Vertices,
    IReadOnlyList<int> Indices)
{
    public IReadOnlyList<WindowsRenderFloat3> Normals { get; init; } = Array.Empty<WindowsRenderFloat3>();

    public IReadOnlyList<WindowsRenderFloat2> TextureCoordinates { get; init; } = Array.Empty<WindowsRenderFloat2>();

    public Guid? MaterialId { get; init; }

    public uint? ColorArgb { get; init; }

    public int? SourceSubobjectIndex { get; init; }

    public IReadOnlyList<Guid> InstancePath { get; init; } = Array.Empty<Guid>();
}

public sealed record WindowsRenderCurveUpload(
    Guid SourceObjectId,
    ThreeDmRenderCurveKind Kind,
    IReadOnlyList<WindowsRenderFloat3> Points,
    bool IsClosed,
    int? SourceSubobjectIndex,
    IReadOnlyList<Guid> InstancePath);

public sealed record WindowsRenderPointSetUpload(
    Guid SourceObjectId,
    IReadOnlyList<WindowsRenderFloat3> Points,
    IReadOnlyList<Guid> InstancePath);

public sealed record WindowsRenderSceneUpload(
    WindowsRenderOrigin Origin,
    IReadOnlyList<WindowsRenderMeshUpload> Meshes,
    IReadOnlyList<WindowsRenderCurveUpload> Curves,
    IReadOnlyList<WindowsRenderPointSetUpload> PointSets);

public static class WindowsThreeDmUploadProjection
{
    public static WindowsRenderSceneUpload Project(
        ThreeDmRenderScene scene,
        WindowsRenderOrigin? origin = null)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var actualOrigin = origin ?? ResolveOrigin(scene);
        var meshes = scene.Meshes.Select(mesh => ProjectMesh(mesh, actualOrigin)).ToArray();
        var curves = scene.Curves.Select(curve => ProjectCurve(curve, actualOrigin)).ToArray();
        var pointSets = scene.PointSets.Select(pointSet => ProjectPointSet(pointSet, actualOrigin)).ToArray();

        return new WindowsRenderSceneUpload(actualOrigin, meshes, curves, pointSets);
    }

    private static WindowsRenderMeshUpload ProjectMesh(ThreeDmRenderMesh mesh, WindowsRenderOrigin origin) =>
        new(
            mesh.SourceObjectId,
            mesh.Vertices.Select(vertex => Rebase(vertex, origin)).ToArray(),
            mesh.Indices.ToArray())
        {
            Normals = mesh.Normals
                .Select(normal => new WindowsRenderFloat3((float)normal.X, (float)normal.Y, (float)normal.Z))
                .ToArray(),
            TextureCoordinates = mesh.TextureCoordinates
                .Select(uv => new WindowsRenderFloat2((float)uv.U, (float)uv.V))
                .ToArray(),
            MaterialId = mesh.MaterialId,
            ColorArgb = mesh.ColorArgb,
            SourceSubobjectIndex = mesh.SourceSubobjectIndex,
            InstancePath = mesh.InstancePath.ToArray(),
        };

    private static WindowsRenderCurveUpload ProjectCurve(ThreeDmRenderCurve curve, WindowsRenderOrigin origin) =>
        new(
            curve.SourceObjectId,
            curve.Kind,
            curve.Points.Select(point => Rebase(point, origin)).ToArray(),
            curve.IsClosed,
            curve.SourceSubobjectIndex,
            curve.InstancePath.ToArray());

    private static WindowsRenderPointSetUpload ProjectPointSet(ThreeDmRenderPointSet pointSet, WindowsRenderOrigin origin) =>
        new(
            pointSet.SourceObjectId,
            pointSet.Points.Select(point => Rebase(point, origin)).ToArray(),
            pointSet.InstancePath.ToArray());

    private static WindowsRenderFloat3 Rebase(ThreeDmRenderVertex vertex, WindowsRenderOrigin origin) =>
        new(
            CheckedFloat(vertex.X - origin.X),
            CheckedFloat(vertex.Y - origin.Y),
            CheckedFloat(vertex.Z - origin.Z));

    private static float CheckedFloat(double value)
    {
        if (!double.IsFinite(value) || value > float.MaxValue || value < -float.MaxValue)
        {
            throw new InvalidDataException("Render coordinate cannot be represented by the Windows float upload format.");
        }

        return (float)value;
    }

    private static WindowsRenderOrigin ResolveOrigin(ThreeDmRenderScene scene)
    {
        var hasPoint = false;
        double minX = 0;
        double minY = 0;
        double minZ = 0;
        double maxX = 0;
        double maxY = 0;
        double maxZ = 0;

        foreach (var point in EnumerateVertices(scene))
        {
            if (!hasPoint)
            {
                minX = maxX = point.X;
                minY = maxY = point.Y;
                minZ = maxZ = point.Z;
                hasPoint = true;
                continue;
            }

            minX = Math.Min(minX, point.X);
            minY = Math.Min(minY, point.Y);
            minZ = Math.Min(minZ, point.Z);
            maxX = Math.Max(maxX, point.X);
            maxY = Math.Max(maxY, point.Y);
            maxZ = Math.Max(maxZ, point.Z);
        }

        return hasPoint
            ? new WindowsRenderOrigin(
                Midpoint(minX, maxX),
                Midpoint(minY, maxY),
                Midpoint(minZ, maxZ))
            : default;
    }

    private static IEnumerable<ThreeDmRenderVertex> EnumerateVertices(ThreeDmRenderScene scene)
    {
        foreach (var mesh in scene.Meshes)
        {
            foreach (var vertex in mesh.Vertices)
            {
                yield return vertex;
            }
        }

        foreach (var curve in scene.Curves)
        {
            foreach (var point in curve.Points)
            {
                yield return point;
            }
        }

        foreach (var pointSet in scene.PointSets)
        {
            foreach (var point in pointSet.Points)
            {
                yield return point;
            }
        }
    }

    private static double Midpoint(double a, double b) => a + ((b - a) * 0.5);
}
