namespace SpatialViewer.ThreeDm.Core;

public readonly record struct Plane3d(
    Point3d Origin,
    Vector3d XAxis,
    Vector3d YAxis,
    Vector3d ZAxis);

public readonly record struct Transform3d(
    double M00, double M01, double M02, double M03,
    double M10, double M11, double M12, double M13,
    double M20, double M21, double M22, double M23,
    double M30, double M31, double M32, double M33);

public sealed record ThreeDmBrepVertexData(
    int Index,
    Point3d Position,
    double Tolerance);

public sealed record ThreeDmBrepEdgeData(
    int Index,
    int StartVertexIndex,
    int EndVertexIndex,
    double Tolerance,
    ThreeDmCurveGeometryData Curve);

public sealed record ThreeDmBrepTrimData(
    int Index,
    int FaceIndex,
    int LoopIndex,
    int? EdgeIndex,
    int StartVertexIndex,
    int EndVertexIndex,
    string TrimType,
    string IsoStatus,
    bool IsReversed,
    double ToleranceU,
    double ToleranceV,
    ThreeDmCurveGeometryData? ParameterCurve);

public sealed record ThreeDmBrepLoopData(
    int Index,
    int FaceIndex,
    string LoopType,
    IReadOnlyList<int> TrimIndices);

public sealed record ThreeDmBrepFaceData(
    int Index,
    int SurfaceIndex,
    bool OrientationIsReversed,
    IReadOnlyList<int> LoopIndices,
    ThreeDmNurbsSurfaceGeometryData Surface);

public sealed record ThreeDmBrepGeometryData(
    IReadOnlyList<ThreeDmBrepVertexData> Vertices,
    IReadOnlyList<ThreeDmBrepEdgeData> Edges,
    IReadOnlyList<ThreeDmBrepTrimData> Trims,
    IReadOnlyList<ThreeDmBrepLoopData> Loops,
    IReadOnlyList<ThreeDmBrepFaceData> Faces,
    bool IsSolid,
    BoundingBox3d Bounds)
    : ThreeDmGeometryData(ThreeDmGeometryKind.Brep, Bounds);

public sealed record ThreeDmInstanceReferenceGeometryData(
    Guid InstanceDefinitionId,
    Transform3d Transform,
    BoundingBox3d Bounds)
    : ThreeDmGeometryData(ThreeDmGeometryKind.InstanceReference, Bounds);

public sealed record ThreeDmInstanceDefinitionInfo(
    Guid Id,
    string Name,
    string Description,
    string? SourceArchive,
    string UpdateType,
    string UnitSystem,
    IReadOnlyList<Guid> ObjectIds);

public sealed record ThreeDmSubDVertexData(
    uint Id,
    Point3d ControlNetPoint,
    string Tag);

public sealed record ThreeDmSubDFaceData(
    uint Id,
    IReadOnlyList<uint> VertexIds,
    uint PackId,
    uint? PerFaceColorArgb);

public sealed record ThreeDmSubDGeometryData(
    IReadOnlyList<ThreeDmSubDVertexData> Vertices,
    IReadOnlyList<ThreeDmSubDFaceData> Faces,
    BoundingBox3d Bounds)
    : ThreeDmGeometryData(ThreeDmGeometryKind.SubD, Bounds);

public sealed record ThreeDmTextDotGeometryData(
    string Text,
    string SecondaryText,
    Point3d Position,
    string FontFace,
    int FontHeight,
    BoundingBox3d Bounds)
    : ThreeDmGeometryData(ThreeDmGeometryKind.TextDot, Bounds);

public sealed record ThreeDmAnnotationGeometryData(
    string AnnotationType,
    string PlainText,
    string RichText,
    Guid DimensionStyleId,
    Plane3d Plane,
    double TextHeight,
    double TextRotationRadians,
    BoundingBox3d Bounds)
    : ThreeDmGeometryData(ThreeDmGeometryKind.Annotation, Bounds);

public sealed record ThreeDmHatchGeometryData(
    int PatternIndex,
    double PatternScale,
    double PatternRotationRadians,
    Point3d BasePoint,
    IReadOnlyList<ThreeDmCurveGeometryData> OuterBoundaries,
    IReadOnlyList<ThreeDmCurveGeometryData> InnerBoundaries,
    BoundingBox3d Bounds)
    : ThreeDmGeometryData(ThreeDmGeometryKind.Hatch, Bounds);
