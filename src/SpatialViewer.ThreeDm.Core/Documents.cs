namespace SpatialViewer.ThreeDm.Core;

public enum ThreeDmGeometryKind
{
    Unknown,
    Point,
    PointCloud,
    Curve,
    Surface,
    Brep,
    Extrusion,
    Mesh,
    SubD,
    Annotation,
    TextDot,
    Hatch,
    InstanceReference,
    Light,
    ClippingPlane,
}

public enum ThreeDmDiagnosticSeverity
{
    Information,
    Warning,
    Error,
}

public sealed record ThreeDmImportDiagnostic(
    ThreeDmDiagnosticSeverity Severity,
    string Code,
    string Message,
    Guid? ObjectId = null);

public sealed record ThreeDmDocumentProperties(
    int ArchiveVersion,
    string? ApplicationName,
    string? ApplicationUrl,
    string? ApplicationDetails,
    string? CreatedBy,
    string? LastEditedBy,
    int Revision,
    string ModelUnitSystem,
    double ModelAbsoluteTolerance,
    double ModelAngleToleranceRadians,
    double ModelRelativeTolerance);

public sealed record ThreeDmLayerInfo(
    Guid Id,
    string Name,
    Guid? ParentLayerId,
    bool IsVisible,
    bool IsLocked,
    uint ColorArgb,
    int LinetypeIndex)
{
    public Guid? RenderMaterialId { get; init; }
}

public sealed record ThreeDmPhysicallyBasedMaterialInfo(
    double BaseColorR,
    double BaseColorG,
    double BaseColorB,
    double BaseColorA,
    double Metallic,
    double Roughness,
    double Alpha,
    double Opacity,
    double Clearcoat,
    double ClearcoatRoughness,
    string Brdf);

public sealed record ThreeDmMaterialTextureInfo(
    string FileName,
    string TextureType,
    bool IsEnabled,
    int MappingChannelId,
    string ProjectionMode,
    string WrapU,
    string WrapV,
    string WrapW,
    double RepeatU,
    double RepeatV,
    double OffsetU,
    double OffsetV,
    double RotationRadians);

public sealed record ThreeDmMaterialInfo(
    Guid Id,
    string Name,
    uint DiffuseColorArgb,
    double Transparency)
{
    public uint? SpecularColorArgb { get; init; }

    public uint? EmissionColorArgb { get; init; }

    public double Shine { get; init; }

    public double Reflectivity { get; init; }

    public ThreeDmPhysicallyBasedMaterialInfo? PhysicallyBased { get; init; }

    public IReadOnlyList<ThreeDmMaterialTextureInfo> Textures { get; init; } =
        Array.Empty<ThreeDmMaterialTextureInfo>();
}

public sealed record ThreeDmViewFrustumInfo(
    double Left,
    double Right,
    double Bottom,
    double Top,
    double Near,
    double Far)
{
    public bool IsValid =>
        double.IsFinite(Left) &&
        double.IsFinite(Right) &&
        double.IsFinite(Bottom) &&
        double.IsFinite(Top) &&
        double.IsFinite(Near) &&
        double.IsFinite(Far) &&
        Left < Right &&
        Bottom < Top &&
        Near > 0 &&
        Near < Far;
}

public sealed record ThreeDmNamedViewInfo(
    string Name,
    Point3d CameraLocation,
    Vector3d CameraDirection,
    Vector3d CameraUp,
    Point3d TargetPoint,
    bool IsPerspectiveProjection)
{
    public double Camera35mmLensLength { get; init; }

    public ThreeDmViewFrustumInfo? Frustum { get; init; }
}

public sealed record ThreeDmSceneObject(
    Guid Id,
    string? Name,
    Guid? LayerId,
    ThreeDmGeometryKind GeometryKind,
    BoundingBox3d Bounds,
    Guid? MaterialId = null,
    bool IsVisible = true,
    uint? ObjectColorArgb = null,
    string? ColorSource = null,
    string? MaterialSource = null,
    ThreeDmGeometryData? Geometry = null)
{
    public bool? SourceObjectVisible { get; init; }
}

public sealed record ThreeDmSceneDocument(
    string SourcePath,
    IReadOnlyList<ThreeDmSceneObject> Objects,
    BoundingBox3d Bounds,
    IReadOnlyList<ThreeDmImportDiagnostic> Diagnostics)
{
    public ThreeDmDocumentProperties? Properties { get; init; }

    public IReadOnlyList<ThreeDmLayerInfo> Layers { get; init; } = Array.Empty<ThreeDmLayerInfo>();

    public IReadOnlyList<ThreeDmMaterialInfo> Materials { get; init; } = Array.Empty<ThreeDmMaterialInfo>();

    public IReadOnlyList<ThreeDmNamedViewInfo> NamedViews { get; init; } = Array.Empty<ThreeDmNamedViewInfo>();

    public IReadOnlyList<ThreeDmInstanceDefinitionInfo> InstanceDefinitions { get; init; } = Array.Empty<ThreeDmInstanceDefinitionInfo>();
}
