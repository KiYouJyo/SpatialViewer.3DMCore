# Rhino / 3DM Read-Core Roadmap

This roadmap defines the implementation order for SpatialViewer.3DMCore. The goal is faithful viewing first, editing later if ever required.

## Phase 0 — Repository boundary and contracts

**Status: bootstrap in progress**

- Mirror CadCore repository shape: independent solution, src/tests/docs/CI/fixtures.
- Establish collision-free `SpatialViewer.ThreeDm.*` assemblies.
- Pin stable Rhino3dm dependency behind a replaceable adapter.
- Define double-precision scene/document contracts and import diagnostics.
- Establish fixture naming and regression policy.

**Exit criteria:** repository builds independently; tests execute; no WinUI dependency; no Rhino3dm type leaks outside adapter.

## Phase 1 — 3DM document ingestion

- Read file metadata and archive version.
- Layers: hierarchy, visibility, locking, color, linetype references.
- Object attributes: id/name/layer/material/source visibility.
- Materials and basic render colors/transparency.
- Named views and model units/tolerances.
- Structured diagnostics for unsupported/corrupt data.

**Exit criteria:** a real architectural 3DM can be opened and inspected without geometry loss being silently ignored.

## Phase 2 — Fundamental geometry

Implement semantic conversion and bounds for:

- Point / PointCloud
- Line / Polyline / PolylineCurve
- Arc / Circle / Ellipse
- NurbsCurve and general Curve
- Plane / Surface / NurbsSurface
- Extrusion
- Mesh

**Key rule:** curves remain curves; surfaces remain surfaces. Preview tessellation is derived data.

## Phase 3 — Brep, trims, instances and advanced objects

- Brep faces, loops, trims and edge topology.
- InstanceDefinition + InstanceReference graph with nested transforms.
- Annotation/TextDot/leader metadata needed for viewing.
- Hatch where readable through the adapter.
- SubD source representation and/or supported render-mesh path.
- Lights and clipping/visibility metadata where useful to viewing.

**Exit criteria:** common building/product Rhino models preserve hierarchy and visible object counts closely enough for side-by-side comparison with Rhino.

## Phase 4 — Adaptive tessellation and render scene

- Camera-relative or tolerance-relative tessellation policy.
- Independent quality presets: draft / normal / high.
- Preserve analytic curves for crisp line rendering.
- Generate normals, UVs, material slots, edge overlays and object IDs.
- Cache meshes by geometry identity + tessellation settings.
- Avoid converting the whole document to float until render upload.

**Exit criteria:** circles and NURBS do not visibly facet at normal zoom; large models stay interactive.

## Phase 5 — Visual fidelity

- By-layer / by-object color resolution.
- Material transparency and basic PBR-compatible properties where available.
- Layer visibility inheritance.
- Edge/wire modes suitable for architectural Rhino files.
- Ground-plane/environment/render-content metadata only where it materially improves viewing.

## Phase 6 — Performance and robustness

- Background parse with cancellation.
- Progressive scene availability for large files.
- Instance deduplication and shared mesh buffers.
- Defensive limits for malformed files and pathological geometry.
- Memory/performance baselines at small, medium, and large fixture sizes.

## Phase 7 — SpatialViewer integration contract

- Stable package/version boundary.
- Open/close/cancel lifecycle.
- Camera fit and model bounds.
- Layer tree, visibility overrides, selection IDs and property inspection.
- No direct UI dependency in this repository.

## Fixture matrix

Maintain small redistributable fixtures for each semantic feature and a separate non-redistributable local corpus for real-world regression. Minimum fixture groups:

1. primitives and analytic curves;
2. NURBS curves/surfaces;
3. trimmed Breps;
4. extrusions;
5. meshes with normals/UVs/materials;
6. nested blocks/instances;
7. layers + object colors/materials;
8. SubD;
9. annotations;
10. malformed/partial files and multiple 3DM archive versions.

## Non-goals for the initial read core

- Rhino command execution.
- Grasshopper definition evaluation.
- Rhino plug-in object reconstruction that requires proprietary plug-ins.
- Full material/render-engine parity with Rhino Render/Cycles.
- Editing or writing 3DM until the viewer read path is mature.
