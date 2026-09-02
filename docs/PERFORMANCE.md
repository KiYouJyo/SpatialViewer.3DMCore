# Performance and robustness / 性能与鲁棒性 / パフォーマンスと堅牢性

Phase 6 (`0.7.0`) defines the large-model execution contract for `SpatialViewer.3DMCore`. The goal is predictable memory growth, responsive cancellation and reusable geometry, without pretending that the pinned Rhino3dm reader exposes streaming primitives that it does not provide.

## Archive-read boundary

Rhino3dm 8.32 exposes synchronous `File3dm.Read(...)`; it does not expose a native `ReadAsync` or object-by-object archive parser through the API used by this repository.

`Rhino3dmThreeDmImporter.ImportAsync` therefore runs the blocking archive read away from the caller thread. Cancellation observed by the caller does not make the native `File3dm.Read` operation interruptible. If cancellation arrives while that native call is running, the caller is released immediately; the worker checks the token as soon as Rhino3dm returns, disposes the model and does not continue semantic conversion.

This is an asynchronous application boundary around a synchronous third-party reader, not a claim that the `.3dm` archive itself is decoded incrementally.

## Progressive availability

`IThreeDmProgressiveImporter` starts producing updates after the archive has been read and validated:

1. `ThreeDmImportHeaderUpdate` publishes document properties, layers, materials, named views and instance definitions.
2. `ThreeDmImportObjectBatchUpdate` publishes bounded batches of source-independent scene objects plus cumulative bounds and diagnostics.
3. `ThreeDmImportCompletedUpdate` publishes final counts, bounds and diagnostics.

The producer and consumer are connected by a bounded channel. Backpressure prevents a slow UI/renderer from allowing an unbounded queue of converted object batches to accumulate in memory.

`ProgressiveBatchSize` is configurable. CI baselines exercise 8, 128 and 1024-object generated fixtures with a 32-object batch ceiling and assert that batch size remains bounded independently of total object count.

## Defensive import limits

`ThreeDmImportLimits` rejects oversized inputs before or immediately after the Rhino archive read:

- file size;
- total object count;
- layer count;
- material count;
- instance-definition count.

`ThreeDmGeometryLimits` adds per-object limits before neutral geometry arrays are copied:

- PointCloud point count;
- Mesh vertex and face counts;
- aggregate Brep topology items;
- SubD vertex and face counts;
- NURBS control-point counts;
- Polyline point counts;
- embedded Brep/Extrusion render-mesh vertex and face counts when render meshes are requested.

Limits are deliberately configurable. Defaults are safety ceilings, not recommendations for normal model size.

## Shared instance geometry

The compatibility `ThreeDmRenderSceneBuilder.Build()` path continues to return world-expanded render primitives.

For large block-heavy models, `ThreeDmSharedMeshSceneBuilder` provides an opt-in instanced path:

- one `ThreeDmSharedMeshGeometry` payload stores vertices, indices, normals and UVs for each unique source/subobject mesh;
- one `ThreeDmSharedMeshInstance` stores source identity, subobject identity, instance path, appearance and a 4×4 transform for each occurrence;
- nested `ByParent` appearance remains instance-specific;
- picking identity is not deduplicated away;
- source tessellation remains backed by the Phase 4 cache.

`ThreeDmSharedMeshSceneStatistics` reports unique versus equivalent expanded vertex/index counts. The deterministic regression baseline for 100 instances of one triangle is 3 unique vertices versus 300 expanded vertices and 3 unique indices versus 300 expanded indices.

## Windows upload precision

`WindowsThreeDmSharedUploadProjection` projects shared geometry without re-expanding it per instance.

Two origins are used:

- a local origin per shared geometry keeps uploaded float vertex buffers small;
- a scene origin keeps instance matrices numerically small for large-coordinate models.

The per-instance upload matrix restores the local geometry origin, applies the model-space instance transform, then rebases by the scene origin. Geometry payload is converted to float once; instance identity and appearance stay separate.

## Performance baselines

CI intentionally avoids hard wall-clock millisecond thresholds on hosted runners because machine scheduling and image changes make those thresholds noisy and misleading.

The repository uses deterministic structural baselines in CI:

- bounded progressive batch sizes across small/medium/large generated fixtures;
- unique versus expanded shared-mesh payload counts;
- tessellation cache reuse tests from Phase 4;
- large-coordinate float-rebasing tests from Phases 4 and 6.

Wall-clock time, peak working set and allocation measurements should be collected on a stable local machine against a maintained real-world Rhino corpus. Those measurements are observational baselines, not cross-runner correctness gates.

## Phase 6 boundary

Phase 6 does not turn Rhino3dm into a streaming archive reader, does not introduce WinUI dependencies, and does not change the semantic source geometry model. Phase 7 owns the final SpatialViewer open/close/cancel/session integration contract.
