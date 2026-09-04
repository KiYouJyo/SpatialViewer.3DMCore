using SpatialViewer.ThreeDm.Core;
using SpatialViewer.ThreeDm.Rendering;

namespace SpatialViewer.ThreeDm.Integration;

public enum ThreeDmSessionState
{
    Closed,
    Opening,
    Open,
    Closing,
    Faulted,
}

public sealed class ThreeDmSession : IAsyncDisposable
{
    private readonly object _sync = new();
    private readonly IThreeDmImporter _importer;
    private readonly ThreeDmVisualRenderSceneBuilder _visualBuilder = new();
    private readonly ThreeDmSharedMeshSceneBuilder _sharedBuilder = new();
    private readonly ThreeDmLayerVisibilityOverrides _layerOverrides = new();
    private CancellationTokenSource? _openCancellation;
    private Task<ThreeDmSceneDocument>? _openOperation;
    private ThreeDmSceneDocument? _document;
    private Exception? _lastError;
    private ThreeDmSessionState _state = ThreeDmSessionState.Closed;
    private string? _sourcePath;

    public ThreeDmSession(IThreeDmImporter importer)
    {
        ArgumentNullException.ThrowIfNull(importer);
        _importer = importer;
    }

    public string? SourcePath
    {
        get
        {
            lock (_sync)
            {
                return _sourcePath;
            }
        }
    }

    public bool CanOpen(string path) =>
        !string.IsNullOrWhiteSpace(path) && _importer.CanImport(path);

    public ThreeDmSessionState State
    {
        get
        {
            lock (_sync)
            {
                return _state;
            }
        }
    }

    public ThreeDmSceneDocument? Document
    {
        get
        {
            lock (_sync)
            {
                return _document;
            }
        }
    }

    public Exception? LastError
    {
        get
        {
            lock (_sync)
            {
                return _lastError;
            }
        }
    }

    public BoundingBox3d? ModelBounds
    {
        get
        {
            lock (_sync)
            {
                return _document?.Bounds;
            }
        }
    }

    public IReadOnlyDictionary<Guid, bool> LayerVisibilityOverrides
    {
        get
        {
            lock (_sync)
            {
                return _layerOverrides.Snapshot;
            }
        }
    }

    public async Task<ThreeDmSceneDocument> OpenAsync(
        string path,
        ThreeDmImportOptions? options = null,
        IProgress<ThreeDmImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!_importer.CanImport(path))
        {
            throw new NotSupportedException($"The configured 3DM importer cannot open '{path}'.");
        }

        Task<ThreeDmSceneDocument> operation;
        CancellationTokenSource lifetime;
        lock (_sync)
        {
            if (_state is not ThreeDmSessionState.Closed and not ThreeDmSessionState.Faulted)
            {
                throw new InvalidOperationException($"Cannot open a 3DM document while the session is {_state}.");
            }

            _document = null;
            _lastError = null;
            _sourcePath = path;
            _layerOverrides.Clear();
            _visualBuilder.ClearCache();
            _sharedBuilder.ClearCache();
            _state = ThreeDmSessionState.Opening;
            lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _openCancellation = lifetime;
            operation = ImportDocumentAsync(path, options, progress, lifetime.Token);
            _openOperation = operation;
        }

        try
        {
            var document = await operation.ConfigureAwait(false);
            lock (_sync)
            {
                if (!ReferenceEquals(_openOperation, operation) || _state != ThreeDmSessionState.Opening)
                {
                    throw new OperationCanceledException("The 3DM open operation was superseded by session close/cancel.");
                }

                _document = document;
                _state = ThreeDmSessionState.Open;
                _openOperation = null;
                _openCancellation?.Dispose();
                _openCancellation = null;
                return document;
            }
        }
        catch (OperationCanceledException)
        {
            lock (_sync)
            {
                if (ReferenceEquals(_openOperation, operation) && _state != ThreeDmSessionState.Closing)
                {
                    _document = null;
                    _sourcePath = null;
                    _openOperation = null;
                    _openCancellation?.Dispose();
                    _openCancellation = null;
                    _state = ThreeDmSessionState.Closed;
                }
            }

            throw;
        }
        catch (Exception exception)
        {
            lock (_sync)
            {
                if (ReferenceEquals(_openOperation, operation) && _state != ThreeDmSessionState.Closing)
                {
                    _document = null;
                    _lastError = exception;
                    _openOperation = null;
                    _openCancellation?.Dispose();
                    _openCancellation = null;
                    _state = ThreeDmSessionState.Faulted;
                }
            }

            throw;
        }
    }

    public bool CancelOpen()
    {
        lock (_sync)
        {
            if (_state != ThreeDmSessionState.Opening || _openCancellation is null)
            {
                return false;
            }

            _openCancellation.Cancel();
            return true;
        }
    }

    public async Task CloseAsync()
    {
        Task<ThreeDmSceneDocument>? operation;
        CancellationTokenSource? cancellation;
        lock (_sync)
        {
            if (_state == ThreeDmSessionState.Closed)
            {
                return;
            }

            _state = ThreeDmSessionState.Closing;
            operation = _openOperation;
            cancellation = _openCancellation;
            cancellation?.Cancel();
        }

        if (operation is not null)
        {
            try
            {
                _ = await operation.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                // Close owns cleanup; an in-flight open fault must not prevent closing the session.
            }
        }

        lock (_sync)
        {
            cancellation?.Dispose();
            if (ReferenceEquals(_openCancellation, cancellation))
            {
                _openCancellation = null;
            }

            _openOperation = null;
            _document = null;
            _lastError = null;
            _sourcePath = null;
            _layerOverrides.Clear();
            _visualBuilder.ClearCache();
            _sharedBuilder.ClearCache();
            _state = ThreeDmSessionState.Closed;
        }
    }

    public IReadOnlyList<ThreeDmLayerNode> GetLayerTree()
    {
        var document = RequireOpenDocument();
        lock (_sync)
        {
            return ThreeDmLayerTreeBuilder.Build(document, _layerOverrides);
        }
    }

    public void SetLayerVisibility(Guid layerId, bool? visible)
    {
        var document = RequireOpenDocument();
        if (!document.Layers.Any(layer => layer.Id == layerId))
        {
            throw new KeyNotFoundException($"Layer '{layerId}' does not exist in the open 3DM document.");
        }

        lock (_sync)
        {
            EnsureOpen();
            _layerOverrides.Set(layerId, visible);
        }
    }

    public ThreeDmSceneDocument GetDisplayDocument()
    {
        var document = RequireOpenDocument();
        lock (_sync)
        {
            return ThreeDmLayerTreeBuilder.ApplyOverrides(document, _layerOverrides);
        }
    }

    public ThreeDmRenderScene BuildVisualScene(ThreeDmVisualRenderSettings? settings = null) =>
        _visualBuilder.Build(GetDisplayDocument(), settings);

    public ThreeDmSharedMeshScene BuildSharedMeshScene(ThreeDmTessellationSettings? settings = null) =>
        _sharedBuilder.Build(GetDisplayDocument(), settings);

    public ThreeDmCameraFit GetCameraFit(ThreeDmCameraFitOptions? options = null) =>
        ThreeDmCameraFitCalculator.Calculate(RequireOpenDocument().Bounds, options);

    public IReadOnlyList<ThreeDmSelectionId> GetSelectionIds(ThreeDmRenderScene scene)
    {
        _ = RequireOpenDocument();
        return ThreeDmSelectionCatalog.Create(scene);
    }

    public IReadOnlyList<ThreeDmSelectionId> GetSelectionIds(ThreeDmSharedMeshScene scene)
    {
        _ = RequireOpenDocument();
        return ThreeDmSelectionCatalog.Create(scene);
    }

    public ThreeDmSelectionProperties? GetSelectionProperties(ThreeDmSelectionId selectionId)
    {
        var document = RequireOpenDocument();
        lock (_sync)
        {
            return ThreeDmSelectionCatalog.Resolve(document, selectionId, _layerOverrides);
        }
    }

    public async ValueTask DisposeAsync() => await CloseAsync().ConfigureAwait(false);

    private async Task<ThreeDmSceneDocument> ImportDocumentAsync(
        string path,
        ThreeDmImportOptions? options,
        IProgress<ThreeDmImportProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (_importer is IThreeDmProgressReportingImporter reportingImporter)
        {
            return await reportingImporter.ImportAsync(path, options, progress, cancellationToken).ConfigureAwait(false);
        }

        return await _importer.ImportAsync(path, options, cancellationToken).ConfigureAwait(false);
    }

    private ThreeDmSceneDocument RequireOpenDocument()
    {
        lock (_sync)
        {
            EnsureOpen();
            return _document!;
        }
    }

    private void EnsureOpen()
    {
        if (_state != ThreeDmSessionState.Open || _document is null)
        {
            throw new InvalidOperationException("A 3DM document must be open for this operation.");
        }
    }
}
