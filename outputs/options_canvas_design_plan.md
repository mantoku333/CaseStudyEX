# OptionsCanvas Design Plan

前提:

- OptionsCanvas に入る UI は基本的にすべてデザイナー作成/Figma 由来として扱う。
- 見た目の状態は `Select` / `Not Select` のように、状態ごとに別の絵・別の GameObject として存在する。
- 現段階では配置、素材、表示サイズ、クリック時/hover 時の見た目は変えない。
- 既に機能している挙動は維持する。

## 今回の整理方針

今回の変更では、Prefab やデザイン素材には触れず、重複していた Button 構築処理だけを共通化する。

共通化した入口:

- `OptionsCanvasButtonUtility.EnsureStateButton`
- `OptionsCanvasButtonUtility.ConfigureExistingGraphicButton`

対象:

- `OptionsMainMenuSkin`
- `OptionsFinishPromptSkin`
- `OptionsMenu` の Q/E/Back/ページタブ系 runtime Button 設定

維持した挙動:

- `Select` / `Not Select` の切り替えは既存の `OptionsMenuButtonState` のまま。
- MainMenu は hover SFX ありのまま。
- FinishPrompt と OptionsMenu 本体の Button は、既存通り click SFX 登録のみ。
- Q/E/Back/ページタブ系は既存 Button を前提にし、無い場合に追加しない。
- Button の `transition = None` と、最大面積 Graphic を hit target にする挙動を維持。

## OptionsCanvas の責務分割

### OptionsMenu

役割:

- メニューを開く/閉じる。
- ゲーム停止/復帰。
- ページ遷移。
- 音量・キー設定・マップ・装飾・タイトル復帰などの機能制御。

持たせないもの:

- Figma ノードの Button 化ルール。
- `Select` / `Not Select` の直接的な状態管理。
- ボタンごとの hit area 探索。

### OptionsMainMenuSkin

役割:

- Figma 由来のメインメニュー見た目を OptionsMenu の機能に接続する。
- Continue/Save/Map/Option/Skill/TitleBack を Button 化する。
- 初期フォーカスを設定する。

持たせないもの:

- Button 化の細かい実装。
- raycastTarget の選定ロジック。

### OptionsFinishPromptSkin

役割:

- Figma 由来の終了確認 UI を OptionsMenu の機能に接続する。
- Yes/No を Button 化する。
- レイアウト補正は現状維持。

持たせないもの:

- Button 化の細かい実装。
- raycastTarget の選定ロジック。

### OptionsMenuButtonState

役割:

- 現在の `Select` / `Not Select` 表示切り替えを担当する。
- hover と keyboard/gamepad selected を同じ `Select` 表示として扱う。

現状維持:

- pressed 専用表示は追加しない。
- active/current-page 状態とは統合しない。

将来整理する場合:

- `OptionsMenuButtonState` を `FigmaButtonVisualState` のような共通名に変更する。
- `Normal / Hover / Pressed / Selected / Active / Disabled` に拡張する。
- ただし拡張時も既存素材が無い状態は今の表示に fallback する。

### OptionsCanvasButtonUtility

役割:

- Figma 由来ノードを Button 化する共通入口。
- `Select` / `Not Select` から hit target を選ぶ。
- Button の transition を統一する。
- `OptionsMenuButtonState` を設定する。

設計上の意味:

- 各 Skin クラスが同じ Button 構築処理を再実装しないようにする。
- Prefab のデザイン構造を変えずに、実装ルールだけを一箇所へ集める。

## 現在の OptionsCanvas 内 UI 分類

### 1. Figma 状態別ボタン

例:

- Alternate main menu
- Finish prompt

実装ルール:

- `OptionsCanvasButtonUtility.EnsureStateButton` を使う。
- `Select` / `Not Select` を表示状態として使う。
- `Button.transition = None`。
- `Select` / `Not Select` 内の最大 Graphic を hit target にする。

### 2. 既存 Button + 単独 Graphic

例:

- Q
- E
- Back Button
- Header tabs

実装ルール:

- `OptionsCanvasButtonUtility.ConfigureExistingGraphicButton` を使う。
- 既存 Button が無い場合は追加しない。
- 最大 Graphic を hit target にする。
- 表示/非表示やページ状態は `OptionsMenu` が管理する。

### 3. 機能入力ボタン

例:

- キー割り当て `ValueButton`
- Sound/Keyboard tab
- BackButton

実装ルール:

- 既存 serialized/reference Button を使う。
- 現在の表示・押下挙動を維持する。
- 将来的に Figma 状態別ノードへ置き換える場合は、同じ共通入口に移す。

### 4. ページ active 表示

例:

- Map/Decoration/Note/Settings の selected/normal pair
- Sound/Keyboard の active/base pair

実装ルール:

- これは hover ではなく「現在ページ/現在タブ」を表す。
- 現時点では `OptionsMenu` が `SetActive` で管理する。
- 将来 `Active` 状態を持つ visual state に移す場合も、current-page の意味は hover/selected と分離する。

## 今後の段階的な組み替え案

### Step 1: 現状維持の共通化

完了:

- Button 化の重複を `OptionsCanvasButtonUtility` に集約。
- Prefab、素材、配置、表示切り替えの意味は変更しない。

### Step 2: 検査ツール追加

候補:

- `Select` / `Not Select` があるのに Button 化されていない箇所を検出する。
- 複数 Graphic の `raycastTarget` が true になっている箇所を検出する。
- Figma 状態別ボタンなのに `transition != None` になっている箇所を検出する。

### Step 3: 状態名の標準化

候補:

- 既存 `Select` / `Not Select` は維持しつつ、内部 API では `selectedState` / `normalState` として扱う。
- 新規デザインは `Normal` / `Hover` / `Pressed` / `Active` の命名も許容する。
- 既存 Prefab は一括リネームしない。リネームはデザイナー作業や Figma 再取り込みとの衝突が起きやすいため。

### Step 4: VisualState 拡張

候補:

- `OptionsMenuButtonState` を拡張し、pressedRoot や activeRoot を扱えるようにする。
- ただし素材が無い場合は今と同じ表示へ fallback する。
- この段階で初めて pressed 専用デザインを反映する。

## 変更禁止事項

今回の作業では以下を変更しない。

- OptionsCanvas prefab の RectTransform 値。
- Figma 由来 prefab の素材参照。
- 表示順・sorting order。
- `Select` / `Not Select` の表示タイミング。
- クリック先のメソッド。
- hover/pressed 時の見た目。
- 現在動いているページ遷移やキー割り当て処理。

