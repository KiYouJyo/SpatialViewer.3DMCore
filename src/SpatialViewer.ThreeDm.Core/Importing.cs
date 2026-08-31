namespace SpatialViewer.ThreeDm.Core;

public sealed record ThreeDmImportOptions(
    bool IncludeHiddenObjects = true,
    bool IncludeRenderMeshes = true,
    bool PreserveSourceGeometry = true);

public interface IThreeDmImporter
{
    bool CanImport(string path);

    ValueTask<ThreeDmSceneDocument> ImportAsync(
        string path,
        ThreeDmImportOptions? options = null,
        CancellationToken cancellationToken = default);
}
