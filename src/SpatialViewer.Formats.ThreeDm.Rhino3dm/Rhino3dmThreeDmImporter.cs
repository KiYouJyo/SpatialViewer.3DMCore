using Rhino.FileIO;
using Rhino.Geometry;
using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.Formats.ThreeDm.Rhino3dm;

public sealed class Rhino3dmThreeDmImporter : IThreeDmImporter
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
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(path))
        {
            return ValueTask.FromException<ThreeDmSceneDocument>(new FileNotFoundException("3DM file was not found.", path));
        }

        options ??= new ThreeDmImportOptions();

        try
        {
            using var model = File3dm.Read(path);
            if (model is null)
            {
                return ValueTask.FromException<ThreeDmSceneDocument>(
                    new InvalidDataException($"Rhino3dm could not read '{path}'."));
            }

            return ValueTask.FromResult(BuildDocument(path, model, options, cancellationToken));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not FileNotFoundException)
        {
            return ValueTask.FromException<ThreeDmSceneDocument>(
                new InvalidDataException($"Failed to read 3DM file '{path}'.", exception));
        }
    }

    private static ThreeDmSceneDocument BuildDocument(
        string path,
        File3dm model,
        ThreeDmImportOptions options,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<ThreeDmImportDiagnostic>();
        var layers = ReadLayers(model);
        var layersById = layers.ToDictionary(item => item.Id);
        var materials = ReadMaterials(model);
        var namedViews = ReadNamedViews(model);
        var instanceDefinitions = ReadInstanceDefinitions(model);
        var objects = new List<ThreeDmSceneObject>(model.Objects.Count);
        var documentBounds = BoundingBox3d.Invalid;

        foreach (var fileObject in model.Objects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var geometry = fileObject.Geometry;
            var attributes = fileObject.Attributes;
            if (geometry is null || attributes is null)
            {
                diagnostics.Add(new ThreeDmImportDiagnostic(
                    ThreeDmDiagnosticSeverity.Warning,
                    "3DM_OBJECT_MISSING_DATA",
                    "A 3DM object did not expose geometry or attributes and was skipped."));
                continue;
            }

            var layerId = ResolveLayerId(model, attributes.LayerIndex);
            var materialId = ResolveMaterialId(model, attributes.MaterialIndex);
            var sourceVisible = attributes.Visible;
            var layerVisible = IsLayerEffectivelyVisible(layerId, layersById);
            var isVisible = sourceVisible && layerVisible;

            if (!options.IncludeHiddenObjects && !isVisible)
            {
                continue;
            }

            var kind = GetGeometryKind(geometry);
            if (kind == ThreeDmGeometryKind.Unknown)
            {
                diagnostics.Add(new ThreeDmImportDiagnostic(
                    ThreeDmDiagnosticSeverity.Warning,
                    "3DM_UNSUPPORTED_GEOMETRY",
                    $"Geometry type '{geometry.GetType().Name}' is not yet semantically supported.",
                    attributes.ObjectId));
            }

            var bounds = ConvertBounds(geometry.GetBoundingBox(true));
            documentBounds = documentBounds.Union(bounds);

            ThreeDmGeometryData? semanticGeometry = null;
            try
            {
                semanticGeometry = Rhino3dmGeometryConverter.Convert(geometry, options.IncludeRenderMeshes);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                diagnostics.Add(new ThreeDmImportDiagnostic(
                    ThreeDmDiagnosticSeverity.Warning,
                    "3DM_GEOMETRY_CONVERSION_FAILED",
                    $"Geometry type '{geometry.GetType().Name}' could not be converted: {exception.Message}",
                    attributes.ObjectId));
            }

            objects.Add(new ThreeDmSceneObject(
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
            });
        }

        return new ThreeDmSceneDocument(
            Path.GetFullPath(path),
            objects,
            documentBounds,
            diagnostics)
        {
            Properties = new ThreeDmDocumentProperties(
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
                model.Settings.ModelRelativeTolerance),
            Layers = layers,
            Materials = materials,
            NamedViews = namedViews,
            InstanceDefinitions = instanceDefinitions,
        };
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
            var physicallyBased = material.PhysicallyBased;
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
            result.Add(new ThreeDmNamedViewInfo(
                view.Name ?? string.Empty,
                ConvertPoint(viewport.CameraLocation),
                ConvertVector(viewport.CameraDirection),
                ConvertVector(viewport.CameraUp),
                ConvertPoint(viewport.TargetPoint),
                viewport.IsPerspectiveProjection));
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
}
