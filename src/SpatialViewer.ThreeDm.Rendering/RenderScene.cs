namespace SpatialViewer.ThreeDm.Rendering;

public readonly record struct ThreeDmRenderVertex(float X, float Y, float Z);

public sealed record ThreeDmRenderMesh(
    Guid SourceObjectId,
    IReadOnlyList<ThreeDmRenderVertex> Vertices,
    IReadOnlyList<int> Indices);

public sealed record ThreeDmRenderScene(IReadOnlyList<ThreeDmRenderMesh> Meshes);
