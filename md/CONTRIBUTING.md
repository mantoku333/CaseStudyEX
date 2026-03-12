# 開発ガイドライン

このドキュメントは、チーム開発でのコンフリクトを最小限に抑えるための開発ガイドラインです。

## ブランチについて

### マスター
- `master`: 常に安定したビルド可能な状態を保つ

### 基軸
- `develop`: 開発中の最新コード

### 新機能開発
- 命名規則: `feature-<名前>-<実装したいもの>` (例: `feature-mantoku-enemy`, `feature-fuyuno-stage`)
- 1つの機能につき1つのブランチで
- 作業完了後はPR依頼を出す

### ブランチ作成例
```bash
git checkout develop
git pull origin develop
git checkout -b feature-mantoku-enemy
```

## コミットメッセージ
コパイロットのやつで


### ーーー以下チラ裏ーーー ###


### 形式
```
<type>: <subject>

<body>
```

### タイプ
- `feat`: 新機能
- `fix`: バグ修正
- `docs`: ドキュメント変更
- `style`: コードスタイル修正（動作に影響なし）
- `refactor`: リファクタリング
- `test`: テスト追加・修正
- `chore`: ビルドプロセス、補助ツール変更

### 例
```
feat: プレイヤーのダッシュ機能を実装

- InputSystemを使用したダッシュ入力検知
- ダッシュアニメーション追加
- スタミナ消費システム実装
```

## コンフリクト回避のルール

### 1. シーン編集のルール
Unityのシーンファイル直接いじらない！

### 2. Prefab編集のルール

**推奨事項**:
- 大きなPrefabは機能単位で分割（例: プレイヤーPrefab = Body + WeaponSystem + EffectSystem）
- Variantsを活用
- Nestedで階層的に管理

### 3. スクリプト編集のルール

**推奨事項**:
- 1クラス1ファイルの原則
- 共通インターフェースを先に定義
- マネージャークラスは1人が責任を持つ
- Pull Request前にコードレビューを依頼

### 4. ScriptableObject の活用

**メリット**:
- データとコードを分離
- コード変更なしで調整可能
- マージコンフリクトが起きにくい

**使用例**:
```csharp
// Data/Enemies/Slime.asset
[CreateAssetMenu(fileName = "EnemyData", menuName = "Game/Enemy Data")]
public class EnemyData : ScriptableObject
{
    public string enemyName;
    public int maxHealth;
    public float moveSpeed;
    public int attackDamage;
}
```

### 5. Git運用のベストプラクティス

#### 作業開始前
```bash
# 最新のdevelopを取得
git checkout develop
git pull origin develop

# 作業ブランチ作成
git checkout -b feature/your-feature
```

#### 作業中
```bash
# こまめにコミット
git add .
git commit -m "feat: 実装内容"

# 定期的にdevelopの変更を取り込む
git fetch origin
git rebase origin/develop
```

#### Pull Request前
```bash
# developの最新を取り込む
git fetch origin
git rebase origin/develop

# ビルドとテストを実行
# Unityでビルドが通ることを確認
```

## コードレビューチェックリスト

Pull Request作成時は以下を確認:

- [ ] ビルドが通る
- [ ] 新しい警告(Warning)が出ていない
- [ ] 既存の実装に影響が出ていない
- [ ] 不要なファイルがコミットされていない（`*.meta`ファイルは必要なので含める）

## Unityエディタ設定

### Asset Serialization Mode
- **設定**: `Edit > Project Settings > Editor > Asset Serialization Mode`
- **推奨**: `Force Text`
- **理由**: YAMLテキスト形式でマージしやすくなる

### Version Control Mode
- **設定**: `Edit > Project Settings > Editor > Version Control Mode`
- **推奨**: `Visible Meta Files`
- **理由**: `.meta`ファイルが可視化され、Gitで管理しやすくなる

## トラブルシューティング

### マージコンフリクトが発生した場合

#### シーンファイルのコンフリクト
```bash
# 自分の変更を優先する場合
git checkout --ours path/to/scene.unity
git add path/to/scene.unity

# 相手の変更を優先する場合
git checkout --theirs path/to/scene.unity
git add path/to/scene.unity
```

#### Prefabのコンフリクト
- Unity Editorで両方のバージョンを開いて手動マージ
- またはSmart Merge Toolを使用（Unity公式ツール）

### .metaファイルが消えた場合
```bash
# Unity Editorを閉じて、該当アセットを削除し、再インポート
```

## 参考リンク

- [Unity - Version Control](https://docs.unity3d.com/Manual/VersionControl.html)
- [Unity - Smart Merge](https://docs.unity3d.com/Manual/SmartMerge.html)
- [Git - Branching Strategy](https://git-scm.com/book/en/v2/Git-Branching-Branching-Workflows)

## 質問・相談

コンフルかディスコードで
