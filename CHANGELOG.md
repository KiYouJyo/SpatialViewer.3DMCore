# Changelog

All notable changes to SpatialViewer.3DMCore are documented here.

## [Unreleased]

### Planned
- Add adaptive tessellation and render-scene generation.
- Add visual-fidelity resolution for layer/object colors, materials and wire/edge modes.
- Add real-world architectural/product Rhino fixture coverage, including Rhino-produced SubD, Light and ClippingPlane samples.
- Add large-model performance, progressive-loading and malformed-file baselines.

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
- Rhino3dm 8.32 cannot synthesize every advanced fixture required by the reader. Runtime-generated Light insertion is unsupported by `File3dmObjectTable`, and a vertex-only synthetic SubD is not a valid serializable SubD. Reader support remains implemented; real Rhino-produced samples are tracked as fixture-corpus follow-up work.

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
