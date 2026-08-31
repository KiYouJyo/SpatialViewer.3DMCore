# コントリビューション

SpatialViewer.3DMCore への貢献ありがとうございます。

- 変更が SpatialViewer UI ではなく 3DM 内核に属することを確認してください。
- Rhino3dm/OpenNURBS 型を `SpatialViewer.ThreeDm.Core` やレンダリング抽象層へ漏らさないでください。
- オブジェクト型の追加・修正には最小 3DM fixture または再現可能なテストを追加してください。
- ジオメトリ正確性の修正と大規模な名前空間整理は分離してください。
- 提出前に Release build と test を実行してください。
