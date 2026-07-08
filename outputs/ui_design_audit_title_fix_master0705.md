# UI Design Audit: Title / Fix_Master0705

調査対象:

- `Assets/Scenes/Title.unity`
- `Assets/Scenes/FixScenes/Fix_Master0705.unity`
- `Fix_Master0705` で参照される `Assets/Prefabs/UI/OptionsCanvas.prefab`

目的:

- デザイナーが用意した UI デザインを正しく配置・実装できているか確認する。
- ボタンの hover / pressed / selected の扱いと実装方法のばらつきを整理する。
- 今後の正しい設計・実装方針を定義する。

## 現状サマリ

### Title シーン

- シーン直置きの `UnityEngine.UI.Button` が 18 個ある。
- 18 個すべてが `Transition = SpriteSwap`。
- そのうち 10 個は `highlighted / pressed / selected` に同じスプライトを設定している。
- 5 個のボタンには `TitleButtonState` が付いており、`Select` / `Not Select` 子オブジェクトを `SetActive` で切り替えている。
- つまり Title 内だけでも、Unity 標準 `SpriteSwap` と独自 `SetActive` 切り替えが混在している。

### Fix_Master0705 シーン

- シーン内の UI ボタンは主に `OptionsCanvas.prefab` 経由。
- `OptionsCanvas.prefab` には `UnityEngine.UI.Button` が 29 個ある。
- 29 個のうち 18 個が `Transition = SpriteSwap`、11 個が `Transition = None`。
- ただし prefab 内の Button に `highlighted / pressed / selected` 用スプライトは設定されていない。
- `OptionsCanvas` は `Assets/Figma/Pages/Finish.prefab` と `Assets/Figma/Screens/Option_Test.prefab` を参照している。
- `OptionsMainMenuSkin` / `OptionsFinishPromptSkin` が、Figma 由来の `Select` / `Not Select` 子オブジェクトに対して Button と `OptionsMenuButtonState` を runtime で後付けしている。

## 現在ある実装パターン

### 1. Unity 標準 Button + SpriteSwap

使われている場所:

- `Title.unity` の一部ボタン
- `TitleSaveListPanelDesign2Skin`

特徴:

- `Button.transition = SpriteSwap` または `ColorTint`
- `SpriteState.highlightedSprite / selectedSprite / pressedSprite` で見た目を変える
- Unity の選択・押下状態に乗れる

確認箇所:

- `Assets/Scripts/Title/TitleSaveListPanelDesign2Skin.cs:306`

課題:

- Title の一部では `highlighted / pressed / selected` がすべて同じスプライトになっているため、「hover と pressed の違い」が表現できない。
- `Select` / `Not Select` 子オブジェクト方式と混在すると、どちらが最終表示を決めるのか分かりにくい。

### 2. 独自コンポーネント + Select / Not Select 切り替え

使われている場所:

- `TitleButtonState`
- `OptionsMenuButtonState`

特徴:

- `IPointerEnterHandler` / `ISelectHandler` を拾う。
- `Select` と `Not Select` 子オブジェクトを `SetActive` で切り替える。
- Figma 由来の「状態ごとに絵が分かれている」構造と相性がよい。

確認箇所:

- `Assets/Scripts/Title/TitleButtonState.cs:62`
- `Assets/Scripts/Title/TitleButtonState.cs:82`
- `Assets/Scripts/UI/OptionsMainMenuSkin.cs:271`

課題:

- `TitleButtonState` と `OptionsMenuButtonState` がほぼ同じ責務を別クラスで持っている。
- `TitleButtonState.RefreshAll()` は毎回 `FindObjectsByType` しており、設計としては UI 共通部品にしづらい。
- pressed 状態が明示されていない。現在の `Select` は実質 hover/selected の絵で、押下中専用の絵がある場合に扱えない。

### 3. Runtime で Button を後付けする Skin クラス

使われている場所:

- `TitleMenuSkin`
- `OptionsMainMenuSkin`
- `OptionsFinishPromptSkin`
- `OptionsMenu.EnsureRuntimeButton`

特徴:

- Figma 由来オブジェクト名を探索する。
- 必要なら `Button` / `Image` / 状態コンポーネントを追加する。
- 見た目の Graphic と当たり判定 Graphic を runtime で設定する。

確認箇所:

- `Assets/Scripts/UI/OptionsMainMenuSkin.cs:93`
- `Assets/Scripts/UI/OptionsFinishPromptSkin.cs:107`
- `Assets/Scripts/UI/OptionsMenu.cs:2266`

課題:

- 同じような `EnsureButton` / `ConfigureRaycastGraphics` が複数クラスに重複している。
- `OptionsMainMenuSkin` は hover 音を登録するが、`OptionsFinishPromptSkin` や `OptionsMenu.BindButton` は click 音だけで hover 音が無い。
- `OptionsMenu.EnsureRuntimeButton` は `Transition = None` にするだけで、状態表示は別処理に依存している。

### 4. タブやページ状態を親ロジックで直接 SetActive

使われている場所:

- `OptionsMenu.SetOptionTabPair`
- `OptionsMenu.SetTabVisualState`

特徴:

- 選択中ページに応じて normal / selected のオブジェクトを切り替える。
- 「ボタンの hover」ではなく「現在ページの active 状態」を表す。

確認箇所:

- `Assets/Scripts/UI/OptionsMenu.cs:792`

課題:

- hover/pressed/selected と active/current-page が混ざりやすい。
- ボタンの状態コンポーネントとページ状態ロジックが同じ `Select` 表現を取り合う可能性がある。

## 問題の本質

現在は「状態」の意味が分かれていない。

- Hover: カーソルが乗っている。
- Pressed: 押している最中。
- Focused / Selected: キーボード・ゲームパッド操作で選択されている。
- Active: 現在開いているページ、現在選ばれているタブ。
- Disabled: 押せない。

これらが `Select` という名前、Unity の `selectedSprite`、`EventSystem.currentSelectedGameObject`、ページ状態の `SetActive` に散らばっているため、画面やパネルごとに挙動がずれている。

## 推奨設計

### 方針

Figma / デザイナー提供 UI は、Unity 標準 `SpriteSwap` を主役にせず、状態別の子オブジェクトを切り替える共通コンポーネントに寄せる。

理由:

- 既存 Figma prefab は `Select` / `Not Select` のような「状態ごとの見た目ノード」を持っている。
- ボタン絵が単一 Image ではなく複数レイヤーで構成される可能性が高い。
- デザイナーの配置を壊さず、状態ごとの見た目を丸ごと差し替えられる。

### 新しい共通コンポーネント

`UIButtonVisualState` を追加する。

責務:

- Unity の `Button` / `Selectable` からイベントを受ける。
- `normalRoot`
- `hoverRoot`
- `pressedRoot`
- `selectedRoot`
- `activeRoot`
- `disabledRoot`
- `hitTarget`
  を管理する。
- 未設定の状態は自然に fallback する。

Fallback:

- `pressedRoot` があれば pressed 中は pressed。
- `hoverRoot` があれば pointer hover 中は hover。
- `selectedRoot` があれば keyboard/gamepad focus 中は selected。
- `activeRoot` があればページ・タブの active 表示に使う。
- 無ければ `normalRoot`。

優先順位:

1. Disabled
2. Pressed
3. Active
4. Hover
5. Focused / Selected
6. Normal

注意:

- Active は「ページ状態」なので、単なる hover/selected より強い状態にする。
- UI によって「active 中でも hover で別絵にしたい」場合は設定フラグで切り替え可能にする。

### 命名規則

デザイナー素材・Prefab 内ノードは以下に統一する。

- `Normal`
- `Hover`
- `Pressed`
- `Selected`
- `Active`
- `Disabled`
- `HitArea`

既存の互換名:

- `Not Select` は `Normal` として扱う。
- `Select` は当面 `Hover` 兼 `Selected` として扱う。
- 旧名は段階的に置き換える。

### Button 設定ルール

デザイナー提供 UI:

- `Button.transition = None`
- `Button.targetGraphic = HitArea` または最大面積の Graphic
- 状態表示は `UIButtonVisualState` が管理する
- 子 Graphic の `raycastTarget` は基本 false、HitArea だけ true

単純な 1 枚絵ボタン:

- `SpriteSwap` を許可してよい
- ただし `hover / pressed / selected / disabled` のスプライトを明示的に持つ
- Figma 系 UI と同じ画面内で混ぜる場合は、なぜ SpriteSwap にするかコメントか設定で分かるようにする

### SFX 設定ルール

すべての操作可能 Button は共通 binder を通す。

- Click: `UIButtonSfxPlayer.Register(button)`
- Hover: `UIButtonSfxPlayer.RegisterHover(button)`

現状は `OptionsFinishPromptSkin` と `OptionsMenu.BindButton` が hover 登録していないため、統一対象。

### Page / Tab 状態

ページ・タブの選択状態は `Button` の `selected` と分離する。

- 現在ページを表すものは `SetActive(bool active)` のような API を `UIButtonVisualState` に持たせる。
- `EventSystem.currentSelectedGameObject` は keyboard/gamepad focus のためだけに使う。
- `OptionsMenu.SetOptionTabPair` は最終的に `UIButtonVisualState.SetActiveState(true/false)` に置き換える。

## 実装ステップ

### Step 1: 共通部品を追加

- `Assets/Scripts/UI/UIButtonVisualState.cs`
- `Assets/Scripts/UI/UIButtonBinder.cs`

`UIButtonBinder` の責務:

- Button の取得/追加
- targetGraphic / hit area 設定
- raycastTarget 整理
- SFX 登録
- `UIButtonVisualState` の自動設定

### Step 2: Options 系から置き換える

優先対象:

- `OptionsMainMenuSkin.EnsureButton`
- `OptionsFinishPromptSkin.EnsureButton`
- `OptionsMenu.EnsureRuntimeButton`

理由:

- `Fix_Master0705` の実 UI は `OptionsCanvas.prefab` 経由でここを通る。
- 重複コードが多く、共通化の効果が高い。

### Step 3: Title 系を置き換える

優先対象:

- `TitleButtonState`
- `TitleMenuSkin.EnsureButton`

方針:

- `TitleButtonState` は廃止または `UIButtonVisualState` の薄い互換 wrapper にする。
- `Title.unity` の Button は Figma 由来のものは `Transition = None` に揃える。
- Save slot のようなリスト項目は `ColorTint + SpriteSwap` を維持してもよいが、設計上「リスト項目の選択表示」として分離する。

### Step 4: Prefab / Scene の検査ツールを追加

Editor メニューまたはテストで以下を検出する。

- Figma 系 Button なのに `Transition != None`
- `Select` / `Not Select` があるのに `UIButtonVisualState` が無い
- `pressed` 用素材があるのに pressedRoot 未設定
- Button に hover SFX が未登録
- 複数 Graphic が raycastTarget true

## 判定基準

正しく実装されている状態:

- 同じ種類のボタンは同じコンポーネントで状態管理されている。
- hover と pressed が素材として存在する場合、それぞれ別状態として表示される。
- selected は「フォーカス」、active は「現在ページ/現在タブ」として分離されている。
- Figma 由来 UI は `Select` / `Not Select` の GameObject 切り替えを共通部品で扱う。
- 画面ごとに `EnsureButton` の独自実装が増えない。

## 優先度

1. `OptionsCanvas.prefab` と `Fix_Master0705` のメニュー UI
2. `Title.unity` のメインメニュー / 確認ダイアログ
3. `Title` のセーブスロット一覧
4. その他 HUD / EventCanvas / GameOver など

