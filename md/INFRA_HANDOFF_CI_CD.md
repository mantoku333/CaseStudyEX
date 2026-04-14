# CI/CD インフラ引き継ぎ

## 目的

このドキュメントは、Jenkins と GitHub 連携の設定を担当するインフラ担当者向けです。  
リポジトリ側の実装は完了しているため、インフラ設定と疎通確認を行えば運用を開始できます。

## 想定フロー

1. 開発者は `develop` で作業する
2. `develop -> master` の PR を作成する
3. GitHub Actions が PR の CI チェックを実行する
4. PR を `master` にマージする
5. GitHub Actions が Jenkins を起動する
6. Jenkins が Unity ビルドを実行し、成果物を保存する

## リポジトリ側で実装済み

1. Actions ワークフロー: `.github/workflows/master-ci.yml`
2. Jenkins パイプライン: `Jenkinsfile`
3. Unity バッチビルドのエントリ: `Assets/Editor/CI/JenkinsBuild.cs`
4. 補足ドキュメント: `md/GITHUB_ACTIONS_CI.md`, `md/JENKINS_SETUP.md`

## インフラ担当の作業

1. Jenkins ノードを用意する（現ターゲットは Windows 推奨）
2. Jenkins ノードに Unity `6000.3.8f1` をインストールする
3. Jenkins ノードで Unity ライセンスを有効化する
4. Jenkins ノードで `git lfs version` が成功することを確認する
5. SCM から `Jenkinsfile` を使う Pipeline Job を作成する
6. Jenkins Job の `Trigger builds remotely` を有効化し、トークンを発行する
7. Jenkins のグローバル環境変数に `UNITY_PATH=C:\\Program Files\\Unity\\Hub\\Editor\\6000.3.8f1\\Editor\\Unity.exe` を設定する
8. GitHub Actions Secret に `JENKINS_WEBHOOK_URL=https://<jenkins-host>/job/<job-name>/buildWithParameters?token=<token>` を設定する

## 動作確認手順

1. `develop -> master` のテスト PR を作成する
2. Actions の `quick-ci-checks` が成功することを確認する
3. PR を `master` にマージする
4. Actions の `trigger-jenkins` が成功することを確認する
5. Jenkins が自動起動することを確認する
6. Jenkins の `Unity Build (master only)` ステージが実行されることを確認する
7. Jenkins に `Logs/jenkins-build.log` が保存されることを確認する
8. Jenkins に `Build/**/*` が保存されることを確認する

## 完了条件

1. `master` へのマージごとに Jenkins が自動起動する
2. Unity ビルドが手動介入なしで完了する
3. 成果物とログが Jenkins に保存される
4. 失敗時に Jenkins の再実行で復旧できる

## トラブル時の確認ポイント

1. Jenkins が起動しない場合: `JENKINS_WEBHOOK_URL` と Jenkins のトリガートークン/エンドポイントを確認する
2. Unity ビルドが始まらない場合: `UNITY_PATH` と Jenkins ノードの Unity ライセンス状態を確認する
3. アセット不足エラーの場合: Jenkins ログで `git lfs pull` が成功しているか確認する
