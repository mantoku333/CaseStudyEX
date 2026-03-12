# セットアップガイド

このガイドでは、プロジェクトのセットアップと開発を開始するための手順を説明します。

## 必要な環境

- **Unity**: Unity 6000.3.8f1
- **Git**: 最新バージョン推奨
- **Git LFS**: 大きなバイナリファイル管理用（推奨）
- **IDE**: Visual Studio, Rider, VS Code

## 初回セットアップ

### 1. リポジトリのクローン

```bash
git clone https://github.com/mantoku333/Case.git
cd Case
```

### 2. Git LFS のセットアップ（推奨）

大きなアセット（画像、音声、3Dモデル等）を効率的に管理するため、Git LFSの使用を推奨。

```bash
# Git LFSをインストール（未インストールの場合）
# Windows: https://git-lfs.github.com/ からインストーラーをダウンロード
# Mac: brew install git-lfs
# Linux: sudo apt-get install git-lfs

# Git LFSを有効化
git lfs install

# LFS管理ファイルを取得
git lfs pull
```

### 3. Unity Hub で Unity 6000.3.8f1 をインストール

1. Unity Hub を開く
2. 「Installs」タブを選択
3. 「Install Editor」をクリック
4. バージョン **6000.3.8f1** を選択してインストール
5. 必要なモジュールを選択:
   - **Windows Build Support** 
   - **Documentation**（推奨）

### 4. Unity Hub でプロジェクトを開く

1. Unity Hub の「Projects」タブを選択
2. 「Add」をクリック
3. クローンした `Case` フォルダを選択
4. プロジェクトをダブルクリックして開く

### 5. Unity エディタの設定確認

プロジェクトを開いたら、以下の設定を確認してください：

#### Asset Serialization Mode
1. `Edit > Project Settings > Editor`
2. `Asset Serialization Mode` が **Force Text** になっていることを確認
   - これにより、YAMLテキスト形式で保存され、Gitマージが容易になります

#### Version Control Mode
1. `Edit > Project Settings > Editor`
2. `Version Control Mode` が **Visible Meta Files** になっていることを確認
   - これにより、`.meta`ファイルが可視化され、Gitで管理しやすくなります

#### Line Ending
1. `Edit > Project Settings > Editor`
2. `Line Endings For New Scripts` を **Unix (LF)** に設定
   - チーム全体で統一された改行コードを使用

## 開発の開始

### ブランチの作成

作業を開始する前に、作業用ブランチを作成

```bash
# developブランチを最新にする
git checkout develop
git pull origin develop

# 新しい機能ブランチを作成
git checkout -b feature-名前-あなたの機能名
```

例:
```bash
git checkout -b feature-nakae-player-movement
git checkout -b feature-oozono-enemy-slime
git checkout -b feature-fuyuno-ui-health-bar
```

### 作業フォルダの確認

`FOLDER_STRUCTURE.md` を参照して、あなたの担当フォルダを確認してください。

例：プレイヤー担当の場合
- `Assets/Scripts/Player/`
- `Assets/Prefabs/Player/`
- `Assets/Art/Sprites/Player/`
- `Assets/Art/Animations/Player/`
- `Assets/Data/Player/`

### テストシーンの作成

コンフリクトを避けるため、自分用の作業環境を作る。

1. `Assets/Scenes/TestScenes/` を右クリック
2. `Create > Scene` を選択
3. シーン名: `名前_機能Test.unity` （例: `mantoku_SceneChangeTest.unity`）
4. このシーンで開発を行う

## ワークフロー

### 1. 作業開始前

```bash
# 最新の変更を取得
git fetch origin
git rebase origin/develop
```

### 2. 作業中

```bash
# 変更をこまめにコミット
git add .
git commit -m "feat: 実装内容を記述"

# リモートにプッシュ（バックアップ）
git push origin feature/あなたの機能名
```

### 3. 作業完了後

```bash
# 最新のdevelopを取り込む
git fetch origin
git rebase origin/develop

# Unityでビルドが通ることを確認
# テストを実行

# Pull Requestを作成
# GitHub上でPull Requestを作成し、レビューを依頼
```

