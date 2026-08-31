# Rhino / 3DM Read-Core Roadmap

This roadmap defines the implementation order for SpatialViewer.3DMCore. The goal is faithful viewing first, editing later if ever required.

## Phase 0 — Repository boundary and contracts

**Status: completed in 0.1.0**

- Mirror CadCore repository shape: independent solution, src/tests/docs/CI/fixtures.
- Establish collision-free `SpatialViewer.ThreeDm.*` assemblies.
- Pin stable Rhino3dm dependency behind a replaceable adapter.
- Define double-precision scene/document contracts and import diagnostics.
- Establish fixture naming and regression policy.

**Exit criteria:** repository builds independently; tests execute; no WinUI dependency; no Rhino3dm type leaks outside adapter.

## Phase 1 — 3DM document ingestion

**Status: completed in 0.2.0**

- Read file metadata and archive version.
- Layers: hierarchy, visibility, locking, color, linetype references.
- Object attributes: id/name/layer/material/source visibility.
- Materials and basic render colors/transparency.
- Named views and model units/tolerances.
- Structured diagnostics for unsupported/corrupt data.
- Real `.3dm` write/read regression coverage generated through Rhino3dm.

**Exit criteria:** document semantics are exposed through source-independent contracts and unsupported geometry is reported instead of silently discarded. Broader architectural-file corpus validation continues in later fidelity/performance phases.

## Phase 2 — Fundamental geometry

**Status: completed in 0.3.0**

Implemented source-independent semantic conversion and bounds for:

- Point / PointCloud
- Line / Polyline / PolylineCurve
- Arc / Circle / Ellipse
- NurbsCurve and general Curve via NURBS representation
- Plane / Surface / NurbsSurface via NURBS control net
- Extrusion with path/caps/profiles
- Mesh with vertices/faces/normals/texture coordinates

Curves retain NURBS control points, weights and knots. Surfaces retain their two-dimensional NURBS control net and knot vectors. Mesh topology is copied without leaking Rhino3dm types, and Extrusion remains an extrusion rather than being permanently replaced by a mesh or Brep.

**Key rule:** curves remain curves; surfaces remain surfaces. Preview tessellation is derived data.

**Regression coverage:** a generated Rhino 8 `.3dm` containing PointCloud, Line, Polyline, Circle, PlaneSurface, capped Extrusion and quad Mesh with UVs is written to disk, reopened through the public importer and asserted against the source-independent geometry contracts.

## Phase 3 — Brep, trims, instances and advanced objects

**Status: completed in 0.4.0**

Implemented source-independent viewing semantics for:

- Brep vertices, edges, trims, loops and faces, including 3D edge curves, trim parameter-space curves, face NURBS surfaces, topology indices, orientation and tolerances.
- InstanceDefinition member-object graphs and InstanceReference 4×4 transforms, including nested definitions/references.
- TextDot and annotation text metadata, annotation planes/styles and leader 3D point chains.
- Hatch pattern metadata plus outer/inner semantic boundary curves.
- SubD control-net vertices/faces and per-face color data where exposed by Rhino3dm.
- Light viewing metadata: style, enabled state, position, direction, color and supported intensity/spot properties.
- ClippingPlane surface, viewport targets, participation lists and clipping-depth metadata.

**Regression coverage:** a generated Rhino 8 `.3dm` containing a solid Brep box, TextDot, text annotation, leader, hatch and a two-level nested instance-definition/reference graph is written, reopened through the public importer and asserted against the source-independent Phase 3 contracts. The Rhino3dm 8.32 writer cannot create every advanced component used by the reader: generic File3dm object insertion does not create Light components, and a synthetic vertex-only SubD is not a valid serializable SubD. Those readers remain implemented and compile-validated; real Rhino-produced SubD/Light/ClippingPlane fixtures should be added to the fixture corpus as they become available.

**Exit criteria:** topology and instance hierarchy required by the viewer are preserved without flattening to render meshes. Broader side-by-side validation against real architectural/product Rhino models continues in the visual-fidelity and robustness phases.

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
