using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Integration;

public sealed record ThreeDmSemanticOverlay(
    Guid SourceObjectId,
    ThreeDmGeometryKind GeometryKind,
    ThreeDmGeometryData Geometry,
    Transform3d Transform,
    IReadOnlyList<Guid> InstancePath);

public static class ThreeDmSemanticOverlayCatalog
{
    public static IReadOnlyList<ThreeDmSemanticOverlay> Create(ThreeDmSceneDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var objects = document.Objects.ToDictionary(item => item.Id);
        var definitions = document.InstanceDefinitions.ToDictionary(item => item.Id);
        var members = document.InstanceDefinitions.SelectMany(item => item.ObjectIds).ToHashSet();
        var result = new List<ThreeDmSemanticOverlay>();

        foreach (var sceneObject in document.Objects)
        {
            if (!IsObjectVisible(sceneObject, document) || members.Contains(sceneObject.Id))
            {
                continue;
            }

            Append(
                sceneObject,
                Identity,
                Array.Empty<Guid>(),
                new HashSet<Guid>(),
                objects,
                definitions,
                document,
                result);
        }

        return result;
    }

    private static void Append(
        ThreeDmSceneObject sceneObject,
        Transform3d accumulatedTransform,
        IReadOnlyList<Guid> instancePath,
        HashSet<Guid> definitionStack,
        IReadOnlyDictionary<Guid, ThreeDmSceneObject> objects,
        IReadOnlyDictionary<Guid, ThreeDmInstanceDefinitionInfo> definitions,
        ThreeDmSceneDocument document,
        List<ThreeDmSemanticOverlay> result)
    {
        if (!IsObjectVisible(sceneObject, document) || sceneObject.Geometry is null)
        {
            return;
        }

        if (sceneObject.Geometry is ThreeDmInstanceReferenceGeometryData reference)
        {
            if (!definitions.TryGetValue(reference.InstanceDefinitionId, out var definition) ||
                !definitionStack.Add(definition.Id))
            {
                return;
            }

            var transform = Multiply(accumulatedTransform, reference.Transform);
            var path = AppendPath(instancePath, sceneObject.Id);
            foreach (var objectId in definition.ObjectIds)
            {
                if (objects.TryGetValue(objectId, out var member))
                {
                    Append(member, transform, path, definitionStack, objects, definitions, document, result);
                }
            }

            definitionStack.Remove(definition.Id);
            return;
        }

        if (sceneObject.GeometryKind is
            ThreeDmGeometryKind.Annotation or
            ThreeDmGeometryKind.TextDot or
            ThreeDmGeometryKind.Hatch or
            ThreeDmGeometryKind.Light or
            ThreeDmGeometryKind.ClippingPlane)
        {
            result.Add(new ThreeDmSemanticOverlay(
                sceneObject.Id,
                sceneObject.GeometryKind,
                sceneObject.Geometry,
                accumulatedTransform,
                instancePath.ToArray()));
        }
    }

    private static bool IsObjectVisible(ThreeDmSceneObject sceneObject, ThreeDmSceneDocument document)
    {
        if (!(sceneObject.SourceObjectVisible ?? sceneObject.IsVisible))
        {
            return false;
        }

        return sceneObject.LayerId is not Guid layerId ||
               ThreeDmLayerTreeBuilder.IsEffectivelyVisible(layerId, document);
    }

    private static Guid[] AppendPath(IReadOnlyList<Guid> path, Guid instanceId)
    {
        var value = new Guid[path.Count + 1];
        for (var i = 0; i < path.Count; i++)
        {
            value[i] = path[i];
        }

        value[^1] = instanceId;
        return value;
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

    private static readonly Transform3d Identity = new(
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1);
}

public sealed partial class ThreeDmSession
{
    public IReadOnlyList<ThreeDmSemanticOverlay> GetSemanticOverlays() =>
        ThreeDmSemanticOverlayCatalog.Create(GetDisplayDocument());
}
