# Changelog

All notable changes to SpatialViewer.3DMCore are documented here.

## [Unreleased]

### Planned
- Add real-world architectural/product Rhino fixture coverage, including Rhino-produced SubD, Light and ClippingPlane samples.

## [0.7.0] - 2026-09-02

### Added
- Non-blocking application-level import around Rhino3dm's synchronous `File3dm.Read`, with caller-visible cancellation and deterministic import progress stages.
- `IThreeDmProgressiveImporter` with header, bounded object-batch and completion updates after the archive read has completed.
- Bounded-channel backpressure so progressive conversion cannot queue unbounded scene batches when the consumer is slower than the producer.
- Configurable document-level safety ceilings for file size, object count, layers, materials and instance definitions.
- Configurable per-geometry safety ceilings for PointCloud points, Mesh vertices/faces, Brep topology, SubD vertices/faces, NURBS control points, Polyline points and embedded render meshes.
- `ThreeDmSharedMeshSceneBuilder`, preserving one local mesh payload per source/subobject while representing repeated block occurrences as transform + identity + appearance instances.
- Shared-mesh appearance inheritance across nested instances without deduplicating picking identity or `InstancePath` metadata.
- `WindowsThreeDmSharedUploadProjection`, converting shared geometry to float once with per-geometry local origins and rebased per-instance 4×4 transforms for large-coordinate stability.
- `ThreeDmSharedMeshSceneStatistics` for deterministic unique-versus-expanded vertex/index accounting.
- Dedicated performance and robustness documentation in `docs/PERFORMANCE.md`.

### Tests
- Added progress-stage, cancellation, pre-read file-size and post-read object-count regression coverage.
- Added progressive-import equivalence tests and deterministic 8 / 128 / 1024-object bounded-batch baselines.
- Added pathological single-geometry regressions for PointCloud and Mesh safety ceilings.
- Added repeated-block tests proving two occurrences reuse one geometry payload while retaining independent transforms, instance paths and inherited colors.
- Added a 100-instance structural baseline proving one triangle remains 3 unique vertices / 3 unique indices instead of 300 / 300 expanded entries.
- Added Windows shared-upload coverage for geometry reuse plus local/scene origin rebasing.

### Notes
- Rhino3dm 8.32 exposes synchronous archive reading. `0.7.0` moves that work away from the caller thread, but does not claim that the native `File3dm.Read` itself can be interrupted or that `.3dm` archive decoding is streamed object-by-object.
- Progressive availability starts after the archive has been read and validated; document tables arrive first, then bounded source-independent object batches.
- The existing world-expanded `ThreeDmRenderSceneBuilder.Build()` path remains compatible. Shared mesh buffers are an opt-in rendering path for block-heavy models.
- CI uses deterministic structural/batching baselines rather than hard hosted-runner millisecond gates. Wall-clock and peak-memory measurements belong to a stable local real-world Rhino corpus.

## [0.6.0] - 2026-08-31

### Added
- Source-independent visual-appearance resolution for `ColorFromLayer`, `ColorFromObject`, `ColorFromMaterial` and nested-instance `ColorFromParent` semantics.
- Material-source resolution for object, layer and parent inheritance, including layer render-material references.
- Effective layer visibility across parent/child layer chains while retaining source object visibility independently for later layer-tree overrides.
- Legacy material display properties: diffuse color, transparency, specular/emission colors, shine and reflectivity.
- PBR material metadata guarded by Rhino's physical-material mode, preserving floating-point base RGBA, metallic, roughness, alpha, opacity, clearcoat, clearcoat roughness and BRDF.
- Material texture-slot metadata including file reference, texture type, enabled state, mapping channel, projection/wrap modes, repeat/offset and rotation.
- Backend-neutral `ThreeDmRenderAppearance` attached consistently to meshes, curves and point sets after geometry-cache generation.
- `Shaded`, `ShadedWithEdges` and `Wireframe` display policies. Brep/Extrusion semantic edges remain available as accurate wire fallbacks when fill meshes are absent; generic mesh/surface wireframe uses deduplicated triangle edges.
- Windows upload projection for resolved appearance, legacy material parameters, PBR parameters and texture-slot metadata alongside the existing origin-rebased geometry upload.
- `ThreeDmVisualRenderSceneBuilder`, separating tessellation/cache generation from layer/material/instance appearance resolution so display-property changes do not invalidate geometry caches.

### Tests
- Added resolution tests for layer colors/materials, `ColorFromMaterial`, nested `ByParent` inheritance and ancestor-layer visibility.
- Added display-mode tests covering shaded edge suppression, wireframe mesh-edge generation and semantic Brep/Extrusion wire preservation.
- Added Windows upload regression coverage for appearance, PBR parameters and texture metadata.
- Added generated Rhino 8 `.3dm` round-trip coverage for parent/child layer visibility, layer render materials, legacy material properties, PBR material parameters and texture filename metadata.
- Added regression coverage proving a hidden parent layer can later be re-enabled without re-importing or losing source-object geometry semantics.

### Notes
- Texture slots and normalized mesh/surface UVs are preserved as inputs for a future concrete renderer; 0.6.0 does not claim final Rhino texture mapping, image loading or shader parity.
- PBR base color stays floating-point through Core/neutral rendering and is converted to float only at the Windows upload boundary rather than being prematurely quantized to ARGB.
- Full Rhino Render/Cycles/RDK material, environment and ground-plane parity remains outside the initial read-core scope.

## [0.5.0] - 2026-08-31

### Added
- Source-independent render-scene generation for points, point clouds, analytic/general curves, meshes, standalone NURBS surfaces, Brep render meshes, extrusions and nested instances.
- Draft / Normal / High tessellation presets with camera-relative, model-tolerance-relative or explicit absolute chord tolerances.
- Adaptive analytic Arc/Circle/Ellipse tessellation that retains the source curve type and full plane basis instead of permanently flattening source geometry.
- Independent rational/non-rational NURBS curve and tensor-product NURBS surface evaluation from neutral Core contracts, including openNURBS superfluous knot boundaries.
- Knot-span-aware adaptive standalone NURBS surface tessellation with crack-free parameter grids, per-direction budgets, indexed triangles, parametric UVs and area-weighted vertex normals.
- Embedded Rhino render-mesh reuse for trimmed Breps and Extrusions, with exact Brep-edge/analytic-wire fallbacks and structured diagnostics when no fill mesh is stored.
- Nested InstanceDefinition / InstanceReference render expansion with composed 4×4 transforms and retained instance paths for downstream picking/selection.
- Tessellation caches keyed by source geometry identity and quality settings, including conservative camera-relative tolerance buckets to avoid near-duplicate meshes while zooming.
- Double-precision neutral render data plus Windows upload projection with origin rebasing before conversion to float buffers for large-coordinate models.

### Tests
- Added analytic curve, independent NURBS evaluation, rotated-plane, mesh triangulation, nested instance and cache regression coverage.
- Added generated Rhino 8 `.3dm` round-trip coverage for embedded Brep render meshes and a rational curved NURBS sphere rendered entirely through the neutral evaluator/tessellator.
- Added large-coordinate Windows upload tests and closed-surface normal stability checks.
- Added symmetric Brep/Extrusion embedded-render-mesh and explicit-fallback tests.

### Notes
- Standalone surfaces can be tessellated independently because they are untrimmed. Trimmed Brep faces are never filled by ignoring their trims; they use stored Rhino render meshes or remain exact edge/wire geometry with a diagnostic.
- Surface UVs in this phase are normalized parametric coordinates. Material texture mapping and render-content fidelity belong to Phase 5.
- Smooth SubD limit-surface meshing remains on a future compatible display-mesh path; Phase 4 preserves and renders the SubD control net rather than inventing a non-Rhino-compatible limit evaluator.

## [0.4.0] - 2026-08-31

### Added
- Source-independent Brep vertex/edge/trim/loop/face topology with semantic edge curves, trim parameter curves and face NURBS surfaces.
- InstanceDefinition member graphs and InstanceReference 4×4 transforms, including nested instance regression coverage.
- TextDot, text annotation, leader-point and hatch-boundary viewing semantics.
- SubD control-net reading contracts and conversion support.
- Light viewing metadata supported by Rhino3dm 8.32.
- ClippingPlane viewport, participation-list and plane-depth semantics.
- Real Rhino 8 `.3dm` write/read regression coverage for Brep topology, TextDot, text/leader annotations, hatch and two-level nested instances.

### Notes
- Rhino3dm 8.32 cannot synthesize every advanced fixture required by the reader. Runtime-generated Light insertion is unsupported by `File3dmObjectTable`, and a synthetic vertex-only SubD is not a valid serializable SubD. Reader support remains implemented; real Rhino-produced samples are tracked as fixture-corpus follow-up work.

## [0.3.0] - 2026-08-31

### Added
- Source-independent Point, PointCloud, Curve/NURBS, Surface/NURBS, Extrusion and Mesh geometry contracts.
- Analytic Arc/Circle/Ellipse classification without permanent line-segment flattening.
- NURBS curve control points, weights and knots; NURBS surface control nets and knot vectors.
- Mesh topology, normals and texture coordinates.
- Real Rhino 8 `.3dm` semantic geometry round-trip tests.

## [0.2.0] - 2026-08-31

### Added
- Rhino3dm-backed `File3dm.Read` document ingestion.
- Archive/application/revision metadata, units and tolerances.
- Layer hierarchy/visibility/locking/color metadata.
- Materials, named views and object attributes.
- Geometry classification, double-precision bounds, cancellation and structured diagnostics.
- Real `.3dm` document write/read regression coverage.

## [0.1.0] - 2026-08-31

### Added
- Independent repository boundary and solution skeleton.
- Three-language repository documentation.
- Core, format, adapter, rendering, Windows backend, and test project boundaries.
- Initial architecture and implementation roadmap.
