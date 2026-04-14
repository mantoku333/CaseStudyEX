# 以下工事中


## 環境もろもろ

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
- [Contributing](md/CONTRIBUTING.md)
- [GitHub Actions CI](md/GITHUB_ACTIONS_CI.md)
- [Jenkins Setup](md/JENKINS_SETUP.md)
