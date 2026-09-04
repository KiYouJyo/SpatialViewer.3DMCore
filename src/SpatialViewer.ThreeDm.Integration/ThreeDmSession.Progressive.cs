using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Integration;

public sealed partial class ThreeDmSession
{
    public bool SupportsProgressiveOpen => _importer is IThreeDmProgressiveImporter;

    public async Task<ThreeDmSceneDocument> OpenProgressivelyAsync(
        string path,
        Func<ThreeDmProgressiveImportUpdate, CancellationToken, ValueTask> onUpdate,
        ThreeDmImportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(onUpdate);
        if (_importer is not IThreeDmProgressiveImporter progressiveImporter)
        {
            throw new NotSupportedException("The configured 3DM importer does not support progressive opening.");
        }

        if (!progressiveImporter.CanImport(path))
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
            _preparedBuilder.ClearCache();
            _state = ThreeDmSessionState.Opening;
            lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _openCancellation = lifetime;
            operation = ImportProgressivelyAsync(
                progressiveImporter,
                path,
                options,
                onUpdate,
                lifetime.Token);
            _openOperation = operation;
        }

        try
        {
            var document = await operation.ConfigureAwait(false);
            lock (_sync)
            {
                if (!ReferenceEquals(_openOperation, operation) || _state != ThreeDmSessionState.Opening)
                {
                    throw new OperationCanceledException("The progressive 3DM open operation was superseded by session close/cancel.");
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

    private static async Task<ThreeDmSceneDocument> ImportProgressivelyAsync(
        IThreeDmProgressiveImporter importer,
        string path,
        ThreeDmImportOptions? options,
        Func<ThreeDmProgressiveImportUpdate, CancellationToken, ValueTask> onUpdate,
        CancellationToken cancellationToken)
    {
        ThreeDmImportHeaderUpdate? header = null;
        ThreeDmImportCompletedUpdate? completed = null;
        var objects = new List<ThreeDmSceneObject>();

        await foreach (var update in importer.ImportProgressivelyAsync(path, options, cancellationToken)
                           .WithCancellation(cancellationToken)
                           .ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (update)
            {
                case ThreeDmImportHeaderUpdate value:
                    if (header is not null || objects.Count != 0 || completed is not null)
                    {
                        throw new InvalidDataException("Progressive 3DM import emitted an out-of-order header.");
                    }

                    header = value;
                    break;

                case ThreeDmImportObjectBatchUpdate value:
                    if (header is null || completed is not null)
                    {
                        throw new InvalidDataException("Progressive 3DM import emitted an object batch outside the active object phase.");
                    }

                    objects.AddRange(value.Objects);
                    break;

                case ThreeDmImportCompletedUpdate value:
                    if (header is null || completed is not null)
                    {
                        throw new InvalidDataException("Progressive 3DM import emitted an invalid completion update.");
                    }

                    completed = value;
                    break;

                default:
                    throw new InvalidDataException($"Unknown progressive 3DM update type '{update.GetType().Name}'.");
            }

            await onUpdate(update, cancellationToken).ConfigureAwait(false);
        }

        if (header is null || completed is null)
        {
            throw new InvalidDataException("Progressive 3DM import ended without both header and completion updates.");
        }

        if (completed.ImportedObjects != objects.Count)
        {
            throw new InvalidDataException(
                $"Progressive 3DM import object-count mismatch: updates={objects.Count}; completion={completed.ImportedObjects}.");
        }

        return new ThreeDmSceneDocument(
            header.SourcePath,
            objects.ToArray(),
            completed.Bounds,
            completed.Diagnostics)
        {
            Properties = header.Properties,
            Layers = header.Layers,
            Materials = header.Materials,
            NamedViews = header.NamedViews,
            InstanceDefinitions = header.InstanceDefinitions,
        };
    }
}
