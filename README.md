# SpatialViewer.3DMCore

[中文](README.md) · [日本語](README.ja.md) · [English](README.en.md)

SpatialViewer 的独立 Rhino 3DM 看图内核仓库。这里维护 3DM 文件读取适配、Rhino 文档语义模型、三维几何/场景转换、渲染抽象与回归测试；WinUI 3 产品界面保留在 `KiYouJyo/SpatialViewer`。

> 当前版本：0.8.0，Phase 7 SpatialViewer Integration Contract 已完成。新增 UI 无关的 `SpatialViewer.ThreeDm.Integration` 宿主层，固定 1.x Host Contract、打开/关闭/取消生命周期、模型范围与相机适配、层级图层覆盖、稳定 selection ID 与属性检查接口；Integration 不再直接依赖 Rhino3dm adapter，而由产品显式注入 `IThreeDmImporter`。当前内核已经具备进入 SpatialViewer 主程序 3D 视口接入阶段的稳定边界。

## 设计原则

- **UI 无关**：解析、文档模型、几何与场景转换不得依赖 WinUI 3 页面或控件。
- **读取器隔离**：Rhino3dm 仅存在于适配项目中，不向上层公开第三方类型。
- **几何语义优先**：Curve/NURBS/Brep/Extrusion/SubD 等保持原始语义；显示网格是缓存/派生结果，不反向替代源几何。
- **双精度优先**：模型空间、变换、包围盒与相机运算使用 double，GPU 上传前再进行必要转换。
- **实例优先**：Block/InstanceDefinition 采用定义 + 变换引用，不在导入阶段无条件展开复制。
- **可回归**：每类 Rhino 对象、图层/材质/颜色、实例、曲面离散和异常文件都必须由单元测试/夹具覆盖。
- **独立版本**：内核与 SpatialViewer UI 分别版本化，由主程序显式锁定内核版本。

## 仓库边界

本仓库是 Rhino/3DM 内核的唯一源代码归属。`SpatialViewer` 负责窗口、标签页、工具栏、属性面板、视图操作和用户交互，只通过稳定接口使用本仓库提供的能力。

开发计划见 [`docs/ROADMAP.md`](docs/ROADMAP.md)，架构边界见 [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)，大模型执行边界见 [`docs/PERFORMANCE.md`](docs/PERFORMANCE.md)。

## 许可证

MIT License。第三方依赖及许可信息见 `THIRD-PARTY-NOTICES.md`。
