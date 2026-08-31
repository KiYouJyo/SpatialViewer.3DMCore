# SpatialViewer.3DMCore

[中文](README.md) · [日本語](README.ja.md) · [English](README.en.md)

SpatialViewer 向けの独立した Rhino 3DM ビューアー内核です。本リポジトリでは 3DM 読み込みアダプター、Rhino ドキュメント意味モデル、3D ジオメトリ／シーン変換、レンダリング抽象、回帰テストを管理します。WinUI 3 の製品 UI は `KiYouJyo/SpatialViewer` に残します。

> 現段階では `SpatialViewer.CadCore` と同じ独立ビルド境界とリポジトリ骨格を構築します。最初の読み込みアダプターは McNeel 公式 `Rhino3dm` を利用し、レイヤー、属性、インスタンス、Curve/NURBS、Brep、Extrusion、Mesh、SubD、マテリアル、ビュー情報を安定して取り込むことを優先します。

## 設計原則

- **UI 非依存**：解析、文書モデル、ジオメトリ、シーン変換は WinUI 3 のページやコントロールに依存しません。
- **リーダー分離**：Rhino3dm 型はアダプター層の外へ公開しません。
- **意味保持**：Curve/NURBS/Brep/Extrusion/SubD の元の意味を保持し、表示 Mesh は派生キャッシュとして扱います。
- **倍精度優先**：モデル空間、変換、境界、カメラは GPU 転送直前まで double を使用します。
- **インスタンス優先**：ブロックは定義＋変換参照として保持し、無条件に展開しません。
- **回帰可能**：主要オブジェクト、色／材質、インスタンス、テセレーション、異常ファイルを fixture とテストで固定します。
- **独立バージョン**：内核と SpatialViewer UI は別々にバージョン管理します。

詳細は [`docs/ROADMAP.md`](docs/ROADMAP.md) と [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) を参照してください。

## ライセンス

MIT License。第三者依存関係は `THIRD-PARTY-NOTICES.md` を参照してください。
