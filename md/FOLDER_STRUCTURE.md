# プロジェクト構造ダイアグラム

## 完全なフォルダツリー

```
Case/
├── .git/
├── .gitignore                      # Unity用.gitignore
├── .gitattributes                  # Unity用マージ設定
├── .vsconfig                       # Visual Studio設定
├── README.md                       # プロジェクト説明
├── CONTRIBUTING.md                 # 開発ガイドライン
│
├── Assets/                         # Unityアセットフォルダ
│   │
│   ├── Scripts/                    # C#スクリプト
│   │   ├── README.md
│   │   ├── Player/                 # プレイヤー関連
│   │   │   └── .gitkeep
│   │   ├── Enemy/                  # 敵関連
│   │   │   └── .gitkeep
│   │   ├── Environment/            # 環境オブジェクト
│   │   │   └── .gitkeep
│   │   ├── Items/                  # アイテム
│   │   │   └── .gitkeep
│   │   ├── UI/                     # UI
│   │   │   └── .gitkeep
│   │   ├── Managers/               # マネージャー
│   │   │   └── .gitkeep
│   │   ├── Utilities/              # ユーティリティ
│   │   │   └── .gitkeep
│   │   ├── Combat/                 # 戦闘システム
│   │   │   └── .gitkeep
│   │   ├── Movement/               # 移動システム
│   │   │   └── .gitkeep
│   │   ├── Audio/                  # オーディオシステム
│   │   │   └── .gitkeep
│   │   └── Data/                   # ScriptableObjectクラス定義
│   │       ├── EnemyData.cs
│   │       ├── ItemData.cs
│   │       └── AbilityData.cs
│   │
│   ├── Scenes/                     # シーンファイル
│   │   ├── README.md
│   │   ├── SampleScene.unity       # デフォルトシーン
│   │   ├── MainScenes/             # メインシーン
│   │   │   └── .gitkeep
│   │   ├── AreaScenes/             # エリアシーン
│   │   │   └── .gitkeep
│   │   └── TestScenes/             # テストシーン
│   │       └── .gitkeep
│   │
│   ├── Prefabs/                    # プレハブ
│   │   ├── README.md
│   │   ├── Player/
│   │   │   └── .gitkeep
│   │   ├── Enemies/
│   │   │   └── .gitkeep
│   │   ├── Environment/
│   │   │   └── .gitkeep
│   │   ├── Items/
│   │   │   └── .gitkeep
│   │   ├── UI/
│   │   │   └── .gitkeep
│   │   ├── Projectiles/
│   │   │   └── .gitkeep
│   │   └── Effects/
│   │       └── .gitkeep
│   │
│   ├── Art/                        # アートアセット
│   │   ├── Sprites/                # スプライト
│   │   │   ├── Player/
│   │   │   │   └── .gitkeep
│   │   │   ├── Enemies/
│   │   │   │   └── .gitkeep
│   │   │   ├── Environment/
│   │   │   │   └── .gitkeep
│   │   │   ├── Items/
│   │   │   │   └── .gitkeep
│   │   │   ├── UI/
│   │   │   │   └── .gitkeep
│   │   │   └── Effects/
│   │   │       └── .gitkeep
│   │   ├── Animations/             # アニメーション
│   │   │   ├── Player/
│   │   │   │   └── .gitkeep
│   │   │   ├── Enemies/
│   │   │   │   └── .gitkeep
│   │   │   └── Effects/
│   │   │       └── .gitkeep
│   │   ├── Tilemaps/               # タイルマップ
│   │   │   └── .gitkeep
│   │   ├── Materials/              # マテリアル
│   │   │   └── .gitkeep
│   │   └── VFX/                    # ビジュアルエフェクト
│   │       └── .gitkeep
│   │
│   ├── Audio/                      # オーディオ
│   │   ├── Music/                  # BGM
│   │   │   └── .gitkeep
│   │   └── SFX/                    # 効果音
│   │       ├── Player/
│   │       │   └── .gitkeep
│   │       ├── Enemy/
│   │       │   └── .gitkeep
│   │       ├── Environment/
│   │       │   └── .gitkeep
│   │       └── UI/
│   │           └── .gitkeep
│   │
│   ├── Data/                       # ScriptableObjectアセット
│   │   ├── README.md
│   │   ├── Player/
│   │   │   └── .gitkeep
│   │   ├── Enemies/
│   │   │   └── .gitkeep
│   │   ├── Items/
│   │   │   └── .gitkeep
│   │   ├── Abilities/
│   │   │   └── .gitkeep
│   │   └── GameSettings/
│   │       └── .gitkeep
│   │
│   ├── Resources/                  # Resourcesフォルダ
│   │   └── .gitkeep
│   │
│   ├── Plugins/                    # サードパーティプラグイン
│   │   └── .gitkeep
│   │
│   └── Settings/                   # Unity設定
│       ├── Renderer2D.asset
│       ├── UniversalRP.asset
│       └── ...
│
├── Packages/                       # Unityパッケージ管理
│   ├── manifest.json
│   └── packages-lock.json
│
└── ProjectSettings/                # プロジェクト設定
    └── ProjectVersion.txt
```
