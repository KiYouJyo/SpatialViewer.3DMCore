using System.Collections.Concurrent;
using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Rendering;

public sealed class ThreeDmRenderSceneBuilder
{
    private readonly ConcurrentDictionary<TessellationCacheKey, LocalRenderObject> _cache = new();

    public int CacheEntryCount => _cache.Count;

    public void ClearCache() => _cache.Clear();

    public ThreeDmRenderScene Build(ThreeDmSceneDocument document, ThreeDmTessellationSettings? settings = null) =>
        Build(document, settings, ThreeDmRenderPrimitiveMask.All);

    public ThreeDmRenderScene Build(
        ThreeDmSceneDocument document,
        ThreeDmTessellationSettings? settings,
        ThreeDmRenderPrimitiveMask primitiveMask)
    {
        ArgumentNullException.ThrowIfNull(document);
        settings ??= new ThreeDmTessellationSettings();

        var meshes = new List<ThreeDmRenderMesh>();
        var pointSets = new List<ThreeDmRenderPointSet>();
        var curves = new List<ThreeDmRenderCurve>();
        var diagnostics = new List<ThreeDmRenderDiagnostic>();
        var objectsById = document.Objects.ToDictionary(item => item.Id);
        var definitionsById = document.InstanceDefinitions.ToDictionary(item => item.Id);
        var definitionMemberIds = document.InstanceDefinitions
            .SelectMany(item => item.ObjectIds)
            .ToHashSet();
        var modelTolerance = document.Properties?.ModelAbsoluteTolerance ?? 0;

        foreach (var sceneObject in document.Objects)
        {
            if (!sceneObject.IsVisible || definitionMemberIds.Contains(sceneObject.Id))
            {
                continue;
            }

            AppendObject(
                sceneObject,
                IdentityTransform,
                Array.Empty<Guid>(),
                new HashSet<Guid>(),
                objectsById,
                definitionsById,
                settings,
                primitiveMask,
                modelTolerance,
                meshes,
                pointSets,
                curves,
                diagnostics);
        }

        return new ThreeDmRenderScene(meshes)
        {
            PointSets = pointSets,
            Curves = curves,
            Diagnostics = diagnostics,
        };
    }

    private void AppendObject(
        ThreeDmSceneObject sceneObject,
        Transform3d accumulatedTransform,
        IReadOnlyList<Guid> instancePath,
        HashSet<Guid> definitionStack,
        IReadOnlyDictionary<Guid, ThreeDmSceneObject> objectsById,
        IReadOnlyDictionary<Guid, ThreeDmInstanceDefinitionInfo> definitionsById,
        ThreeDmTessellationSettings settings,
        ThreeDmRenderPrimitiveMask primitiveMask,
        double modelTolerance,
        List<ThreeDmRenderMesh> meshes,
        List<ThreeDmRenderPointSet> pointSets,
        List<ThreeDmRenderCurve> curves,
        List<ThreeDmRenderDiagnostic> diagnostics)
    {
        if (!sceneObject.IsVisible || sceneObject.Geometry is null)
        {
            return;
        }

        if (sceneObject.Geometry is ThreeDmInstanceReferenceGeometryData instanceReference)
        {
            if (!definitionsById.TryGetValue(instanceReference.InstanceDefinitionId, out var definition))
            {
                diagnostics.Add(new ThreeDmRenderDiagnostic(
                    sceneObject.Id,
                    "3DM_RENDER_INSTANCE_DEFINITION_MISSING",
                    $"Instance definition '{instanceReference.InstanceDefinitionId}' was not found."));
                return;
            }

            if (!definitionStack.Add(definition.Id))
            {
                diagnostics.Add(new ThreeDmRenderDiagnostic(
                    sceneObject.Id,
                    "3DM_RENDER_INSTANCE_CYCLE",
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
                        "3DM_RENDER_INSTANCE_MEMBER_MISSING",
                        $"Instance definition '{definition.Name}' references missing object '{objectId}'."));
                    continue;
                }

                AppendObject(
                    childObject,
                    childTransform,
                    childPath,
                    definitionStack,
                    objectsById,
                    definitionsById,
                    settings,
                    primitiveMask,
                    modelTolerance,
                    meshes,
                    pointSets,
                    curves,
                    diagnostics);
            }

            definitionStack.Remove(definition.Id);
            return;
        }

        var cacheTolerance = settings.ResolveCacheChordTolerance(sceneObject.Bounds, modelTolerance);
        var tessellationSettings = settings with { AbsoluteChordTolerance = cacheTolerance };
        var key = new TessellationCacheKey(
            sceneObject.Id,
            sceneObject.GeometryKind,
            settings.Quality,
            cacheTolerance,
            settings.IncludeBrepEdges,
            settings.MaxCurveSegments,
            settings.MaxSurfaceSegmentsPerDirection);

        LocalRenderObject local;
        try
        {
            local = _cache.GetOrAdd(key, _ => TessellateLocal(sceneObject, tessellationSettings, modelTolerance));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            diagnostics.Add(new ThreeDmRenderDiagnostic(
                sceneObject.Id,
                "3DM_RENDER_TESSELLATION_FAILED",
                $"{sceneObject.GeometryKind} tessellation failed: {exception.Message}"));
            return;
        }

        if ((primitiveMask & ThreeDmRenderPrimitiveMask.Meshes) != 0)
        {
            foreach (var mesh in local.Meshes)
            {
                meshes.Add(TransformMesh(mesh, accumulatedTransform, instancePath));
            }
        }

        if ((primitiveMask & ThreeDmRenderPrimitiveMask.PointSets) != 0)
        {
            foreach (var pointSet in local.PointSets)
            {
                pointSets.Add(TransformPointSet(pointSet, accumulatedTransform, instancePath));
            }
        }

        if ((primitiveMask & ThreeDmRenderPrimitiveMask.Curves) != 0)
        {
            foreach (var curve in local.Curves)
            {
                curves.Add(TransformCurve(curve, accumulatedTransform, instancePath));
            }
        }

        diagnostics.AddRange(local.Diagnostics);
    }

    private static LocalRenderObject TessellateLocal(
        ThreeDmSceneObject sceneObject,
        ThreeDmTessellationSettings settings,
        double modelTolerance)
    {
        var meshes = new List<ThreeDmRenderMesh>();
        var pointSets = new List<ThreeDmRenderPointSet>();
        var curves = new List<ThreeDmRenderCurve>();
        var diagnostics = new List<ThreeDmRenderDiagnostic>();

        switch (sceneObject.Geometry)
        {
            case ThreeDmPointGeometryData point:
                pointSets.Add(new ThreeDmRenderPointSet(sceneObject.Id, [ToRenderVertex(point.Position)]));
                break;

            case ThreeDmPointCloudGeometryData pointCloud:
                pointSets.Add(new ThreeDmRenderPointSet(
                    sceneObject.Id,
                    pointCloud.Points.Select(ToRenderVertex).ToArray()));
                break;

            case ThreeDmCurveGeometryData curve:
                curves.Add(ThreeDmCurveTessellator.Tessellate(sceneObject.Id, curve, settings, modelTolerance));
                break;

            case ThreeDmMeshGeometryData mesh:
                meshes.Add(ThreeDmMeshTessellator.Tessellate(
                    sceneObject.Id,
                    mesh,
                    sceneObject.MaterialId,
                    sceneObject.ObjectColorArgb));
                break;

            case ThreeDmBrepGeometryData brep:
                var hasBrepRenderMeshes = AddEmbeddedRenderMeshes(sceneObject, brep.RenderMeshes, meshes);
                if (settings.IncludeBrepEdges)
                {
                    foreach (var edge in brep.Edges)
                    {
                        curves.Add(ThreeDmCurveTessellator.Tessellate(
                            sceneObject.Id,
                            edge.Curve,
                            settings,
                            modelTolerance,
                            edge.Index));
                    }
                }

                if (!hasBrepRenderMeshes)
                {
                    diagnostics.Add(new ThreeDmRenderDiagnostic(
                        sceneObject.Id,
                        "3DM_RENDER_BREP_FILL_REQUIRES_RENDER_MESH",
                        "No embedded Rhino render mesh was stored for this Brep; exact edge overlays remain available."));
                }
                break;

            case ThreeDmExtrusionGeometryData extrusion:
                var hasExtrusionRenderMeshes = AddEmbeddedRenderMeshes(sceneObject, extrusion.RenderMeshes, meshes);
                TessellateExtrusionWireframe(sceneObject.Id, extrusion, settings, modelTolerance, curves);
                if (!hasExtrusionRenderMeshes)
                {
                    diagnostics.Add(new ThreeDmRenderDiagnostic(
                        sceneObject.Id,
                        "3DM_RENDER_EXTRUSION_FILL_REQUIRES_RENDER_MESH",
                        "No embedded Rhino render mesh was stored for this extrusion; analytic wireframe remains available."));
                }
                break;

            case ThreeDmHatchGeometryData hatch:
                var hatchIndex = 0;
                foreach (var boundary in hatch.OuterBoundaries.Concat(hatch.InnerBoundaries))
                {
                    curves.Add(ThreeDmCurveTessellator.Tessellate(
                        sceneObject.Id,
                        boundary,
                        settings,
                        modelTolerance,
                        hatchIndex++));
                }
                break;

            case ThreeDmAnnotationGeometryData annotation when annotation.LeaderPoints.Count > 1:
                curves.Add(new ThreeDmRenderCurve(
                    sceneObject.Id,
                    ThreeDmRenderCurveKind.Polyline,
                    annotation.LeaderPoints.Select(ToRenderVertex).ToArray(),
                    false,
                    settings.ResolveChordTolerance(annotation.Bounds, modelTolerance)));
                break;

            case ThreeDmSubDGeometryData subD:
                TessellateSubDControlNet(sceneObject.Id, subD, settings, modelTolerance, curves);
                diagnostics.Add(new ThreeDmRenderDiagnostic(
                    sceneObject.Id,
                    "3DM_RENDER_SUBD_LIMIT_MESH_PENDING",
                    "SubD control-net wireframe is available; smooth limit-surface rendering requires a future compatible display-mesh path."));
                break;

            case ThreeDmNurbsSurfaceGeometryData surface:
                meshes.Add(ThreeDmNurbsSurfaceTessellator.Tessellate(
                    sceneObject.Id,
                    surface,
                    settings,
                    modelTolerance,
                    sceneObject.MaterialId,
                    sceneObject.ObjectColorArgb));
                break;
        }

        return new LocalRenderObject(meshes, pointSets, curves, diagnostics);
    }

    private static bool AddEmbeddedRenderMeshes(
        ThreeDmSceneObject sceneObject,
        IReadOnlyList<ThreeDmEmbeddedRenderMeshData> embeddedMeshes,
        List<ThreeDmRenderMesh> output)
    {
        var added = false;
        foreach (var embedded in embeddedMeshes)
        {
            var rendered = ThreeDmMeshTessellator.Tessellate(
                sceneObject.Id,
                embedded.Mesh,
                sceneObject.MaterialId,
                sceneObject.ObjectColorArgb) with
            {
                SourceSubobjectIndex = embedded.SourceSubobjectIndex,
            };
            output.Add(rendered);
            added = true;
        }

        return added;
    }

    private static void TessellateExtrusionWireframe(
        Guid sourceObjectId,
        ThreeDmExtrusionGeometryData extrusion,
        ThreeDmTessellationSettings settings,
        double modelTolerance,
        List<ThreeDmRenderCurve> output)
    {
        var delta = new ThreeDmRenderVertex(
            extrusion.PathEnd.X - extrusion.PathStart.X,
            extrusion.PathEnd.Y - extrusion.PathStart.Y,
            extrusion.PathEnd.Z - extrusion.PathStart.Z);

        for (var profileIndex = 0; profileIndex < extrusion.Profiles.Count; profileIndex++)
        {
            var startCurve = ThreeDmCurveTessellator.Tessellate(
                sourceObjectId,
                extrusion.Profiles[profileIndex],
                settings,
                modelTolerance,
                profileIndex * 2);
            output.Add(startCurve);

            var endPoints = startCurve.Points
                .Select(point => new ThreeDmRenderVertex(point.X + delta.X, point.Y + delta.Y, point.Z + delta.Z))
                .ToArray();
            output.Add(startCurve with
            {
                Points = endPoints,
                SourceSubobjectIndex = (profileIndex * 2) + 1,
            });

            if (startCurve.Points.Count < 2)
            {
                continue;
            }

            var connectorBudget = Math.Min(16, startCurve.Points.Count - 1);
            var stride = Math.Max(1, (startCurve.Points.Count - 1) / Math.Max(1, connectorBudget));
            for (var i = 0; i < startCurve.Points.Count - 1; i += stride)
            {
                output.Add(new ThreeDmRenderCurve(
                    sourceObjectId,
                    ThreeDmRenderCurveKind.Line,
                    [startCurve.Points[i], endPoints[i]],
                    false,
                    startCurve.TargetChordTolerance,
                    10_000 + (profileIndex * 100) + i));
            }
        }
    }

    private static void TessellateSubDControlNet(
        Guid sourceObjectId,
        ThreeDmSubDGeometryData subD,
        ThreeDmTessellationSettings settings,
        double modelTolerance,
        List<ThreeDmRenderCurve> output)
    {
        var vertices = subD.Vertices.ToDictionary(item => item.Id, item => item.ControlNetPoint);
        var edges = new HashSet<(uint A, uint B)>();
        var tolerance = settings.ResolveChordTolerance(subD.Bounds, modelTolerance);

        foreach (var face in subD.Faces)
        {
            for (var i = 0; i < face.VertexIds.Count; i++)
            {
                var a = face.VertexIds[i];
                var b = face.VertexIds[(i + 1) % face.VertexIds.Count];
                var edge = a <= b ? (a, b) : (b, a);
                if (!edges.Add(edge) || !vertices.TryGetValue(a, out var pointA) || !vertices.TryGetValue(b, out var pointB))
                {
                    continue;
                }

                output.Add(new ThreeDmRenderCurve(
                    sourceObjectId,
                    ThreeDmRenderCurveKind.Line,
                    [ToRenderVertex(pointA), ToRenderVertex(pointB)],
                    false,
                    tolerance));
            }
        }
    }

    private static ThreeDmRenderMesh TransformMesh(
        ThreeDmRenderMesh mesh,
        Transform3d transform,
        IReadOnlyList<Guid> instancePath)
    {
        var vertices = mesh.Vertices.Select(point => TransformPoint(point, transform)).ToArray();
        var normals = mesh.Normals.Select(normal => TransformNormal(normal, transform)).ToArray();
        return mesh with
        {
            Vertices = vertices,
            Normals = normals,
            InstancePath = instancePath.ToArray(),
        };
    }

    private static ThreeDmRenderPointSet TransformPointSet(
        ThreeDmRenderPointSet pointSet,
        Transform3d transform,
        IReadOnlyList<Guid> instancePath) =>
        pointSet with
        {
            Points = pointSet.Points.Select(point => TransformPoint(point, transform)).ToArray(),
            InstancePath = instancePath.ToArray(),
        };

    private static ThreeDmRenderCurve TransformCurve(
        ThreeDmRenderCurve curve,
        Transform3d transform,
        IReadOnlyList<Guid> instancePath) =>
        curve with
        {
            Points = curve.Points.Select(point => TransformPoint(point, transform)).ToArray(),
            InstancePath = instancePath.ToArray(),
        };

    private static ThreeDmRenderVertex TransformPoint(ThreeDmRenderVertex point, Transform3d transform)
    {
        var x = (transform.M00 * point.X) + (transform.M01 * point.Y) + (transform.M02 * point.Z) + transform.M03;
        var y = (transform.M10 * point.X) + (transform.M11 * point.Y) + (transform.M12 * point.Z) + transform.M13;
        var z = (transform.M20 * point.X) + (transform.M21 * point.Y) + (transform.M22 * point.Z) + transform.M23;
        var w = (transform.M30 * point.X) + (transform.M31 * point.Y) + (transform.M32 * point.Z) + transform.M33;
        if (Math.Abs(w) > 1e-15 && Math.Abs(w - 1.0) > 1e-15)
        {
            x /= w;
            y /= w;
            z /= w;
        }

        return new ThreeDmRenderVertex(x, y, z);
    }

    private static ThreeDmRenderNormal TransformNormal(ThreeDmRenderNormal normal, Transform3d transform)
    {
        var a = transform.M00;
        var b = transform.M01;
        var c = transform.M02;
        var d = transform.M10;
        var e = transform.M11;
        var f = transform.M12;
        var g = transform.M20;
        var h = transform.M21;
        var i = transform.M22;
        var determinant = (a * ((e * i) - (f * h))) - (b * ((d * i) - (f * g))) + (c * ((d * h) - (e * g)));

        double x;
        double y;
        double z;
        if (Math.Abs(determinant) > 1e-15)
        {
            var inverse00 = ((e * i) - (f * h)) / determinant;
            var inverse01 = ((c * h) - (b * i)) / determinant;
            var inverse02 = ((b * f) - (c * e)) / determinant;
            var inverse10 = ((f * g) - (d * i)) / determinant;
            var inverse11 = ((a * i) - (c * g)) / determinant;
            var inverse12 = ((c * d) - (a * f)) / determinant;
            var inverse20 = ((d * h) - (e * g)) / determinant;
            var inverse21 = ((b * g) - (a * h)) / determinant;
            var inverse22 = ((a * e) - (b * d)) / determinant;

            x = (inverse00 * normal.X) + (inverse10 * normal.Y) + (inverse20 * normal.Z);
            y = (inverse01 * normal.X) + (inverse11 * normal.Y) + (inverse21 * normal.Z);
            z = (inverse02 * normal.X) + (inverse12 * normal.Y) + (inverse22 * normal.Z);
        }
        else
        {
            x = (a * normal.X) + (b * normal.Y) + (c * normal.Z);
            y = (d * normal.X) + (e * normal.Y) + (f * normal.Z);
            z = (g * normal.X) + (h * normal.Y) + (i * normal.Z);
        }

        var length = Math.Sqrt((x * x) + (y * y) + (z * z));
        return length > 1e-15
            ? new ThreeDmRenderNormal(x / length, y / length, z / length)
            : normal;
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

    private static ThreeDmRenderVertex ToRenderVertex(Point3d point) => new(point.X, point.Y, point.Z);

    private static readonly Transform3d IdentityTransform = new(
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1);

    private readonly record struct TessellationCacheKey(
        Guid SourceObjectId,
        ThreeDmGeometryKind GeometryKind,
        ThreeDmTessellationQuality Quality,
        double ChordTolerance,
        bool IncludeBrepEdges,
        int MaxCurveSegments,
        int MaxSurfaceSegmentsPerDirection);

    private sealed record LocalRenderObject(
        IReadOnlyList<ThreeDmRenderMesh> Meshes,
        IReadOnlyList<ThreeDmRenderPointSet> PointSets,
        IReadOnlyList<ThreeDmRenderCurve> Curves,
        IReadOnlyList<ThreeDmRenderDiagnostic> Diagnostics);
}
