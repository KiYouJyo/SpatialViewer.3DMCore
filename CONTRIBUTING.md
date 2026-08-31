# 贡献指南

感谢参与 SpatialViewer.3DMCore。

- 修改前先确认代码属于 3DM 内核，而不是 SpatialViewer UI。
- Rhino3dm/OpenNURBS 类型不得泄漏到 `SpatialViewer.ThreeDm.Core` 或渲染抽象层。
- 新增或修复对象类型时，应同时增加最小 3DM fixture 或可重复构造的测试。
- 几何正确性修复与命名空间/大规模重构应分开提交。
- 提交前运行 `dotnet build SpatialViewer.3DMCore.sln -c Release` 与 `dotnet test SpatialViewer.3DMCore.sln -c Release`。
