namespace SpatialViewer.ThreeDm.Core;

public sealed record ThreeDmImportLimits(
    long MaxFileSizeBytes = 16L * 1024 * 1024 * 1024,
    int MaxObjectCount = 5_000_000,
    int MaxLayerCount = 1_000_000,
    int MaxMaterialCount = 1_000_000,
    int MaxInstanceDefinitionCount = 1_000_000)
{
    public static ThreeDmImportLimits Default { get; } = new();

    public void Validate()
    {
        if (MaxFileSizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxFileSizeBytes));
        }

        if (MaxObjectCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxObjectCount));
        }

        if (MaxLayerCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxLayerCount));
        }

        if (MaxMaterialCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxMaterialCount));
        }

        if (MaxInstanceDefinitionCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxInstanceDefinitionCount));
        }
    }
}

public enum ThreeDmImportStage
{
    ReadingArchive,
    ReadingDocumentTables,
    ConvertingObjects,
    Completed,
}

public sealed record ThreeDmImportProgress(
    ThreeDmImportStage Stage,
    int ProcessedObjects,
    int TotalObjects);

public sealed record ThreeDmImportOptions(
    bool IncludeHiddenObjects = true,
    bool IncludeRenderMeshes = true,
    bool PreserveSourceGeometry = true)
{
    public ThreeDmImportLimits Limits { get; init; } = ThreeDmImportLimits.Default;

    public int ProgressIntervalObjects { get; init; } = 256;

    public int ProgressiveBatchSize { get; init; } = 128;

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Limits);
        Limits.Validate();

        if (ProgressIntervalObjects <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ProgressIntervalObjects));
        }

        if (ProgressiveBatchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ProgressiveBatchSize));
        }
    }
}

public abstract record ThreeDmProgressiveImportUpdate;

public sealed record ThreeDmImportHeaderUpdate(
    string SourcePath,
    ThreeDmDocumentProperties Properties,
    IReadOnlyList<ThreeDmLayerInfo> Layers,
    IReadOnlyList<ThreeDmMaterialInfo> Materials,
    IReadOnlyList<ThreeDmNamedViewInfo> NamedViews,
    IReadOnlyList<ThreeDmInstanceDefinitionInfo> InstanceDefinitions,
    int TotalObjects)
    : ThreeDmProgressiveImportUpdate;

public sealed record ThreeDmImportObjectBatchUpdate(
    IReadOnlyList<ThreeDmSceneObject> Objects,
    BoundingBox3d CumulativeBounds,
    IReadOnlyList<ThreeDmImportDiagnostic> Diagnostics,
    int ProcessedObjects,
    int TotalObjects)
    : ThreeDmProgressiveImportUpdate;

public sealed record ThreeDmImportCompletedUpdate(
    BoundingBox3d Bounds,
    IReadOnlyList<ThreeDmImportDiagnostic> Diagnostics,
    int ImportedObjects,
    int TotalObjects)
    : ThreeDmProgressiveImportUpdate;

public interface IThreeDmImporter
{
    bool CanImport(string path);

    ValueTask<ThreeDmSceneDocument> ImportAsync(
        string path,
        ThreeDmImportOptions? options = null,
        CancellationToken cancellationToken = default);
}

public interface IThreeDmProgressReportingImporter : IThreeDmImporter
{
    ValueTask<ThreeDmSceneDocument> ImportAsync(
        string path,
        ThreeDmImportOptions? options,
        IProgress<ThreeDmImportProgress>? progress,
        CancellationToken cancellationToken = default);
}

public interface IThreeDmProgressiveImporter : IThreeDmProgressReportingImporter
{
    IAsyncEnumerable<ThreeDmProgressiveImportUpdate> ImportProgressivelyAsync(
        string path,
        ThreeDmImportOptions? options = null,
        CancellationToken cancellationToken = default);
}
