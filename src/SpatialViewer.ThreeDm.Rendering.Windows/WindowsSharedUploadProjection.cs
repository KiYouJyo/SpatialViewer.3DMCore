using SpatialViewer.ThreeDm.Core;
using SpatialViewer.ThreeDm.Rendering;

namespace SpatialViewer.ThreeDm.Rendering.Windows;

public readonly record struct WindowsRenderFloat4x4(
    float M00, float M01, float M02, float M03,
    float M10, float M11, float M12, float M13,
    float M20, float M21, float M22, float M23,
    float M30, float M31, float M32, float M33);

public sealed record WindowsSharedMeshGeometryUpload(
    int GeometryIndex,
    Guid SourceObjectId,
    int? SourceSubobjectIndex,
    WindowsRenderOrigin LocalOrigin,
    IReadOnlyList<WindowsRenderFloat3> Vertices,
    IReadOnlyList<int> Indices)
{
    public IReadOnlyList<int> WireIndices { get; init; } = Array.Empty<int>();

    public IReadOnlyList<WindowsRenderFloat3> Normals { get; init; } = Array.Empty<WindowsRenderFloat3>();

    public IReadOnlyList<WindowsRenderFloat2> TextureCoordinates { get; init; } = Array.Empty<WindowsRenderFloat2>();
}

public sealed record WindowsSharedMeshInstanceUpload(
    int GeometryIndex,
    Guid SourceObjectId,
    int? SourceSubobjectIndex,
    WindowsRenderFloat4x4 Transform,
    IReadOnlyList<Guid> InstancePath)
{
    public Guid? MaterialId { get; init; }

    public uint? ColorArgb { get; init; }

    public WindowsRenderAppearance Appearance { get; init; } = WindowsRenderAppearance.Default;
}

public sealed record WindowsSharedMeshSceneUpload(
    WindowsRenderOrigin Origin,
    IReadOnlyList<WindowsSharedMeshGeometryUpload> Geometries,
    IReadOnlyList<WindowsSharedMeshInstanceUpload> Instances);

public static class WindowsThreeDmSharedUploadProjection
{
    public static WindowsSharedMeshSceneUpload Project(
        ThreeDmSharedMeshScene scene,
        WindowsRenderOrigin? origin = null)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var actualOrigin = origin ?? ResolveOrigin(scene);
        var localOrigins = scene.Geometries.ToDictionary(
            geometry => geometry.GeometryIndex,
            geometry => ResolveLocalOrigin(geometry.Bounds));
        var geometries = scene.Geometries
            .Select(geometry => ProjectGeometry(geometry, localOrigins[geometry.GeometryIndex]))
            .ToArray();
        var instances = scene.Instances
            .Select(instance => ProjectInstance(
                instance,
                localOrigins.TryGetValue(instance.GeometryIndex, out var localOrigin)
                    ? localOrigin
                    : throw new InvalidDataException($"Shared mesh instance references missing geometry index {instance.GeometryIndex}."),
                actualOrigin))
            .ToArray();

        return new WindowsSharedMeshSceneUpload(actualOrigin, geometries, instances);
    }

    private static WindowsSharedMeshGeometryUpload ProjectGeometry(
        ThreeDmSharedMeshGeometry geometry,
        WindowsRenderOrigin localOrigin) =>
        new(
            geometry.GeometryIndex,
            geometry.SourceObjectId,
            geometry.SourceSubobjectIndex,
            localOrigin,
            geometry.Vertices.Select(vertex => Rebase(vertex, localOrigin)).ToArray(),
            geometry.Indices.ToArray())
        {
            WireIndices = CreateWireIndices(geometry.Indices),
            Normals = geometry.Normals
                .Select(normal => new WindowsRenderFloat3(
                    CheckedFloat(normal.X),
                    CheckedFloat(normal.Y),
                    CheckedFloat(normal.Z)))
                .ToArray(),
            TextureCoordinates = geometry.TextureCoordinates
                .Select(uv => new WindowsRenderFloat2(CheckedFloat(uv.U), CheckedFloat(uv.V)))
                .ToArray(),
        };

    private static WindowsSharedMeshInstanceUpload ProjectInstance(
        ThreeDmSharedMeshInstance instance,
        WindowsRenderOrigin localOrigin,
        WindowsRenderOrigin sceneOrigin)
    {
        var localRestore = Translation(localOrigin.X, localOrigin.Y, localOrigin.Z);
        var sceneRebase = Translation(-sceneOrigin.X, -sceneOrigin.Y, -sceneOrigin.Z);
        var uploadTransform = Multiply(sceneRebase, Multiply(instance.Transform, localRestore));

        return new WindowsSharedMeshInstanceUpload(
            instance.GeometryIndex,
            instance.SourceObjectId,
            instance.SourceSubobjectIndex,
            ToFloatMatrix(uploadTransform),
            instance.InstancePath.ToArray())
        {
            MaterialId = instance.MaterialId,
            ColorArgb = instance.ColorArgb,
            Appearance = ConvertAppearance(instance.Appearance),
        };
    }

    private static int[] CreateWireIndices(IReadOnlyList<int> triangleIndices)
    {
        var edges = new HashSet<(int A, int B)>();
        for (var i = 0; i + 2 < triangleIndices.Count; i += 3)
        {
            AddEdge(triangleIndices[i], triangleIndices[i + 1], edges);
            AddEdge(triangleIndices[i + 1], triangleIndices[i + 2], edges);
            AddEdge(triangleIndices[i + 2], triangleIndices[i], edges);
        }

        var result = new int[edges.Count * 2];
        var cursor = 0;
        foreach (var (a, b) in edges.OrderBy(edge => edge.A).ThenBy(edge => edge.B))
        {
            result[cursor++] = a;
            result[cursor++] = b;
        }

        return result;
    }

    private static void AddEdge(int left, int right, HashSet<(int A, int B)> edges)
    {
        var edge = left <= right ? (left, right) : (right, left);
        edges.Add(edge);
    }

    private static WindowsRenderOrigin ResolveOrigin(ThreeDmSharedMeshScene scene)
    {
        var geometries = scene.Geometries.ToDictionary(item => item.GeometryIndex);
        var hasPoint = false;
        double minX = 0;
        double minY = 0;
        double minZ = 0;
        double maxX = 0;
        double maxY = 0;
        double maxZ = 0;

        foreach (var instance in scene.Instances)
        {
            if (!geometries.TryGetValue(instance.GeometryIndex, out var geometry) || !geometry.Bounds.IsValid)
            {
                continue;
            }

            foreach (var corner in EnumerateCorners(geometry.Bounds))
            {
                var point = TransformPoint(corner, instance.Transform);
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
        }

        return hasPoint
            ? new WindowsRenderOrigin(
                Midpoint(minX, maxX),
                Midpoint(minY, maxY),
                Midpoint(minZ, maxZ))
            : default;
    }

    private static WindowsRenderOrigin ResolveLocalOrigin(BoundingBox3d bounds) =>
        bounds.IsValid
            ? new WindowsRenderOrigin(
                Midpoint(bounds.Min.X, bounds.Max.X),
                Midpoint(bounds.Min.Y, bounds.Max.Y),
                Midpoint(bounds.Min.Z, bounds.Max.Z))
            : default;

    private static IEnumerable<Point3d> EnumerateCorners(BoundingBox3d bounds)
    {
        yield return new Point3d(bounds.Min.X, bounds.Min.Y, bounds.Min.Z);
        yield return new Point3d(bounds.Min.X, bounds.Min.Y, bounds.Max.Z);
        yield return new Point3d(bounds.Min.X, bounds.Max.Y, bounds.Min.Z);
        yield return new Point3d(bounds.Min.X, bounds.Max.Y, bounds.Max.Z);
        yield return new Point3d(bounds.Max.X, bounds.Min.Y, bounds.Min.Z);
        yield return new Point3d(bounds.Max.X, bounds.Min.Y, bounds.Max.Z);
        yield return new Point3d(bounds.Max.X, bounds.Max.Y, bounds.Min.Z);
        yield return new Point3d(bounds.Max.X, bounds.Max.Y, bounds.Max.Z);
    }

    private static Point3d TransformPoint(Point3d point, Transform3d transform)
    {
        var x = (transform.M00 * point.X) + (transform.M01 * point.Y) + (transform.M02 * point.Z) + transform.M03;
        var y = (transform.M10 * point.X) + (transform.M11 * point.Y) + (transform.M12 * point.Z) + transform.M13;
        var z = (transform.M20 * point.X) + (transform.M21 * point.Y) + (transform.M22 * point.Z) + transform.M23;
        var w = (transform.M30 * point.X) + (transform.M31 * point.Y) + (transform.M32 * point.Z) + transform.M33;
        if (Math.Abs(w) > 1e-15 && Math.Abs(w - 1) > 1e-15)
        {
            x /= w;
            y /= w;
            z /= w;
        }

        return new Point3d(x, y, z);
    }

    private static WindowsRenderFloat3 Rebase(ThreeDmRenderVertex vertex, WindowsRenderOrigin origin) =>
        new(
            CheckedFloat(vertex.X - origin.X),
            CheckedFloat(vertex.Y - origin.Y),
            CheckedFloat(vertex.Z - origin.Z));

    private static WindowsRenderFloat4x4 ToFloatMatrix(Transform3d transform) =>
        new(
            CheckedFloat(transform.M00), CheckedFloat(transform.M01), CheckedFloat(transform.M02), CheckedFloat(transform.M03),
            CheckedFloat(transform.M10), CheckedFloat(transform.M11), CheckedFloat(transform.M12), CheckedFloat(transform.M13),
            CheckedFloat(transform.M20), CheckedFloat(transform.M21), CheckedFloat(transform.M22), CheckedFloat(transform.M23),
            CheckedFloat(transform.M30), CheckedFloat(transform.M31), CheckedFloat(transform.M32), CheckedFloat(transform.M33));

    private static Transform3d Translation(double x, double y, double z) =>
        new(
            1, 0, 0, x,
            0, 1, 0, y,
            0, 0, 1, z,
            0, 0, 0, 1);

    private static Transform3d Multiply(Transform3d left, Transform3d right) =>
        new(
            (left.M00 * right.M00) + (left.M01 * right.M10) + (left.M02 * right.M20) + (left.M03 * right.M30),
            (left.M00 * right.M01) + (left.M01 * right.M11) + (left.M02 * right.M21) + (left.M03 * right.M31),
            (left.M00 * right.M02) + (left.M01 * right.M12) + (left.M02 * right.M22) + (left.M03 * right.M32),
            (left.M00 * right.M03) + (left.M01 * right.M13) + (left.M02 * right.M23) + (left.M03 * right.M33),
            (left.M10 * right.M00) + (left.M11 * right.M10) + (left.M12 * right.M20) + (left.M13 * right.M30),
            (left.M10 * right.M01) + (left.M11 * right.M11) + (left.M12 * right.M21) + (left.M13 * right.M31),
            (left.M10 * right.M02) + (left.M11 * right.M12) + (left.M12 * right.M22) + (left.M13 * right.M32),
            (left.M10 * right.M03) + (left.M11 * right.M13) + (left.M12 * right.M23) + (left.M13 * right.M33),
            (left.M20 * right.M00) + (left.M21 * right.M10) + (left.M22 * right.M20) + (left.M23 * right.M30),
            (left.M20 * right.M01) + (left.M21 * right.M11) + (left.M22 * right.M21) + (left.M23 * right.M31),
            (left.M20 * right.M02) + (left.M21 * right.M12) + (left.M22 * right.M22) + (left.M23 * right.M32),
            (left.M20 * right.M03) + (left.M21 * right.M13) + (left.M22 * right.M23) + (left.M23 * right.M33),
            (left.M30 * right.M00) + (left.M31 * right.M10) + (left.M32 * right.M20) + (left.M33 * right.M30),
            (left.M30 * right.M01) + (left.M31 * right.M11) + (left.M32 * right.M21) + (left.M33 * right.M31),
            (left.M30 * right.M02) + (left.M31 * right.M12) + (left.M32 * right.M22) + (left.M33 * right.M32),
            (left.M30 * right.M03) + (left.M31 * right.M13) + (left.M32 * right.M23) + (left.M33 * right.M33));

    private static WindowsRenderAppearance ConvertAppearance(ThreeDmRenderAppearance appearance)
    {
        WindowsRenderPbrMaterial? physicallyBased = null;
        if (appearance.PhysicallyBased is { } pbr)
        {
            physicallyBased = new WindowsRenderPbrMaterial(
                CheckedUnitFloat(pbr.BaseColorR),
                CheckedUnitFloat(pbr.BaseColorG),
                CheckedUnitFloat(pbr.BaseColorB),
                CheckedUnitFloat(pbr.BaseColorA),
                CheckedUnitFloat(pbr.Metallic),
                CheckedUnitFloat(pbr.Roughness),
                CheckedUnitFloat(pbr.Alpha),
                CheckedUnitFloat(pbr.Opacity),
                CheckedUnitFloat(pbr.Clearcoat),
                CheckedUnitFloat(pbr.ClearcoatRoughness),
                pbr.Brdf);
        }

        var textures = appearance.Textures
            .Select(texture => new WindowsRenderMaterialTexture(
                texture.FileName,
                texture.TextureType,
                texture.IsEnabled,
                texture.MappingChannelId,
                texture.ProjectionMode,
                texture.WrapU,
                texture.WrapV,
                texture.WrapW,
                CheckedFloat(texture.RepeatU),
                CheckedFloat(texture.RepeatV),
                CheckedFloat(texture.OffsetU),
                CheckedFloat(texture.OffsetV),
                CheckedFloat(texture.RotationRadians)))
            .ToArray();

        return new WindowsRenderAppearance(
            appearance.ColorArgb,
            CheckedUnitFloat(appearance.Opacity),
            appearance.MaterialId,
            appearance.SpecularColorArgb,
            appearance.EmissionColorArgb,
            CheckedFloat(appearance.Shine),
            CheckedFloat(appearance.Reflectivity))
        {
            PhysicallyBased = physicallyBased,
            Textures = textures,
        };
    }

    private static float CheckedUnitFloat(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new InvalidDataException("Render appearance value must be finite.");
        }

        return (float)Math.Clamp(value, 0, 1);
    }

    private static float CheckedFloat(double value)
    {
        if (!double.IsFinite(value) || value > float.MaxValue || value < -float.MaxValue)
        {
            throw new InvalidDataException("Render value cannot be represented by the Windows float upload format.");
        }

        return (float)value;
    }

    private static double Midpoint(double a, double b) => a + ((b - a) * 0.5);
}
