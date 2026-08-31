# Contributing

Thank you for contributing to SpatialViewer.3DMCore.

- Confirm that a change belongs to the 3DM core rather than the SpatialViewer UI.
- Do not leak Rhino3dm/OpenNURBS types into `SpatialViewer.ThreeDm.Core` or rendering abstractions.
- Object-type fixes should add a minimal 3DM fixture or a reproducible test.
- Keep geometry correctness changes separate from broad namespace/refactor changes.
- Run Release build and tests before submitting changes.
