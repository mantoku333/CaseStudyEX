# カメラ実装 現状分析と解決案

対象ブランチ: `future-fuyuno-CameraRework`
検証シーン: `Assets/Scenes/FixScenes/Fix_1115_CameraRework.unity`（`Fix_0925_NYAA.unity` のコピー）

---

## 1. 結論

今のカメラは **「部屋ごとに固定カメラを置く」方式** を選んだ結果、
**「部屋に収まらない部屋」と「部屋のつなぎ目」を全部スクリプトで手当てする** 構造になっている。

その手当てが積み上がって、カメラ関連コードは **8,384行**（うちエディタ拡張1,885行）。
うち `RoomCameraPortal.cs`(1,009行) と `VerticalRoomCameraPortal.cs`(893行) は
**286行が完全一致のコピー実装** で、やっていることは
「Cinemachine のブレンドを使わず、自前の SmoothDamp でカメラ位置とズームを補間する」。

つまり **Cinemachine の仕事を自前で再実装している**。ここが「納得いかない」の正体だと見ている。

そして、その自前実装を必要にした直接の原因が、おそらくこれ↓

```
CinemachineBrain.DefaultBlend = EaseInOut / Time 2.4秒
CustomBlends = なし
```

`Fix_0925_NYAA.unity:36014` の設定。部屋カメラ34本すべてが **2.4秒かけて** 切り替わる。
部屋移動のテンポに対して明らかに長く、「切り替わりがぬるい・見切れる・間に合わない」が出る。
ポータル群はその2.4秒をごまかすための装置に見える。

---

## 2. 現状の構造（実測）

### シーン内の実体数（`Fix_0925_NYAA.unity`）

| 要素 | 数 |
|---|---|
| `CinemachineCamera` | 39 |
| └ うち `CinemachinePositionComposer` 付き | 2 |
| └ うち `CinemachineFollow` 付き | 3 |
| **→ Body を持たない「置くだけ固定カメラ」** | **約34** |
| `CinemachineConfiner2D` | **0** |
| `RoomCameraTrigger` | 58 |
| `RoomCameraPortal` | 44 |
| `VerticalRoomCameraPortal` | 4 |
| `FallRoomCameraPortal` | 6 |
| `RoomCameraSwitchPortal` | 10 |
| `RoomCameraGate` | 0（**完全に未使用**・63行の死にコード） |

カメラ1本あたり平均1.1個のポータルが付いている。**部屋を1つ足すと、カメラ1本・トリガー1個・ポータル1〜2個を手で置く** 運用になっている。

### コード量

| ファイル | 行数 |
|---|---|
| `Assets/Editor/RoomCameraHierarchyTools.cs` | 1,885 |
| `Assets/Scripts/Camera/RoomCameraTrigger.cs` | 1,179 |
| `Assets/Scripts/Camera/RoomCameraPortal.cs` | 1,009 |
| `Assets/Scripts/Camera/VerticalRoomCameraPortal.cs` | 893 |
| `Assets/Scripts/Managers/CameraManager.cs` | 646 |
| その他 Camera/ 配下 | 約1,900 |
| **合計** | **8,384** |

---

## 3. 「納得いかない」原因の分解

### 原因1: ブレンド時間 2.4秒（最有力・最も安く直る）

`DefaultBlend` が 2.4秒、`CustomBlends` が未設定。
部屋カメラ34本が一律2.4秒でブレンドする。

- 部屋を速く通り抜けると、ブレンドが終わる前に次の部屋に入る → カメラが追いつかず中途半端な位置で漂う
- `RoomCameraPortal` の `targetRoomWeight = 0.45`、`smoothTime = 0.15` などの自前補間は、この2.4秒を回避するための上書き
- `CameraManager.SuppressFollowCameraCenterOnActivateForFrames()`（`CameraManager.cs:345`）という
  「3フレームだけ CenterOnActivate を切る」ハックも、ブレンド制御が効かないことへの対処

**この1設定だけでも体感は大きく変わる。先に切り分けるべき。**

### 原因2: Cinemachine のブレンドを使わず自前補間している

`RoomCameraPortal.UpdateTransitionCamera()`（`RoomCameraPortal.cs:210`）が、
ランタイム生成した `transitionCamera` の `transform.position` と `Lens.OrthographicSize` を
`Vector3.SmoothDamp` / `Mathf.SmoothDamp` で毎フレーム直接書いている。

これが生んでいる副作用:

- `KeepPlayerInsideView()`、`ClampBossRoomPreview()`、`ClampBossRoomViewEdge()`、
  `ApplyTransitionHeightLock()` …**「見切れないように自分で押し戻す」コードが大量に必要**
- 補間が `Update()` で走る一方、Brain は `UpdateMethod = SmartUpdate` / `BlendUpdateMethod = LateUpdate`。
  プレイヤーは Rigidbody2D 駆動なので vcam は FixedUpdate 側で評価されうる。
  **書き込みタイミングと評価タイミングが一致しておらず、1フレームずれ＝微細なジッタの温床**
- Cinemachine 側の damping / noise / impulse と自前 SmoothDamp が二重にかかる

### 原因3: ポータルが5種類に分裂し、しかも中身がコピー

| クラス | 役割 | 配置数 |
|---|---|---|
| `RoomCameraPortal` | 横つなぎ + 自前ブレンド | 44 |
| `VerticalRoomCameraPortal` | 縦つなぎ + 自前ブレンド | 4 |
| `FallRoomCameraPortal` | 落下で切り替え | 6 |
| `RoomCameraSwitchPortal` | テレポート時に即切り替え | 10 |
| `RoomCameraGate` | 片方向切り替え | 0 |

`RoomCameraPortal` と `VerticalRoomCameraPortal` は **286行が一字一句同一**。
同一化しているメソッド例:

```
CameraPose / ResolveRoomPose / ResolveDefaultRoomBasePose / TryGetCurrentCameraPose
ResolveTransitionPose / TryResolveDefaultFollowPose / TryFindFollowCamera
TryApplyPositionComposerPose / TryApplyDirectFollowPose / ResolveFollowOffsetAtEntry
AlignDefaultFollowCameraBeforeCommit / IsPlayerFullyInsideRoom / IsBoundsInsideRoom
StopTransitionCamera / GetTransitionCamera / SetTransitionCameraSize
ResolveCurrentRoom / ResolveRoomOnPortalSide
```

縦と横の違いは本来「軸」というパラメータ1つ。クラスを分ける理由がない。
**片方を直してもう片方を直し忘れる** 事故が起きる形になっている。

### 原因4: カメラの「所有権」が三重になっている

今、誰がアクティブカメラを決めるかが3系統ある:

```
① RoomCameraTrigger の static 群
     _activeTrigger / portalCameraOwner / portalCameraDestination
     → LateUpdate の ValidateActiveRoomAgainstPlayer() が毎フレーム
       「プレイヤーを含む最小の部屋」を探して勝手に再アクティブ化する

② portal hold（HoldCameraForPortal / CommitCameraFromPortal）
     → ①の毎フレーム監視を止めるためのロック機構

③ EncounterCameraOwnership（Brain の SetCameraOverride を直接使う）
     → ①②を全部無視して上書きする
```

②は①を抑えるために後付けされ、③は①②を無視するために作られている。
**「仕組みを足して前の仕組みを黙らせる」構造** なので、新しい演出を入れるたびに4つ目が必要になる。

実装上の歪みも出ている:

- `RoomCameraTrigger` は static を持つのに `[RuntimeInitializeOnLoadMethod]` でのリセットが無い
  （`EncounterCameraOwnership.cs:48`、`FollowCameraFacingBias.cs:41` にはある）。
  現在は `m_EnterPlayModeOptions: 0` なのでドメインリロードが走り顕在化していないが、
  **「Reload Domain」をオフにした瞬間に前回の `_activeTrigger` が残って壊れる**
- `RoomCameraPortal.UpdateTransitionCamera()` が自分の `OnDisable()` を手で呼んで状態を捨てている（`RoomCameraPortal.cs:217`）

### 原因5: 設計書どおりに作られていない

`md/CAMERA_SYSTEM_DESIGN.md` と実装がずれている。

| 設計書の記述 | 実装 |
|---|---|
| `RoomCameraPortal` は Priority を早めに切り替えるだけ | 1,009行の自前ブレンド機構 |
| `RoomCameraPortal.Axis = X / Y` | **そのフィールドは存在しない**（クラスが縦横で分裂） |
| `Switch To Opposite Room On Enter` | 存在しない |
| `Restore Entry Room When Backed Out` | 存在しない |
| ボス部屋は `CinemachineConfiner2D` で範囲制限 | **シーン内に Confiner2D が0個** |
| `RoomCameraGate` は片方向用に残す | 配置数0 |

**設計書の「Confiner2D で範囲を制限する」という一番効く部分が、実装で採用されていない。**
その代わりに「見切れ防止」を各ポータルが手書きしている。原因2の根っこがここ。

---

## 4. 解決案

### 案A: ブレンド設定の切り分け（まず最初にやる / 半日）

コードを1行も触らずに、原因1を潰して体感を測る。

1. `CinemachineBrain.DefaultBlend` を `EaseInOut 2.4s` → `EaseInOut 0.3〜0.5s` に
2. `CinemachineBlenderSettings` アセットを作り `CustomBlends` に設定
   - 部屋 → 部屋: 0.25〜0.4s
   - 部屋 → ボス部屋: 0.8s（演出として溜める）
   - 任意 → `CN_EventCam`: Cut もしくは長め（演出ごと）
3. この状態で、各ポータルの自前ブレンドを**一時的に無効化**して走り回る

**判定**: Aだけで許容できる絵になるなら、原因2〜3の1,900行は丸ごと捨てられる。
許容できないなら案Bへ。**この切り分けを先にやらないと、案Bが必要かどうか判断できない。**

### 案B: 固定カメラ34本をやめ、Confiner2D 1本に寄せる（本命 / 3〜5日）

設計書が本来意図していた形に戻す。

```
CN_GameplayCam （1本だけ）
  CinemachineCamera
  CinemachinePositionComposer   ← プレイヤー追従と画面内位置
  CinemachineConfiner2D         ← 部屋の外を映さない保証
  FollowCameraFacingBias        ← 既存のまま使える
```

`RoomCameraTrigger` の役割を **「カメラを切り替える」から「今の部屋の枠を渡す」** に変える:

```csharp
// 部屋に入ったら、カメラを切り替えるのではなく境界とレンズだけ差し替える
confiner.BoundingShape2D = room.CameraBounds;   // 部屋ごとの Collider2D
lens.OrthographicSize    = room.OrthographicSize;
```

これで何が消えるか:

| 消えるもの | 理由 |
|---|---|
| 固定カメラ約34本 | 部屋が1画面に収まるなら Confiner2D が自動で中心に固定する＝固定カメラと同じ絵になる |
| `RoomCameraPortal` の自前ブレンド（約700行） | カメラが1本なのでそもそも切り替えが発生しない。移動は Composer の damping が担う |
| `KeepPlayerInsideView` / `ClampBossRoomPreview` / `ClampBossRoomViewEdge` | 見切れ防止は Confiner2D の保証に置き換わる |
| `RoomCameraTrigger` の horizontalFollow モード（約150行） | 横長部屋は Confiner2D が自然に扱う |
| portal hold 機構 | 切り替えが無いのでロックする対象が無い |

残すもの:

- `CN_EventCam` / ボス部屋カメラ / Timeline 用カメラ → ここだけ従来どおり Priority 切り替え
- `DualTargetCameraTarget`（ボス部屋）→ Confiner2D と組ませる（設計書どおり）
- `CameraManager` の Impulse（画面揺れ）→ そのまま使える

**リスク**: 「この部屋はこの構図で見せたい」という手付けの絵が34本ぶん失われる。
→ 対策として、構図を固定したい部屋だけ `RoomCameraTrigger` に
`overrideOrthographicSize` / `targetOffsetY`（既に実装済み）を指定する。
既存の固定カメラの Transform から初期値を自動抽出するエディタスクリプトを書けば移行は機械的にできる。

### 案C: ポータル5種を1種に統合（案Bと並行 / 1〜2日）

案Bを採っても「ここでは早めに切り替えたい」要求は残る。そのとき:

```
RoomCameraPortal  （軸は enum Axis { X, Y } のフィールドに戻す）
  ├ 旧 VerticalRoomCameraPortal → Axis = Y
  ├ 旧 FallRoomCameraPortal     → Axis = Y + oneWay = true
  ├ 旧 RoomCameraSwitchPortal   → instant = true
  └ 旧 RoomCameraGate           → 削除（配置数0）
```

設計書に書かれていた `Axis` 設計に戻すだけ。**1,900行 → 300行程度**になる見込み。

### 案D: 所有権を `CameraDirector` 1箇所に集約（案B後 / 1〜2日）

原因4の三重権限を、優先度付きリクエストのスタック1本にする。

```csharp
// 誰が要求しても同じ入口を通る。勝つのは常に最高優先度の1件。
int handle = CameraDirector.Request(camera, CameraPriority.Encounter, blend);
CameraDirector.Release(handle);

enum CameraPriority { Room = 0, Portal = 10, Encounter = 20, Story = 30 }
```

- `RoomCameraTrigger` の static 3種と毎フレーム監視を廃止
- `EncounterCameraOwnership` は `CameraPriority.Encounter` のリクエスト1件に縮小
- `[RuntimeInitializeOnLoadMethod]` でのリセットを1箇所に持たせる

---

## 5. 推奨する進め方

```
案A（半日）          → ブレンド設定だけ直して走り回り、体感を測る
  ↓ それで足りなければ
案B（3〜5日）        → Confiner2D 1本化。ここが本命
  ↓
案C（1〜2日）        → ポータル統合（案Bで大半が消えるので実質は整理のみ）
  ↓
案D（1〜2日）        → 所有権集約。演出追加のたびに壊れる構造を止める
```

案Aを飛ばして案Bから始めるのは勧めない。
**2.4秒のブレンドが原因だった場合、案Bの工数が丸ごと無駄になる**ため。

## 6. 検証手順

案A/Bのどちらでも、同じ基準で比較する。

1. `Fix_1115_CameraRework.unity` を開く
2. 計測ルートを固定する（例: `101 → 108 → 112 → 201 → 221_Boss`）
3. 各ルートで見るもの:
   - 部屋をまたぐ瞬間にプレイヤーが画面端に寄りすぎないか
   - 全力で走り抜けたときカメラが追いつくか
   - 引き返したときにカメラが往復しないか（ハンチング）
   - 落下中に着地点が見えるか
   - ボス部屋入場時にプレイヤーとボスが両方映るか
4. 既存の `SROptions.Camera.cs` にアクティブカメラ名の表示があるので、
   切り替わりの瞬間を目視で特定できる

> 注: `md/CAMERA_SYSTEM_DESIGN.md` にあるとおり、コンパイル確認は Unity Editor 上で行う。
> `dotnet build Assembly-CSharp.csproj` はエラー0件でも失敗表示になる。

## 7. 今回の作業環境

| 項目 | 内容 |
|---|---|
| ブランチ | `future-fuyuno-CameraRework`（`Develop` から作成） |
| 検証シーン | `Assets/Scenes/FixScenes/Fix_1115_CameraRework.unity` |
| シーン GUID | `5e4ec46a5e834519a001526c7cb6ff4c`（新規発行） |
| Build Settings | 上記シーンを追記登録済み（末尾追加なので既存インデックスに影響なし） |
| 元シーン | `Fix_0925_NYAA.unity` は未変更（作業前の未コミット変更もそのまま保持） |

元シーンには触らないので、案A/Bの絵を元シーンと並べて比較できる。
