# SpatialViewer.3DMCore 1.0 completeness

Version 1.0.0 marks the reusable Rhino/3DM read-and-view core as complete for SpatialViewer product integration.

## 1.0 guarantees

- .3dm archive ingestion through a replaceable Rhino3dm adapter with source-independent contracts.
- Document properties, units/tolerances, layers, materials, named views, instances and structured diagnostics.
- Semantic Point/PointCloud/Curve/NURBS/Surface/Brep/Extrusion/Mesh/SubD/Annotation/TextDot/Hatch/Light/ClippingPlane data where exposed by the reader.
- Trim-aware Brep semantics, nested Block identity and double-precision transforms.
- Adaptive display tessellation, embedded Rhino render-mesh reuse, geometry caching and large-coordinate rebasing.
- ByLayer/ByObject/ByMaterial/ByParent appearance, PBR/texture metadata and Shaded/ShadedWithEdges/Wireframe policy inputs.
- Background and progressive application-level loading with cancellation, progress, safety ceilings and bounded batching.
- Shared mesh instances for Block-heavy models without losing source/subobject/instance-path identity.
- Stable product host API for lifecycle, layer overrides, selection, inspection, camera presets and semantic overlays.
- Product-ready neutral render package plus Windows upload projection with triangle/wire indices.
- Independently versioned and verifiable win-x64 kernel package.

## Intentionally outside this repository

The following are product/backend work and must not be pulled into 3DMCore merely to finish application integration:

- Direct3D device/shader/swap-chain ownership.
- WinUI 3 controls, tabs, toolbars, property panes and window lifetime.
- Orbit/Pan/Zoom input handling and keyboard/mouse gestures.
- GPU ID-picking render pass and selection highlighting.
- Texture file loading/caching and final shader implementation.
- Application update UX and kernel activation UI.

## Reader-dependent limits

The core does not claim behavior the pinned reader cannot provide. In particular, the offline Rhino3dm 8.32 path preserves SubD control-net semantics but does not synthesize Rhino's smooth limit-surface display mesh. Real Rhino-produced advanced-object files may be validated in an external/non-redistributable corpus when the pinned File3dm writer cannot create those objects.

## Explicit non-goals

- Rhino command execution or editing.
- Grasshopper definition evaluation.
- Reconstruction of proprietary plug-in custom objects without their plug-ins.
- Pixel-identical Rhino Render/Cycles/RDK environment and material-engine parity.
- Writing/editing 3DM as part of the initial viewer core.

With these boundaries, further work required to show Rhino models inside SpatialViewer belongs to product integration rather than unfinished read-core functionality.
