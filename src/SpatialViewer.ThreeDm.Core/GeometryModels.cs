namespace SpatialViewer.ThreeDm.Core;

public abstract record ThreeDmGeometryData(
    ThreeDmGeometryKind Kind,
    BoundingBox3d Bounds);

public sealed record ThreeDmWeightedPoint3d(Point3d Position, double Weight);

public sealed record ThreeDmPointGeometryData(
    Point3d Position,
    BoundingBox3d Bounds)
    : ThreeDmGeometryData(ThreeDmGeometryKind.Point, Bounds);

public sealed record ThreeDmPointCloudGeometryData(
    IReadOnlyList<Point3d> Points,
    BoundingBox3d Bounds)
    : ThreeDmGeometryData(ThreeDmGeometryKind.PointCloud, Bounds);

public enum ThreeDmCurveForm
{
    Other,
    Line,
    Polyline,
    Arc,
    Circle,
    Ellipse,
    Nurbs,
}

public sealed record ThreeDmArcGeometryData(
    Plane3d Plane,
    double Radius,
    double StartAngleRadians,
    double EndAngleRadians)
{
    public Point3d Center => Plane.Origin;

    public Vector3d Normal => Plane.ZAxis;
}

public sealed record ThreeDmEllipseGeometryData(
    Plane3d Plane,
    double Radius1,
    double Radius2)
{
    public Point3d Center => Plane.Origin;

    public Vector3d Normal => Plane.ZAxis;
}

public sealed record ThreeDmNurbsCurveData(
    int Degree,
    bool IsRational,
    bool IsClosed,
    bool IsPeriodic,
    IReadOnlyList<ThreeDmWeightedPoint3d> ControlPoints,
    IReadOnlyList<double> Knots,
    double StartSuperfluousKnot,
    double EndSuperfluousKnot);

public sealed record ThreeDmCurveGeometryData(
    ThreeDmCurveForm Form,
    ThreeDmNurbsCurveData Nurbs,
    IReadOnlyList<Point3d> PolylinePoints,
    ThreeDmArcGeometryData? Arc,
    ThreeDmEllipseGeometryData? Ellipse,
    BoundingBox3d Bounds)
    : ThreeDmGeometryData(ThreeDmGeometryKind.Curve, Bounds);

public sealed record ThreeDmNurbsSurfaceGeometryData(
    int DegreeU,
    int DegreeV,
    int ControlPointCountU,
    int ControlPointCountV,
    bool IsRational,
    bool IsClosedU,
    bool IsClosedV,
    bool IsPeriodicU,
    bool IsPeriodicV,
    IReadOnlyList<ThreeDmWeightedPoint3d> ControlPoints,
    IReadOnlyList<double> KnotsU,
    IReadOnlyList<double> KnotsV,
    double StartSuperfluousKnotU,
    double EndSuperfluousKnotU,
    double StartSuperfluousKnotV,
    double EndSuperfluousKnotV,
    BoundingBox3d Bounds)
    : ThreeDmGeometryData(ThreeDmGeometryKind.Surface, Bounds);

public sealed record ThreeDmExtrusionGeometryData(
    Point3d PathStart,
    Point3d PathEnd,
    Vector3d PathTangent,
    bool IsSolid,
    bool IsCappedAtBottom,
    bool IsCappedAtTop,
    IReadOnlyList<ThreeDmCurveGeometryData> Profiles,
    BoundingBox3d Bounds)
    : ThreeDmGeometryData(ThreeDmGeometryKind.Extrusion, Bounds)
{
    public IReadOnlyList<ThreeDmEmbeddedRenderMeshData> RenderMeshes { get; init; } =
        Array.Empty<ThreeDmEmbeddedRenderMeshData>();
}

public sealed record ThreeDmMeshFace(int A, int B, int C, int? D = null);

public sealed record ThreeDmTextureCoordinate(double U, double V);

public sealed record ThreeDmMeshGeometryData(
    IReadOnlyList<Point3d> Vertices,
    IReadOnlyList<ThreeDmMeshFace> Faces,
    IReadOnlyList<Vector3d> Normals,
    IReadOnlyList<ThreeDmTextureCoordinate> TextureCoordinates,
    bool IsClosed,
    BoundingBox3d Bounds)
    : ThreeDmGeometryData(ThreeDmGeometryKind.Mesh, Bounds);
