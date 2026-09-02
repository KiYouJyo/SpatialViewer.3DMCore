# 3DMCore Architecture / 内核架构 / アーキテクチャ

## Boundary

`SpatialViewer.3DMCore` owns the Rhino 3DM ingestion-to-render-scene pipeline. `SpatialViewer` owns product UI and interaction.

```text
3DM
  -> Reader Adapter (Rhino3dm; replaceable)
  -> 3DM semantic document / progressive object batches
  -> source geometry + attributes + instance graph
  -> ThreeDmSceneDocument (double precision)
  -> tessellation / geometry-cache generation
  -> backend-neutral render geometry
  -> visual fidelity resolution (layer/material/instance/display mode)
  -> expanded render scene OR shared geometry + instance transforms
  -> optional Windows origin-rebased upload projection
  -> SpatialViewer UI / concrete renderer
```

## Dependency direction

- `SpatialViewer.ThreeDm.Core` knows nothing about Rhino3dm, Windows, or UI.
- `SpatialViewer.Formats.ThreeDm` depends only on ThreeDm.Core.
- `SpatialViewer.Formats.ThreeDm.Rhino3dm` depends on the format model + official Rhino3dm package.
- `SpatialViewer.ThreeDm.Rendering` depends only on ThreeDm.Core.
- `SpatialViewer.ThreeDm.Rendering.Windows` depends on the rendering abstraction; a concrete GPU API is intentionally deferred.
- `SpatialViewer` may depend on 3DMCore; 3DMCore must never reference SpatialViewer App/Presentation assemblies.

## Naming rule

This repository deliberately uses `SpatialViewer.ThreeDm.*` assembly and namespace names instead of duplicating `SpatialViewer.Core` or `SpatialViewer.Rendering` from CadCore. That prevents DLL/name collisions when multiple independent format cores are loaded by the same desktop app.

## Geometry rule

Source geometry and display geometry are separate concepts. Curves, NURBS surfaces, Breps, Extrusions and SubD must not be permanently replaced by low-resolution polylines/meshes during import. Tessellation is derived, parameterized, cacheable, and regenerable.

## Visual-fidelity rule

Geometry caching and visual appearance are separate layers. `ThreeDmRenderSceneBuilder` derives cacheable geometry; `ThreeDmVisualRenderSceneBuilder` then resolves current layer visibility, Rhino color/material source rules, nested-instance `ByParent` inheritance and display mode without mutating or re-importing the source geometry.

Resolved render primitives carry backend-neutral `ThreeDmRenderAppearance` data so the UI or concrete renderer does not need to reinterpret Rhino layer/material tables. Legacy material properties, PBR parameters and texture-slot metadata remain source-independent contracts. Texture metadata is input data, not a claim of Rhino Render/Cycles shader parity.

Source object visibility is retained independently from effective layer visibility. This allows a layer-tree override to reveal already-imported geometry while still respecting objects that were explicitly hidden at source.

## Import execution rule

Rhino3dm 8.32 exposes synchronous `File3dm.Read`. The adapter may move that blocking call away from the caller thread, but must not advertise it as a natively asynchronous or streaming archive parser. Cancellation can release the caller while the native read is in flight; semantic conversion must stop as soon as control returns from the reader.

Progressive import begins after archive read/validation. Document tables are emitted first, followed by bounded object batches through a backpressured channel. Full import and progressive import share the same object-conversion path so safety checks and source semantics cannot diverge between APIs.

Document-level and per-geometry resource ceilings are adapter-entry safeguards. Pathological inputs should be rejected before allocating equivalent neutral geometry arrays whenever Rhino exposes the source collection count.

## Instance rule

Rhino instance definitions are represented as a definition graph plus transforms. The importer should avoid eager recursive expansion except for explicitly requested export/debug paths. Render expansion retains instance paths so visual `ByParent` inheritance and downstream selection can resolve the correct instance context.

The compatibility render-scene path may expand instances into world-space primitives. The high-performance path uses `ThreeDmSharedMeshScene`: local mesh payload is deduplicated by source object/subobject while each occurrence retains its own 4×4 transform, `InstancePath`, source identity and resolved appearance. Shared payload must never erase picking or ByParent context.

The Windows shared upload path performs local-origin rebasing once per geometry and scene-origin rebasing through instance transforms. This keeps float buffers/matrices stable for large-coordinate models without duplicating vertex/index buffers per instance.

Detailed execution and baseline policy is documented in [`PERFORMANCE.md`](PERFORMANCE.md).
