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
- Light viewing metadata and ClippingPlane viewing semantics supported by the pinned reader.

**Regression coverage:** generated Rhino 8 `.3dm` fixtures cover a solid Brep box, TextDot, text annotation, leader, hatch and a two-level nested instance graph. Rhino3dm 8.32 cannot synthesize every advanced fixture required by the reader; real Rhino-produced SubD/Light/ClippingPlane samples remain fixture-corpus follow-up work.

**Exit criteria:** topology and instance hierarchy required by the viewer are preserved without flattening to render meshes.

## Phase 4 — Adaptive tessellation and render scene

**Status: completed in 0.5.0**

Implemented:

- Camera-relative, model-tolerance-relative and explicit absolute chord-tolerance policies.
- Independent Draft / Normal / High quality presets and curve/surface budgets.
- Analytic Arc/Circle/Ellipse display tessellation using retained source plane bases.
- Independent rational/non-rational NURBS curve and tensor-product surface evaluation.
- Knot-span-aware adaptive standalone-surface tessellation with indexed triangles, parametric UVs and robust normals.
- Mesh triangulation while preserving source vertices, normals, UVs, material IDs, colors and source identity.
- Stored Rhino render-mesh reuse for trimmed Breps and Extrusions; trimmed Breps are never filled by ignoring trims.
- Exact Brep edge overlays and analytic extrusion wire fallbacks when fill meshes are absent.
- Recursive nested instance expansion with composed transforms and retained instance paths.
- Tessellation caches with conservative camera-relative tolerance buckets.
- Double-precision neutral render data and Windows origin rebasing before float upload.

**Regression coverage:** analytic curves, independent NURBS evaluation, rotated planes, mesh triangulation, nested instances, render-mesh fallbacks, adaptive curved surfaces, cache buckets, large coordinates and a generated rational NURBS sphere round trip.

**Boundaries:** normalized surface UVs are parametric coordinates, not final material texture mapping. Smooth SubD limit-surface generation remains a future compatible display-mesh path.

**Exit criteria:** source geometry remains semantic, tessellation scales with requested visual tolerance, trimmed Breps keep trims, nearby zoom levels reuse geometry, and large coordinates stay double precision until rebased upload.

## Phase 5 — Visual fidelity

**Status: completed in 0.6.0**

Implemented:

- ByLayer, ByObject, ByMaterial and nested ByParent color resolution.
- Object/layer/parent material-source resolution including layer render materials.
- Parent/child layer visibility inheritance with cycle protection.
- Separate source-object and effective display visibility for later layer overrides.
- Legacy material properties plus floating-point Rhino PBR metadata.
- Texture-slot filename/type/channel/projection/wrap/repeat/offset/rotation metadata.
- Backend-neutral appearance attached after geometry-cache generation.
- `Shaded`, `ShadedWithEdges` and `Wireframe` policies.
- Windows appearance/PBR/texture upload contracts.
- A visual scene builder that can change display properties without rebuilding source geometry.

**Regression coverage:** ByLayer/ByMaterial/ByParent, ancestor-layer hiding, display modes, Windows appearance upload, post-import layer re-enable and generated Rhino 8 legacy/PBR/texture round trips.

**Boundaries:** material and texture inputs are preserved but 0.6.0 does not claim final Rhino texture mapping, image loading, environment/ground-plane reproduction or Rhino Render/Cycles/RDK parity.

**Exit criteria:** the viewer resolves Rhino display appearance across layers and nested instances, respects hierarchical visibility, switches architectural display modes without rebuilding geometry, and hands resolved material inputs to the Windows backend.

## Phase 6 — Performance and robustness

**Status: completed in 0.7.0**

Implemented:

- Application-level background import around Rhino3dm's synchronous `File3dm.Read`, keeping the caller responsive while preserving the reader's real synchronous boundary.
- Deterministic import progress plus caller-visible cancellation. Cancellation during native archive read releases the caller; the worker stops semantic work as soon as Rhino3dm returns.
- `IThreeDmProgressiveImporter`: document-table header first, then bounded source-independent object batches, then final completion metadata.
- Bounded-channel backpressure so a slow renderer/UI cannot cause an unbounded queue of converted batches.
- Configurable file/object/layer/material/instance-definition safety ceilings.
- Configurable per-geometry ceilings for PointCloud, Mesh, Brep topology, SubD, NURBS/Polyline data and embedded Brep/Extrusion render meshes, checked before equivalent neutral-array allocation where the source API exposes counts.
- `ThreeDmSharedMeshSceneBuilder`: one local geometry payload per source object/subobject plus per-occurrence transform, source identity, subobject identity, `InstancePath` and resolved appearance.
- Compatibility preservation: the existing world-expanded `ThreeDmRenderSceneBuilder.Build()` behavior remains available; shared geometry is an opt-in path.
- `WindowsThreeDmSharedUploadProjection` with per-geometry local-origin rebasing and scene-origin-rebased instance matrices, so shared float buffers remain stable for large-coordinate models.
- `ThreeDmSharedMeshSceneStatistics` for deterministic unique-versus-expanded vertex/index accounting.

**Regression coverage:** progress/cancellation/resource-limit tests; progressive full-import equivalence; 8 / 128 / 1024-object bounded-batch baselines; pathological PointCloud/Mesh limits; repeated-block geometry sharing with independent transforms/ByParent appearance; 100-instance structural reuse; and Windows shared-buffer origin rebasing.

**Performance policy:** CI intentionally uses deterministic structural and batching baselines rather than hosted-runner millisecond thresholds. Stable-machine wall-clock, allocation and peak-working-set observations should use a maintained real-world Rhino corpus. See [`PERFORMANCE.md`](PERFORMANCE.md).

**Boundaries:** progressive availability begins after the synchronous Rhino archive read has completed; Phase 6 does not claim object-by-object archive decoding. Shared buffers currently target mesh payloads and do not remove per-instance selection/appearance metadata.

**Exit criteria:** file opening can leave the caller thread responsive, cancellation and progress are observable, converted objects can become available in bounded batches, pathological inputs have configurable guardrails, repeated block meshes can share payloads without losing identity, and large-coordinate instanced upload does not require per-instance vertex duplication.

## Phase 7 — SpatialViewer integration contract

**Status: completed in 0.8.0**

Implemented:

- Dedicated UI-independent `SpatialViewer.ThreeDm.Integration` assembly as the product-facing host boundary.
- Stable `SpatialViewer.ThreeDmHost` API v1 compatibility window for future package/runtime validation.
- Importer-injected open/close/cancel lifecycle without coupling the host facade to Rhino3dm.
- Model-bounds access and deterministic camera-fit calculation with aspect/FOV/padding validation.
- Hierarchical layer-tree projection with source visibility, runtime overrides, effective visibility and malformed-cycle protection.
- Runtime visibility overrides that rebuild display state without re-importing source geometry.
- Stable selection identities preserving source object, subobject and instance-path context.
- Property inspection for source object, layer, material and instance-path metadata.
- Integration regression tests covering lifecycle, cancellation, layer overrides, camera fit, selection resolution, unsupported-format routing and cyclic layer trees.

**Exit criteria:** SpatialViewer can bind to one UI-independent host assembly for document lifecycle and viewer state, while reader adapters and concrete Windows GPU rendering remain replaceable implementation details.

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
