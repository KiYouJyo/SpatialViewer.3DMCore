using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Rhino.FileIO;
using Rhino.Geometry;
using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.Formats.ThreeDm.Rhino3dm;

public sealed class Rhino3dmThreeDmImporter : IThreeDmProgressiveImporter
{
    public const string PinnedPackageVersion = "8.32.0";

    public bool CanImport(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return string.Equals(Path.GetExtension(path), ".3dm", StringComparison.OrdinalIgnoreCase);
    }

    public ValueTask<ThreeDmSceneDocument> ImportAsync(
        string path,
        ThreeDmImportOptions? options = null,
        CancellationToken cancellationToken = default) =>
        ImportAsync(path, options, null, cancellationToken);

    public async ValueTask<ThreeDmSceneDocument> ImportAsync(
        string path,
        ThreeDmImportOptions? options,
        IProgress<ThreeDmImportProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        options ??= new ThreeDmImportOptions();
        ValidateImportRequest(path, options, cancellationToken);
        progress?.Report(new ThreeDmImportProgress(ThreeDmImportStage.ReadingArchive, 0, 0));

        var worker = Task.Run(
            () => ImportCore(path, options, progress, cancellationToken),
            CancellationToken.None);

        try
        {
            return await worker.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            ObserveFault(worker);
            throw;
        }
    }

    public async IAsyncEnumerable<ThreeDmProgressiveImportUpdate> ImportProgressivelyAsync(
        string path,
        ThreeDmImportOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new ThreeDmImportOptions();
        ValidateImportRequest(path, options, cancellationToken);

        var channel = Channel.CreateBounded<ThreeDmProgressiveImportUpdate>(new BoundedChannelOptions(4)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });
        var producer = Task.Run(
            () => ProduceProgressiveUpdatesAsync(path, options, channel.Writer, cancellationToken),
            CancellationToken.None);

        await foreach (var update in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return update;
        }

        await producer.ConfigureAwait(false);
    }

    private static ThreeDmSceneDocument ImportCore(
        string path,
        ThreeDmImportOptions options,
        IProgress<ThreeDmImportProgress>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            using var model = File3dm.Read(path);
            if (model is null)
            {
                throw new InvalidDataException($"Rhino3dm could not read '{path}'.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            ValidateModelLimits(model, options.Limits);
            progress?.Report(new ThreeDmImportProgress(
                ThreeDmImportStage.ReadingDocumentTables,
                0,
                model.Objects.Count));

            return BuildDocument(path, model, options, progress, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not FileNotFoundException && exception is not InvalidDataException)
        {
            throw new InvalidDataException($"Failed to read 3DM file '{path}'.", exception);
        }
    }

    private static async Task ProduceProgressiveUpdatesAsync(
        string path,
        ThreeDmImportOptions options,
        ChannelWriter<ThreeDmProgressiveImportUpdate> writer,
        CancellationToken cancellationToken)
    {
        try
        {
            using var model = File3dm.Read(path);
            if (model is null)
            {
                throw new InvalidDataException($"Rhino3dm could not read '{path}'.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            ValidateModelLimits(model, options.Limits);
            var header = ReadHeader(path, model);
            var layersById = header.Layers.ToDictionary(item => item.Id);
            var totalObjects = model.Objects.Count;

            await writer.WriteAsync(new ThreeDmImportHeaderUpdate(
                header.SourcePath,
                header.Properties,
                header.Layers,
                header.Materials,
                header.NamedViews,
                header.InstanceDefinitions,
                totalObjects), cancellationToken).ConfigureAwait(false);

            var allDiagnostics = new List<ThreeDmImportDiagnostic>();
            var batchDiagnostics = new List<ThreeDmImportDiagnostic>();
            var batchObjects = new List<ThreeDmSceneObject>(Math.Min(options.ProgressiveBatchSize, totalObjects));
            var bounds = BoundingBox3d.Invalid;
            var processedObjects = 0;
            var importedObjects = 0;

            foreach (var fileObject in model.Objects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sceneObject = ConvertObject(
                    model,
                    fileObject,
                    options,
                    layersById,
                    allDiagnostics,
                    batchDiagnostics);
                if (sceneObject is not null)
                {
                    batchObjects.Add(sceneObject);
                    bounds = bounds.Union(sceneObject.Bounds);
                    importedObjects++;
                }

                processedObjects++;
                if (processedObjects % options.ProgressiveBatchSize != 0 && processedObjects != totalObjects)
                {
                    continue;
                }

                await writer.WriteAsync(new ThreeDmImportObjectBatchUpdate(
                    batchObjects.ToArray(),
                    bounds,
                    batchDiagnostics.ToArray(),
                    processedObjects,
                    totalObjects), cancellationToken).ConfigureAwait(false);
                batchObjects.Clear();
                batchDiagnostics.Clear();
            }

            await writer.WriteAsync(new ThreeDmImportCompletedUpdate(
                bounds,
                allDiagnostics.ToArray(),
                importedObjects,
                totalObjects), cancellationToken).ConfigureAwait(false);
            writer.TryComplete();
        }
        catch (OperationCanceledException exception)
        {
            writer.TryComplete(exception);
        }
        catch (Exception exception)
        {
            writer.TryComplete(NormalizeReadException(path, exception));
        }
    }

    private static ThreeDmSceneDocument BuildDocument(
        string path,
        File3dm model,
        ThreeDmImportOptions options,
        IProgress<ThreeDmImportProgress>? progress,
        CancellationToken cancellationToken)
    {
        var header = ReadHeader(path, model);
        var layersById = header.Layers.ToDictionary(item => item.Id);
        var diagnostics = new List<ThreeDmImportDiagnostic>();
        var objects = new List<ThreeDmSceneObject>(model.Objects.Count);
        var documentBounds = BoundingBox3d.Invalid;
        var totalObjects = model.Objects.Count;
        var processedObjects = 0;

        progress?.Report(new ThreeDmImportProgress(
            ThreeDmImportStage.ConvertingObjects,
            0,
            totalObjects));

        foreach (var fileObject in model.Objects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sceneObject = ConvertObject(
                model,
                fileObject,
                options,
                layersById,
                diagnostics,
                null);
            if (sceneObject is not null)
            {
                objects.Add(sceneObject);
                documentBounds = documentBounds.Union(sceneObject.Bounds);
            }

            processedObjects++;
            if (processedObjects == totalObjects || processedObjects % options.ProgressIntervalObjects == 0)
            {
                progress?.Report(new ThreeDmImportProgress(
                    ThreeDmImportStage.ConvertingObjects,
                    processedObjects,
                    totalObjects));
            }
        }

        var document = new ThreeDmSceneDocument(
            header.SourcePath,
            objects,
            documentBounds,
            diagnostics)
        {
            Properties = header.Properties,
            Layers = header.Layers,
            Materials = header.Materials,
            NamedViews = header.NamedViews,
            InstanceDefinitions = header.InstanceDefinitions,
        };

        progress?.Report(new ThreeDmImportProgress(
            ThreeDmImportStage.Completed,
            totalObjects,
            totalObjects));
        return document;
    }

    private static ThreeDmSceneObject? ConvertObject(
        File3dm model,
        File3dmObject fileObject,
        ThreeDmImportOptions options,
        Dictionary<Guid, ThreeDmLayerInfo> layersById,
        List<ThreeDmImportDiagnostic> diagnostics,
        List<ThreeDmImportDiagnostic>? secondaryDiagnostics)
    {
        var geometry = fileObject.Geometry;
        var attributes = fileObject.Attributes;
        if (geometry is null || attributes is null)
        {
            AddDiagnostic(new ThreeDmImportDiagnostic(
                ThreeDmDiagnosticSeverity.Warning,
                "3DM_OBJECT_MISSING_DATA",
                "A 3DM object did not expose geometry or attributes and was skipped."), diagnostics, secondaryDiagnostics);
            return null;
        }

        var layerId = ResolveLayerId(model, attributes.LayerIndex);
        var materialId = ResolveMaterialId(model, attributes.MaterialIndex);
        var sourceVisible = attributes.Visible;
        var layerVisible = IsLayerEffectivelyVisible(layerId, layersById);
        var isVisible = sourceVisible && layerVisible;
        if (!options.IncludeHiddenObjects && !isVisible)
        {
            return null;
        }

        var kind = GetGeometryKind(geometry);
        if (kind == ThreeDmGeometryKind.Unknown)
        {
            AddDiagnostic(new ThreeDmImportDiagnostic(
                ThreeDmDiagnosticSeverity.Warning,
                "3DM_UNSUPPORTED_GEOMETRY",
                $"Geometry type '{geometry.GetType().Name}' is not yet semantically supported.",
                attributes.ObjectId), diagnostics, secondaryDiagnostics);
        }

        ValidateGeometryLimits(geometry, options.Limits.Geometry, options.IncludeRenderMeshes);
        var bounds = ConvertBounds(geometry.GetBoundingBox(true));
        ThreeDmGeometryData? semanticGeometry = null;
        try
        {
            semanticGeometry = Rhino3dmGeometryConverter.Convert(geometry, options.IncludeRenderMeshes);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            AddDiagnostic(new ThreeDmImportDiagnostic(
                ThreeDmDiagnosticSeverity.Warning,
                "3DM_GEOMETRY_CONVERSION_FAILED",
                $"Geometry type '{geometry.GetType().Name}' could not be converted: {exception.Message}",
                attributes.ObjectId), diagnostics, secondaryDiagnostics);
        }

        return new ThreeDmSceneObject(
            attributes.ObjectId,
            attributes.Name,
            layerId,
            kind,
            bounds,
            materialId,
            isVisible,
            ToArgb(attributes.ObjectColor),
            attributes.ColorSource.ToString(),
            attributes.MaterialSource.ToString(),
            semanticGeometry)
        {
            SourceObjectVisible = sourceVisible,
        };
    }

    private static void ValidateGeometryLimits(
        GeometryBase geometry,
        ThreeDmGeometryLimits limits,
        bool includeRenderMeshes)
    {
        switch (geometry)
        {
            case PointCloud pointCloud:
                EnsureAtMost("PointCloud point count", pointCloud.Count, limits.MaxPointCloudPoints);
                break;
            case Mesh mesh:
                ValidateMeshLimits(mesh, limits, "Mesh");
                break;
            case Brep brep:
                var topologyCount = (long)brep.Vertices.Count + brep.Edges.Count + brep.Trims.Count + brep.Loops.Count + brep.Faces.Count;
                EnsureAtMost("Brep topology item count", topologyCount, limits.MaxBrepTopologyItems);
                if (includeRenderMeshes)
                {
                    foreach (var face in brep.Faces)
                    {
                        var renderMesh = face.GetMesh(MeshType.Render);
                        if (renderMesh is not null)
                        {
                            ValidateMeshLimits(renderMesh, limits, "Brep render mesh");
                        }
                    }
                }

                break;
            case SubD subD:
                EnsureAtMost("SubD vertex count", subD.Vertices.Count, limits.MaxSubDVertices);
                EnsureAtMost("SubD face count", subD.Faces.Count, limits.MaxSubDFaces);
                break;
            case Extrusion extrusion when includeRenderMeshes:
                var extrusionMesh = extrusion.GetMesh(MeshType.Render);
                if (extrusionMesh is not null)
                {
                    ValidateMeshLimits(extrusionMesh, limits, "Extrusion render mesh");
                }

                break;
            case Curve curve:
                ValidateCurveLimits(curve, limits);
                break;
            case Surface surface:
                ValidateSurfaceLimits(surface, limits);
                break;
        }
    }

    private static void ValidateCurveLimits(Curve curve, ThreeDmGeometryLimits limits)
    {
        if (curve.TryGetPolyline(out var polyline))
        {
            EnsureAtMost("Polyline point count", polyline.Count, limits.MaxPolylinePoints);
        }

        using var nurbs = curve.ToNurbsCurve();
        if (nurbs is not null)
        {
            EnsureAtMost("NURBS curve control-point count", nurbs.Points.Count, limits.MaxNurbsControlPoints);
        }
    }

    private static void ValidateSurfaceLimits(Surface surface, ThreeDmGeometryLimits limits)
    {
        using var nurbs = surface.ToNurbsSurface();
        if (nurbs is null)
        {
            return;
        }

        var count = (long)nurbs.Points.CountU * nurbs.Points.CountV;
        EnsureAtMost("NURBS surface control-point count", count, limits.MaxNurbsControlPoints);
    }

    private static void ValidateMeshLimits(Mesh mesh, ThreeDmGeometryLimits limits, string label)
    {
        EnsureAtMost($"{label} vertex count", mesh.Vertices.Count, limits.MaxMeshVertices);
        EnsureAtMost($"{label} face count", mesh.Faces.Count, limits.MaxMeshFaces);
    }

    private static void EnsureAtMost(string label, long actual, int limit)
    {
        if (actual > limit)
        {
            throw new InvalidDataException($"3DM {label} {actual} exceeds the configured limit of {limit}.");
        }
    }

    private static DocumentHeader ReadHeader(string path, File3dm model) =>
        new(
            Path.GetFullPath(path),
            CreateProperties(model),
            ReadLayers(model),
            ReadMaterials(model),
            ReadNamedViews(model),
            ReadInstanceDefinitions(model));

    private static ThreeDmDocumentProperties CreateProperties(File3dm model) =>
        new(
            model.ArchiveVersion,
            model.ApplicationName,
            model.ApplicationUrl,
            model.ApplicationDetails,
            model.CreatedBy,
            model.LastEditedBy,
            model.Revision,
            model.Settings.ModelUnitSystem.ToString(),
            model.Settings.ModelAbsoluteTolerance,
            model.Settings.ModelAngleToleranceRadians,
            model.Settings.ModelRelativeTolerance);

    private static void ValidateImportRequest(
        string path,
        ThreeDmImportOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();
        options.Validate();

        var file = new FileInfo(path);
        if (!file.Exists)
        {
            throw new FileNotFoundException("3DM file was not found.", path);
        }

        if (file.Length > options.Limits.MaxFileSizeBytes)
        {
            throw new InvalidDataException(
                $"3DM file size {file.Length} bytes exceeds the configured limit of {options.Limits.MaxFileSizeBytes} bytes.");
        }
    }

    private static void ValidateModelLimits(File3dm model, ThreeDmImportLimits limits)
    {
        if (model.Objects.Count > limits.MaxObjectCount)
        {
            throw new InvalidDataException(
                $"3DM object count {model.Objects.Count} exceeds the configured limit of {limits.MaxObjectCount}.");
        }

        if (model.AllLayers.Count > limits.MaxLayerCount)
        {
            throw new InvalidDataException(
                $"3DM layer count {model.AllLayers.Count} exceeds the configured limit of {limits.MaxLayerCount}.");
        }

        if (model.AllMaterials.Count > limits.MaxMaterialCount)
        {
            throw new InvalidDataException(
                $"3DM material count {model.AllMaterials.Count} exceeds the configured limit of {limits.MaxMaterialCount}.");
        }

        if (model.AllInstanceDefinitions.Count > limits.MaxInstanceDefinitionCount)
        {
            throw new InvalidDataException(
                $"3DM instance-definition count {model.AllInstanceDefinitions.Count} exceeds the configured limit of {limits.MaxInstanceDefinitionCount}.");
        }
    }

    private static void AddDiagnostic(
        ThreeDmImportDiagnostic diagnostic,
        List<ThreeDmImportDiagnostic> diagnostics,
        List<ThreeDmImportDiagnostic>? secondaryDiagnostics)
    {
        diagnostics.Add(diagnostic);
        secondaryDiagnostics?.Add(diagnostic);
    }

    private static Exception NormalizeReadException(string path, Exception exception) =>
        exception is FileNotFoundException or InvalidDataException
            ? exception
            : new InvalidDataException($"Failed to read 3DM file '{path}'.", exception);

    private static void ObserveFault(Task worker)
    {
        _ = worker.ContinueWith(
            static completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    private static List<ThreeDmLayerInfo> ReadLayers(File3dm model)
    {
        var result = new List<ThreeDmLayerInfo>(model.AllLayers.Count);
        foreach (var layer in model.AllLayers)
        {
            result.Add(new ThreeDmLayerInfo(
                layer.Id,
                layer.Name ?? string.Empty,
                layer.ParentLayerId == Guid.Empty ? null : layer.ParentLayerId,
                layer.IsVisible,
                layer.IsLocked,
                ToArgb(layer.Color),
                layer.LinetypeIndex)
            {
                RenderMaterialId = ResolveMaterialId(model, layer.RenderMaterialIndex),
            });
        }

        return result;
    }

    private static List<ThreeDmMaterialInfo> ReadMaterials(File3dm model)
    {
        var result = new List<ThreeDmMaterialInfo>(model.AllMaterials.Count);
        foreach (var material in model.AllMaterials)
        {
            var physicallyBased = material.IsPhysicallyBased ? material.PhysicallyBased : null;
            var textures = material.GetTextures()
                .Select(texture => new ThreeDmMaterialTextureInfo(
                    texture.FileName ?? string.Empty,
                    texture.TextureType.ToString(),
                    texture.Enabled,
                    texture.MappingChannelId,
                    texture.ProjectionMode.ToString(),
                    texture.WrapU.ToString(),
                    texture.WrapV.ToString(),
                    texture.WrapW.ToString(),
                    texture.Repeat.X,
                    texture.Repeat.Y,
                    texture.Offset.X,
                    texture.Offset.Y,
                    texture.Rotation))
                .ToArray();

            result.Add(new ThreeDmMaterialInfo(
                material.Id,
                material.Name ?? string.Empty,
                ToArgb(material.DiffuseColor),
                material.Transparency)
            {
                SpecularColorArgb = ToArgb(material.SpecularColor),
                EmissionColorArgb = ToArgb(material.EmissionColor),
                Shine = material.Shine,
                Reflectivity = material.Reflectivity,
                PhysicallyBased = physicallyBased is null
                    ? null
                    : new ThreeDmPhysicallyBasedMaterialInfo(
                        physicallyBased.BaseColor.R,
                        physicallyBased.BaseColor.G,
                        physicallyBased.BaseColor.B,
                        physicallyBased.BaseColor.A,
                        physicallyBased.Metallic,
                        physicallyBased.Roughness,
                        physicallyBased.Alpha,
                        physicallyBased.Opacity,
                        physicallyBased.Clearcoat,
                        physicallyBased.ClearcoatRoughness,
                        physicallyBased.BRDF.ToString()),
                Textures = textures,
            });
        }

        return result;
    }

    private static List<ThreeDmNamedViewInfo> ReadNamedViews(File3dm model)
    {
        var result = new List<ThreeDmNamedViewInfo>(model.AllNamedViews.Count);
        foreach (var view in model.AllNamedViews)
        {
            var viewport = view.Viewport;
            ThreeDmViewFrustumInfo? frustum = null;
            if (viewport.GetFrustum(
                    out var left,
                    out var right,
                    out var bottom,
                    out var top,
                    out var nearDistance,
                    out var farDistance))
            {
                var candidate = new ThreeDmViewFrustumInfo(
                    left,
                    right,
                    bottom,
                    top,
                    nearDistance,
                    farDistance);
                frustum = candidate.IsValid ? candidate : null;
            }

            result.Add(new ThreeDmNamedViewInfo(
                view.Name ?? string.Empty,
                ConvertPoint(viewport.CameraLocation),
                ConvertVector(viewport.CameraDirection),
                ConvertVector(viewport.CameraUp),
                ConvertPoint(viewport.TargetPoint),
                viewport.IsPerspectiveProjection)
            {
                Camera35mmLensLength = viewport.Camera35mmLensLength,
                Frustum = frustum,
            });
        }

        return result;
    }

    private static List<ThreeDmInstanceDefinitionInfo> ReadInstanceDefinitions(File3dm model)
    {
        var result = new List<ThreeDmInstanceDefinitionInfo>(model.AllInstanceDefinitions.Count);
        foreach (var definition in model.AllInstanceDefinitions)
        {
            result.Add(new ThreeDmInstanceDefinitionInfo(
                definition.Id,
                definition.Name ?? string.Empty,
                definition.Description ?? string.Empty,
                string.IsNullOrWhiteSpace(definition.SourceArchive) ? null : definition.SourceArchive,
                definition.GetObjectIds()));
        }

        return result;
    }

    private static Guid? ResolveLayerId(File3dm model, int layerIndex)
    {
        if (layerIndex < 0)
        {
            return null;
        }

        var layer = model.AllLayers.FindIndex(layerIndex);
        if (layer is null || layer.Id == Guid.Empty)
        {
            return null;
        }

        return layer.Id;
    }

    private static bool IsLayerEffectivelyVisible(
        Guid? layerId,
        Dictionary<Guid, ThreeDmLayerInfo> layersById)
    {
        if (layerId is null)
        {
            return true;
        }

        var visited = new HashSet<Guid>();
        var currentId = layerId;
        while (currentId is Guid id && layersById.TryGetValue(id, out var layer))
        {
            if (!visited.Add(id))
            {
                return false;
            }

            if (!layer.IsVisible)
            {
                return false;
            }

            currentId = layer.ParentLayerId;
        }

        return true;
    }

    private static Guid? ResolveMaterialId(File3dm model, int materialIndex)
    {
        if (materialIndex < 0)
        {
            return null;
        }

        var material = model.AllMaterials.FindIndex(materialIndex);
        if (material is null || material.Id == Guid.Empty)
        {
            return null;
        }

        return material.Id;
    }

    private static ThreeDmGeometryKind GetGeometryKind(GeometryBase geometry)
    {
        return geometry switch
        {
            Rhino.Geometry.Point => ThreeDmGeometryKind.Point,
            PointCloud => ThreeDmGeometryKind.PointCloud,
            Brep => ThreeDmGeometryKind.Brep,
            Extrusion => ThreeDmGeometryKind.Extrusion,
            Mesh => ThreeDmGeometryKind.Mesh,
            SubD => ThreeDmGeometryKind.SubD,
            InstanceReferenceGeometry => ThreeDmGeometryKind.InstanceReference,
            Hatch => ThreeDmGeometryKind.Hatch,
            Rhino.Geometry.Light => ThreeDmGeometryKind.Light,
            ClippingPlaneSurface => ThreeDmGeometryKind.ClippingPlane,
            Curve => ThreeDmGeometryKind.Curve,
            Surface => ThreeDmGeometryKind.Surface,
            TextDot => ThreeDmGeometryKind.TextDot,
            AnnotationBase => ThreeDmGeometryKind.Annotation,
            _ => ThreeDmGeometryKind.Unknown,
        };
    }

    private static BoundingBox3d ConvertBounds(Rhino.Geometry.BoundingBox bounds)
    {
        if (!bounds.IsValid)
        {
            return BoundingBox3d.Invalid;
        }

        return new BoundingBox3d(ConvertPoint(bounds.Min), ConvertPoint(bounds.Max));
    }

    private static SpatialViewer.ThreeDm.Core.Point3d ConvertPoint(Rhino.Geometry.Point3d point) =>
        new(point.X, point.Y, point.Z);

    private static SpatialViewer.ThreeDm.Core.Vector3d ConvertVector(Rhino.Geometry.Vector3d vector) =>
        new(vector.X, vector.Y, vector.Z);

    private static uint ToArgb(System.Drawing.Color color) => unchecked((uint)color.ToArgb());

    private sealed record DocumentHeader(
        string SourcePath,
        ThreeDmDocumentProperties Properties,
        IReadOnlyList<ThreeDmLayerInfo> Layers,
        IReadOnlyList<ThreeDmMaterialInfo> Materials,
        IReadOnlyList<ThreeDmNamedViewInfo> NamedViews,
        IReadOnlyList<ThreeDmInstanceDefinitionInfo> InstanceDefinitions);
}
