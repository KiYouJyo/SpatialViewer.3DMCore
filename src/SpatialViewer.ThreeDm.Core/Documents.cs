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

public sealed record ThreeDmSceneObject(
    Guid Id,
    string? Name,
    Guid? LayerId,
    ThreeDmGeometryKind GeometryKind,
    BoundingBox3d Bounds);

public sealed record ThreeDmSceneDocument(
    string SourcePath,
    IReadOnlyList<ThreeDmSceneObject> Objects,
    BoundingBox3d Bounds,
    IReadOnlyList<ThreeDmImportDiagnostic> Diagnostics);
