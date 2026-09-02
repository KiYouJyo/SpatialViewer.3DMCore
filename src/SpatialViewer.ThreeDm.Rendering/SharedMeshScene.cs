using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Rendering;

public sealed record ThreeDmSharedMeshGeometry(
    int GeometryIndex,
    Guid SourceObjectId,
    int? SourceSubobjectIndex,
    IReadOnlyList<ThreeDmRenderVertex> Vertices,
    IReadOnlyList<int> Indices,
    IReadOnlyList<ThreeDmRenderNormal> Normals,
    IReadOnlyList<ThreeDmRenderTextureCoordinate> TextureCoordinates,
    BoundingBox3d Bounds);

public sealed record ThreeDmSharedMeshInstance(
    int GeometryIndex,
    Guid SourceObjectId,
    int? SourceSubobjectIndex,
    Transform3d Transform,
    IReadOnlyList<Guid> InstancePath)
{
    public Guid? MaterialId { get; init; }

    public uint? ColorArgb { get; init; }

    public ThreeDmRenderAppearance Appearance { get; init; } = ThreeDmRenderAppearance.Default;
}

public sealed record ThreeDmSharedMeshScene(
    IReadOnlyList<ThreeDmSharedMeshGeometry> Geometries,
    IReadOnlyList<ThreeDmSharedMeshInstance> Instances)
{
    public IReadOnlyList<ThreeDmRenderDiagnostic> Diagnostics { get; init; } = Array.Empty<ThreeDmRenderDiagnostic>();
}

public sealed class ThreeDmSharedMeshSceneBuilder
{
    private readonly ThreeDmRenderSceneBuilder _geometryBuilder = new();

    public int CacheEntryCount => _geometryBuilder.CacheEntryCount;

    public void ClearCache() => _geometryBuilder.ClearCache();

    public ThreeDmSharedMeshScene Build(
        ThreeDmSceneDocument document,
        ThreeDmTessellationSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        settings ??= new ThreeDmTessellationSettings();

        var objectsById = document.Objects.ToDictionary(item => item.Id);
        var definitionsById = document.InstanceDefinitions.ToDictionary(item => item.Id);
        var definitionMemberIds = document.InstanceDefinitions
            .SelectMany(item => item.ObjectIds)
            .ToHashSet();
        var layersById = document.Layers.ToDictionary(item => item.Id);
        var materialsById = document.Materials.ToDictionary(item => item.Id);
        var localMeshes = new Dictionary<Guid, IReadOnlyList<ThreeDmRenderMesh>>();
        var geometryIndices = new Dictionary<GeometryKey, int>();
        var geometries = new List<ThreeDmSharedMeshGeometry>();
        var instances = new List<ThreeDmSharedMeshInstance>();
        var diagnostics = new List<ThreeDmRenderDiagnostic>();

        foreach (var sceneObject in document.Objects)
        {
            if (definitionMemberIds.Contains(sceneObject.Id) || !IsEffectivelyVisible(sceneObject, layersById))
            {
                continue;
            }

            AppendObject(
                sceneObject,
                IdentityTransform,
                Array.Empty<Guid>(),
                null,
                new HashSet<Guid>(),
                document,
                objectsById,
                definitionsById,
                layersById,
                materialsById,
                settings,
                localMeshes,
                geometryIndices,
                geometries,
                instances,
                diagnostics);
        }

        return new ThreeDmSharedMeshScene(geometries, instances)
        {
            Diagnostics = diagnostics,
        };
    }

    private void AppendObject(
        ThreeDmSceneObject sceneObject,
        Transform3d accumulatedTransform,
        IReadOnlyList<Guid> instancePath,
        ThreeDmRenderAppearance? inheritedAppearance,
        HashSet<Guid> definitionStack,
        ThreeDmSceneDocument document,
        IReadOnlyDictionary<Guid, ThreeDmSceneObject> objectsById,
        IReadOnlyDictionary<Guid, ThreeDmInstanceDefinitionInfo> definitionsById,
        Dictionary<Guid, ThreeDmLayerInfo> layersById,
        Dictionary<Guid, ThreeDmMaterialInfo> materialsById,
        ThreeDmTessellationSettings settings,
        Dictionary<Guid, IReadOnlyList<ThreeDmRenderMesh>> localMeshes,
        Dictionary<GeometryKey, int> geometryIndices,
        List<ThreeDmSharedMeshGeometry> geometries,
        List<ThreeDmSharedMeshInstance> instances,
        List<ThreeDmRenderDiagnostic> diagnostics)
    {
        if (sceneObject.Geometry is null || !IsEffectivelyVisible(sceneObject, layersById))
        {
            return;
        }

        var appearance = ThreeDmAppearanceResolver.Resolve(
            sceneObject,
            layersById,
            materialsById,
            inheritedAppearance);

        if (sceneObject.Geometry is ThreeDmInstanceReferenceGeometryData instanceReference)
        {
            if (!definitionsById.TryGetValue(instanceReference.InstanceDefinitionId, out var definition))
            {
                diagnostics.Add(new ThreeDmRenderDiagnostic(
                    sceneObject.Id,
                    "3DM_SHARED_INSTANCE_DEFINITION_MISSING",
                    $"Instance definition '{instanceReference.InstanceDefinitionId}' was not found."));
                return;
            }

            if (!definitionStack.Add(definition.Id))
            {
                diagnostics.Add(new ThreeDmRenderDiagnostic(
                    sceneObject.Id,
                    "3DM_SHARED_INSTANCE_CYCLE",
                    $"Cyclic instance definition '{definition.Name}' was detected and skipped."));
                return;
            }

            var childTransform = Multiply(accumulatedTransform, instanceReference.Transform);
            var childPath = AppendPath(instancePath, sceneObject.Id);
            foreach (var objectId in definition.ObjectIds)
            {
                if (!objectsById.TryGetValue(objectId, out var childObject))
                {
                    diagnostics.Add(new ThreeDmRenderDiagnostic(
                        sceneObject.Id,
                        "3DM_SHARED_INSTANCE_MEMBER_MISSING",
                        $"Instance definition '{definition.Name}' references missing object '{objectId}'."));
                    continue;
                }

                AppendObject(
                    childObject,
                    childTransform,
                    childPath,
                    appearance,
                    definitionStack,
                    document,
                    objectsById,
                    definitionsById,
                    layersById,
                    materialsById,
                    settings,
                    localMeshes,
                    geometryIndices,
                    geometries,
                    instances,
                    diagnostics);
            }

            definitionStack.Remove(definition.Id);
            return;
        }

        var meshes = GetLocalMeshes(sceneObject, document, settings, localMeshes, diagnostics);
        foreach (var mesh in meshes)
        {
            var key = new GeometryKey(sceneObject.Id, mesh.SourceSubobjectIndex);
            if (!geometryIndices.TryGetValue(key, out var geometryIndex))
            {
                geometryIndex = geometries.Count;
                geometryIndices.Add(key, geometryIndex);
                geometries.Add(new ThreeDmSharedMeshGeometry(
                    geometryIndex,
                    sceneObject.Id,
                    mesh.SourceSubobjectIndex,
                    mesh.Vertices,
                    mesh.Indices,
                    mesh.Normals,
                    mesh.TextureCoordinates,
                    ResolveBounds(mesh.Vertices)));
            }

            instances.Add(new ThreeDmSharedMeshInstance(
                geometryIndex,
                sceneObject.Id,
                mesh.SourceSubobjectIndex,
                accumulatedTransform,
                instancePath.ToArray())
            {
                MaterialId = appearance.MaterialId,
                ColorArgb = appearance.ColorArgb,
                Appearance = appearance,
            });
        }
    }

    private IReadOnlyList<ThreeDmRenderMesh> GetLocalMeshes(
        ThreeDmSceneObject sceneObject,
        ThreeDmSceneDocument document,
        ThreeDmTessellationSettings settings,
        Dictionary<Guid, IReadOnlyList<ThreeDmRenderMesh>> localMeshes,
        List<ThreeDmRenderDiagnostic> diagnostics)
    {
        if (localMeshes.TryGetValue(sceneObject.Id, out var cached))
        {
            return cached;
        }

        var localDocument = document with
        {
            Objects = [sceneObject with { IsVisible = true }],
            InstanceDefinitions = Array.Empty<ThreeDmInstanceDefinitionInfo>(),
        };
        var localScene = _geometryBuilder.Build(localDocument, settings);
        diagnostics.AddRange(localScene.Diagnostics);
        localMeshes.Add(sceneObject.Id, localScene.Meshes);
        return localScene.Meshes;
    }

    private static bool IsEffectivelyVisible(
        ThreeDmSceneObject sceneObject,
        Dictionary<Guid, ThreeDmLayerInfo> layersById) =>
        (sceneObject.SourceObjectVisible ?? sceneObject.IsVisible) &&
        ThreeDmAppearanceResolver.IsLayerEffectivelyVisible(sceneObject.LayerId, layersById);

    private static BoundingBox3d ResolveBounds(IReadOnlyList<ThreeDmRenderVertex> vertices)
    {
        if (vertices.Count == 0)
        {
            return BoundingBox3d.Invalid;
        }

        var first = vertices[0];
        var minX = first.X;
        var minY = first.Y;
        var minZ = first.Z;
        var maxX = first.X;
        var maxY = first.Y;
        var maxZ = first.Z;
        for (var i = 1; i < vertices.Count; i++)
        {
            var point = vertices[i];
            minX = Math.Min(minX, point.X);
            minY = Math.Min(minY, point.Y);
            minZ = Math.Min(minZ, point.Z);
            maxX = Math.Max(maxX, point.X);
            maxY = Math.Max(maxY, point.Y);
            maxZ = Math.Max(maxZ, point.Z);
        }

        return new BoundingBox3d(
            new Point3d(minX, minY, minZ),
            new Point3d(maxX, maxY, maxZ));
    }

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

    private static Guid[] AppendPath(IReadOnlyList<Guid> path, Guid instanceId)
    {
        var result = new Guid[path.Count + 1];
        for (var i = 0; i < path.Count; i++)
        {
            result[i] = path[i];
        }

        result[^1] = instanceId;
        return result;
    }

    private static readonly Transform3d IdentityTransform = new(
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1);

    private readonly record struct GeometryKey(Guid SourceObjectId, int? SourceSubobjectIndex);
}
