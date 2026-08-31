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

**Status: completed in 0.5.0**

Implemented:

- Camera-relative, model-tolerance-relative and explicit absolute chord-tolerance policies.
- Independent Draft / Normal / High quality presets and explicit curve/surface tessellation budgets.
- Analytic Arc/Circle/Ellipse display tessellation using the retained source plane basis; source geometry remains analytic in Core.
- Source-independent rational/non-rational NURBS curve evaluation and adaptive curve tessellation.
- Source-independent tensor-product NURBS surface evaluation, preserving openNURBS superfluous U/V knot boundaries.
- Knot-span-aware adaptive tessellation for standalone untrimmed NURBS surfaces, including indexed triangles, normalized parametric UVs and robust area-weighted normals.
- Mesh quad triangulation while preserving source vertices, normals, UVs, material IDs, object colors and source-object identity.
- Stored Rhino render-mesh reuse for trimmed Breps and Extrusions. Trimmed Brep faces are never incorrectly filled by tessellating the untrimmed underlying surface.
- Exact Brep edge overlays and analytic extrusion wire fallbacks with structured diagnostics when no stored fill mesh exists.
- Recursive nested InstanceDefinition / InstanceReference expansion with composed 4×4 transforms and retained instance paths.
- Render-object caches keyed by source identity, quality, chord tolerance and tessellation budgets, with conservative tolerance bucketing for nearby camera zoom levels.
- Double-precision neutral render data. Conversion to float happens only at the Windows upload boundary, after scene-origin rebasing for large-coordinate stability.

**Regression coverage:** generated tests cover analytic curves, independent NURBS evaluation, rotated analytic planes, mesh triangulation, nested instances, render-mesh fallbacks, adaptive curved surface refinement, cache buckets and large-coordinate upload. A generated Rhino 8 `.3dm` containing a rational NURBS sphere is written, reopened through the public importer and rendered through the neutral surface evaluator/tessellator; generated vertices are checked against the source sphere and normals are validated for unit length/alignment.

**Boundaries:** normalized surface UVs are parametric coordinates, not final material texture mapping; texture/material fidelity belongs to Phase 5. Smooth SubD limit-surface generation remains a future compatible display-mesh path. Phase 4 deliberately keeps the preserved SubD control net instead of introducing a non-Rhino-compatible limit evaluator.

**Exit criteria:** source curves and NURBS remain semantic geometry, display tessellation scales with requested visual tolerance, trimmed Breps do not lose trims, repeated nearby zoom levels can reuse conservative cached geometry, and large coordinates remain double precision until origin-rebased GPU upload.

## Phase 5 — Visual fidelity

**Status: completed in 0.6.0**

Implemented:

- Source-independent color-source resolution for ByLayer, ByObject, ByMaterial and ByParent semantics.
- Material-source resolution for object, layer and parent inheritance, including layer render-material references.
- Parent/child layer visibility inheritance with cycle protection.
- Separate source-object visibility and effective display visibility so layer overrides can reveal already-imported geometry without mutating source semantics.
- Legacy material display properties: diffuse/transparency, specular/emission, shine and reflectivity.
- PBR metadata only when Rhino marks the material as physically based, preserving floating-point base RGBA plus metallic, roughness, alpha, opacity, clearcoat, clearcoat roughness and BRDF.
- Material texture-slot metadata including filename, type, enabled state, mapping channel, projection/wrap, repeat/offset and rotation.
- Backend-neutral appearance attached to mesh, curve and point render primitives after geometry-cache generation.
- `Shaded`, `ShadedWithEdges` and `Wireframe` display policies. Semantic Brep/Extrusion/SubD wires are preferred when available; generic mesh/surface wireframe falls back to deduplicated triangle edges.
- Windows upload contracts for resolved appearance, legacy material parameters, PBR parameters and texture metadata.
- A dedicated visual render-scene builder that reuses Phase 4 geometry/tessellation caches while resolving current layer/material/instance appearance afterward.

**Regression coverage:** generated Core/Rendering tests cover ByLayer, ColorFromMaterial, nested ByParent appearance, ancestor-layer hiding, display modes, Windows appearance/PBR/texture upload and post-import layer re-enable behavior. Real Rhino 8 `.3dm` round-trip tests cover layer render materials, legacy material parameters, physically based material conversion/parameters and texture filename metadata.

**Boundaries:** 0.6.0 preserves material and texture inputs but does not claim final Rhino texture-coordinate mapping, image loading, environment/ground-plane reproduction or Rhino Render/Cycles/RDK shader parity. PBR base color stays floating point through neutral contracts rather than being quantized to 8-bit display color. Smooth SubD limit-surface display remains dependent on a future compatible Rhino display-mesh path.

**Exit criteria:** the viewer can resolve source display colors/materials across layers and nested instances, respect hierarchical visibility, switch useful architectural shaded/wire modes without rebuilding source geometry, and hand resolved legacy/PBR/texture inputs to a Windows renderer without reinterpreting Rhino document tables in the UI layer.

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
