namespace SpatialViewer.ThreeDm.Rendering;

public readonly record struct ThreeDmRenderVertex(double X, double Y, double Z);

public readonly record struct ThreeDmRenderNormal(double X, double Y, double Z);

public readonly record struct ThreeDmRenderTextureCoordinate(double U, double V);

public enum ThreeDmRenderCurveKind
{
    Line,
    Polyline,
    Arc,
    Circle,
    Ellipse,
    Nurbs,
    Other,
}

public sealed record ThreeDmRenderPointSet(
    Guid SourceObjectId,
    IReadOnlyList<ThreeDmRenderVertex> Points);

public sealed record ThreeDmRenderCurve(
    Guid SourceObjectId,
    ThreeDmRenderCurveKind Kind,
    IReadOnlyList<ThreeDmRenderVertex> Points,
    bool IsClosed,
    double TargetChordTolerance,
    int? SourceSubobjectIndex = null);

public sealed record ThreeDmRenderMesh(
    Guid SourceObjectId,
    IReadOnlyList<ThreeDmRenderVertex> Vertices,
    IReadOnlyList<int> Indices)
{
    public IReadOnlyList<ThreeDmRenderNormal> Normals { get; init; } = Array.Empty<ThreeDmRenderNormal>();

    public IReadOnlyList<ThreeDmRenderTextureCoordinate> TextureCoordinates { get; init; } = Array.Empty<ThreeDmRenderTextureCoordinate>();

    public Guid? MaterialId { get; init; }

    public uint? ColorArgb { get; init; }
}

public sealed record ThreeDmRenderDiagnostic(
    Guid? SourceObjectId,
    string Code,
    string Message);

public sealed record ThreeDmRenderScene(IReadOnlyList<ThreeDmRenderMesh> Meshes)
{
    public IReadOnlyList<ThreeDmRenderPointSet> PointSets { get; init; } = Array.Empty<ThreeDmRenderPointSet>();

    public IReadOnlyList<ThreeDmRenderCurve> Curves { get; init; } = Array.Empty<ThreeDmRenderCurve>();

    public IReadOnlyList<ThreeDmRenderDiagnostic> Diagnostics { get; init; } = Array.Empty<ThreeDmRenderDiagnostic>();
}
