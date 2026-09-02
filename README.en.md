# SpatialViewer.3DMCore

[中文](README.md) · [日本語](README.ja.md) · [English](README.en.md)

Independent Rhino 3DM viewing core for SpatialViewer. This repository owns 3DM reader adapters, Rhino document semantics, 3D geometry/scene translation, rendering abstractions, and regression tests. The WinUI 3 product UI remains in `KiYouJyo/SpatialViewer`.

> Current version: 0.7.0. Phase 6 Performance and Robustness is complete. The core now includes background import with cancellation/progress, progressive post-archive object batches, document/per-geometry safety ceilings, shared instance-mesh payloads, and large-coordinate Windows instanced upload alongside the existing semantic geometry, adaptive derived geometry, and visual-fidelity pipeline. Phase 7 focuses next on the stable SpatialViewer integration contract: open/close/cancel lifecycle, camera fit, layer overrides, selection IDs, and property inspection.

## Design principles

- **UI independent**: parsing, document models, geometry, and scene translation do not depend on WinUI 3 controls.
- **Reader isolation**: Rhino3dm types stay inside the adapter assembly.
- **Semantic geometry first**: source Curve/NURBS/Brep/Extrusion/SubD semantics are preserved; display meshes are derived caches.
- **Double precision first**: model-space geometry, transforms, bounds, and cameras use doubles until the render upload boundary.
- **Instance first**: blocks are represented as definition + transform references rather than eagerly duplicated geometry.
- **Regression driven**: object types, layers/materials/colors, instances, tessellation, and malformed files require fixtures and tests.
- **Independent versioning**: the core and SpatialViewer UI are versioned separately.

See [`docs/ROADMAP.md`](docs/ROADMAP.md), [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md), and [`docs/PERFORMANCE.md`](docs/PERFORMANCE.md).

## License

MIT License. See `THIRD-PARTY-NOTICES.md` for third-party notices.
