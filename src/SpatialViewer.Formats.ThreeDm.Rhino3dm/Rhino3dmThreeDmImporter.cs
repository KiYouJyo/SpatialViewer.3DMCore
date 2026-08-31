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

        return ValueTask.FromException<ThreeDmSceneDocument>(
            new NotSupportedException(
                "The repository bootstrap is complete, but Rhino3dm document ingestion is scheduled for Roadmap Phase 1."));
    }
}
