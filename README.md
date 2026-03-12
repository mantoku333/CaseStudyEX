# CaseStudyEX

Unity 6 で開発中の 2D メトロイドヴァニア試作プロジェクトです。  
現在はプレイヤー移動、傘グライド、パララックス背景、ScriptableObject ベースのデータ定義を中心に実装しています。

## Environment

- Unity: `6000.3.8f1`
- Render Pipeline: URP (`com.unity.render-pipelines.universal` `17.3.0`)
- Input: Input System (`1.18.0`)
- Version Control: Git + Git LFS

## Setup

```bash
git clone https://github.com/mantoku333/CaseStudyEX.git
cd CaseStudyEX
git lfs install
git lfs pull
```

1. Unity Hub で `6000.3.8f1` をインストール
2. Unity Hub からこのプロジェクトフォルダを追加して開く

## Quick Start

1. `Assets/Scenes/SampleScene.unity` を開く
2. Play して挙動確認

`ProjectSettings/EditorBuildSettings.asset` では `Assets/Scenes/SampleScene.unity` が有効化されています。

## Current Features

- `PlayerPlatformerMockController`
- 左右移動 / ジャンプ
- 傘の開閉 + 滑空（落下速度制御）
- 着地時・ジャンプ開始時の squash & stretch（DOTween）
- `TripleParallaxBackground`
- 3 レイヤーの無限パララックス背景
- ScriptableObject データ定義
- `EnemyData`
- `ItemData`
- `AbilityData`
- デバッグ補助
- `SROptions.Debug`: プレイヤー位置リセット

## Controls (Current Sample)

- Move: `A / D` or `← / →`
- Jump: `Space`
- Umbrella Toggle: `Mouse Right Click`

`UmbrellaToggle` アクション未定義時は、`PlayerPlatformerMockController` のフォールバックで右クリックが使用されます。

## Key Packages

- `com.unity.inputsystem`
- `com.unity.cinemachine`
- `com.unity.postprocessing`
- `com.cysharp.unitask`
- `jp.hadashikick.vcontainer`
- `com.coffee.softmask-for-ugui`
- `com.coffee.ui-effect`
- `com.coffee.ui-particle`
- `com.yujiap.project-window-history`

## Project Structure (Top-Level)

- `Assets/`: Scenes, scripts, prefabs, art, audio, data
- `Packages/`: Unity package manifest/lock
- `ProjectSettings/`: Unity project settings
- `md/`: 開発ドキュメント

## Documentation

- [Setup Guide](md/SETUP.md)
- [Folder Structure](md/FOLDER_STRUCTURE.md)
- [Quick Reference](md/QUICK_REFERENCE.md)
- [Contributing](md/CONTRIBUTING.md)
