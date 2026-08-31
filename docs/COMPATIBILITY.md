# Compatibility

## Initial target

- File format: Rhino `.3dm`
- Reader adapter: McNeel `Rhino3dm` 8.32.0
- Runtime: .NET 10, x64
- Host UI target: SpatialViewer / WinUI 3 (host-side only; no UI dependency inside the core)

## Compatibility policy

The core should accept practical Rhino 5/6/7/8-era 3DM files where supported by the current openNURBS/Rhino3dm reader. Compatibility is validated by fixture files rather than inferred from extension alone.

Unsupported or partially supported object types must be reported diagnostically instead of silently discarded.
