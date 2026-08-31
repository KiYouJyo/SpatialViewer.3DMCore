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
    int LinetypeIndex);

public sealed record ThreeDmMaterialInfo(
    Guid Id,
    string Name,
    uint DiffuseColorArgb,
    double Transparency);

public sealed record ThreeDmNamedViewInfo(
    string Name,
    Point3d CameraLocation,
    Vector3d CameraDirection,
    Vector3d CameraUp,
    Point3d TargetPoint,
    bool IsPerspectiveProjection);

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
    ThreeDmGeometryData? Geometry = null);

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
