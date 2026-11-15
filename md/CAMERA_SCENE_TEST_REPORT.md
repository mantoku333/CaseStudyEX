# カメラ実測レポート（シーンデータ検証）

対象: `Assets/Scenes/FixScenes/Fix_1115_CameraRework.unity`
方法: シーンYAMLから全カメラ・部屋境界・ポータルを抽出し、座標・視野・ブレンド時間を数値検証
検証対象数: カメラ39本 / `RoomCameraTrigger` 58個 / ポータル64個

> 注: Unity Editor が本プロジェクトを開いたまま稼働中だったため、バッチモードでの
> 自動プレイテストは実行していない（プロジェクトロックが競合するため）。
> 代わりにシーンデータの数値検証を行った。実際の操作感の確認は Editor 上で必要。

---

## 結論: 3つの訴えは全部データで裏が取れた

| 訴え | 原因 | 実測値 |
|---|---|---|
| 画面がグラングラン移動する | ① ブレンド2.4秒で38u移動<br>② 長廊下カメラの7部屋共有 | ① プレイヤーの**3.4倍速**で2.4秒<br>② 最大**1,064u**の横滑り + **131u**の縦ワープ |
| プレイヤーが見切れる | ③ フォロー→固定の19u追従遅れ<br>④ Confiner2D不在 | ③ 画面半幅21.3uに対し**19.2u**のズレ<br>④ **31/32本**が部屋外を2u以上映す |
| 切り替えタイミングが変 | ⑤ 部屋境界の重なり<br>⑥ フラグ矛盾 | ⑤ **65ペア**が重複、うち**8ペア**は面積差5%未満<br>⑥ **4部屋**がフラグ二重ON、**1部屋**は専用カメラが死んでいた |

---

## ① ブレンド 2.4秒 ＝ 「グラングラン」の主犯

```
CinemachineBrain.DefaultBlend = EaseInOut / 2.4秒   （CustomBlends なし）
PlayerStatsData.moveSpeed     = 7 u/s （滑空 10 u/s）
部屋の標準幅                   = 約38u
```

隣の部屋のカメラは、ちょうど部屋1つ分 = **38u 離れている**。
それを 2.4秒で横断するので:

| | 値 | プレイヤー比 |
|---|---|---|
| カメラ平均速度 | 15.8 u/s | **2.3倍** |
| カメラピーク速度（EaseInOut） | 23.8 u/s | **3.4倍** |

**自分より3倍速く画面が横に流れる状態が2.4秒続く。** これがグラングランの正体。

さらに、ブレンド中にプレイヤーは **16.8u（部屋幅の44%、滑空なら63%）** 進む。
つまりブレンドが終わる前に次の部屋に着いてしまうので、**カメラの目標地点が常に古い**。

実測した跳び量（固定カメラ同士のポータル10本）:

| ポータル | 距離 | 2.4秒での速度 |
|---|---|---|
| Portal_205_206 | 48.1u | 20.0 u/s |
| Portal_201_202 | 38.4u | 16.0 u/s |
| Portal_306_307 | 38.3u | 16.0 u/s |
| Portal_212_213 | 38.3u | 15.9 u/s |
| Portal_108_109 | 37.8u | 15.8 u/s |

最大 48.1u / 平均 33.6u / 中央 38.0u。

---

## ② 長廊下カメラを7部屋が共有している ＝ 最悪のケース

**これが一番酷い。**

`CN_LongHall_HorizontalFollow`（1本のカメラ）を、マップ上で **最大1,064u離れた7部屋** が共有している。

| 部屋 | 中心座標 | サイズ |
|---|---|---|
| Col_107 | (135.1, -52.5) | 108.4 x 20.3 |
| Col_105 | (185.9, -12.5) | 163.1 x 20.3 |
| Col_102 | (168.5, 8.0) | 76.0 x 20.3 |
| Col_301 | (821.7, 7.1) | 58.7 x 20.3 |
| Col_311 | (1012.7, 118.9) | 58.1 x 20.4 |
| Col_322 | (1138.9, 119.1) | 38.2 x 20.7 |
| Col_321 | (1186.0, 119.2) | 56.2 x 19.8 |

部屋間距離: Col_107 → Col_321 で **1,064.8u**、Col_105 → Col_311 で 837.2u。

### 何が起きるか

`RoomCameraTrigger.ActivateHorizontalFollowCamera()`（[RoomCameraTrigger.cs:491](Assets/Scripts/Camera/RoomCameraTrigger.cs:491)）:

```csharp
horizontalFollowVelocityX = 0f;
_horizontalFollowCamera.Follow = null;
_horizontalFollowCamera.Target.TrackingTarget = null;   // ← ここ
UpdateHorizontalFollowCamera();
```

そして `UpdateHorizontalFollowCamera()`（[RoomCameraTrigger.cs:522](Assets/Scripts/Camera/RoomCameraTrigger.cs:522)）:

```csharp
currentPosition.x = Mathf.SmoothDamp(..., _horizontalFollowSmoothTime);  // X は補間
currentPosition.y = targetPosition.y;                                    // Y は即時代入
```

結果:

- **同一のカメラオブジェクトなので、Cinemachine から見れば「カメラは切り替わっていない」。
  ブレンドが一切かからない。** Brain の設定は無関係。
- X は `smoothTime = 0.15` の生の SmoothDamp → 最大 **1,064u を約0.4秒で滑る（≒2,600 u/s）**
- Y は**即時代入**なので、Col_105(-12.5) → Col_311(118.9) で **131u を1フレームで縦ワープ**

ブレンド時間をどう調整しても、この7部屋だけは直らない。

### しかもカメラは最初から正しく設定されていた

`CN_LongHall_HorizontalFollow` の中身:

```yaml
CinemachineCamera:
  Target:
    TrackingTarget: {fileID: 1955794425}   # ← Player プレハブ。正しく割当済み
  Lens:
    OrthographicSize: 12.4

CinemachinePositionComposer:
  Composition:
    ScreenPosition: {x: 0, y: 0.3}
    DeadZone:    { Enabled: 1, Size: {x: 0.12, y: 0.121} }
    HardLimits:  { Enabled: 1, Size: {x: 0.8,  y: 0.8  } }
  CenterOnActivate: 1
  TargetOffset: {x: 0, y: 1.3, z: 0}
  Damping: {x: 1, y: 1, z: 1}
```

**デッドゾーン付きの減衰追従カメラとして完璧に組んである。**
そして `RoomCameraTrigger` が実行時に `TrackingTarget = null` を代入して、
この設定を全部無効化し、自前の SmoothDamp で置き換えている。

→ **Cinemachine が持っている機能を、わざわざ壊してから劣化再実装している。**

---

## ③ フォローカメラ → 固定カメラ の追従遅れ ＝ 「見切れ」

ポータル64本のうち **32本** が「デフォルトフォロー側 ↔ 固定カメラ側」をつないでいる。

- フォローカメラは**プレイヤーの位置**にいる
- 固定カメラは**部屋の中心**にいる
- 両者のズレは最大「固定部屋の半幅」

| ポータル | 固定側 | 最大ズレ |
|---|---|---|
| Portal_204_205 | Col_205 | **29.1u** |
| Portal_109_111 | Col_109 | 19.4u |
| Portal_208_209 | Col_208 | 19.3u |
| Portal_101_102 | Col_101 | 19.1u |

平均 18.5u / 最大 29.1u。

**画面の半幅は `OrthographicSize 12 × (16/9) = 21.3u`。**
つまりズレ19.2uは、**画面半幅の90%**。プレイヤーが画面のほぼ端まで追いやられる。

逆方向（固定 → フォロー）がさらに悪い: カメラは部屋中心から 19.2u を 2.4秒で詰める = 8.0 u/s。
プレイヤーは 7.0 u/s で遠ざかるので、**実質の詰め速度は 1.0 u/s**。
追いつくのに約19秒かかるため、その間プレイヤーは画面端に張り付いたまま = **見切れ**。
滑空（10 u/s）だと **カメラより速いので永久に追いつけない**。

---

## ④ Confiner2D が1個も無い ＝ 部屋の外が見える

設計書 `md/CAMERA_SYSTEM_DESIGN.md` は「ボス部屋カメラには `CinemachineConfiner2D` を付ける」と
明記しているが、**シーン内の Confiner2D は0個**。

固定カメラ32本のうち **31本** が、担当部屋の外を2u以上映している:

| 部屋 | カメラ | 部屋外X | 部屋外Y |
|---|---|---|---|
| Col_106 | CN_106 | **12.5u** | 0.0 |
| Col_112 | CN_112 | **12.3u** | 0.2 |
| Col_205 | CN_205 | 1.3 | **7.5u** |
| Col_309 | CN_309 | 3.2 | 1.7 |

Col_106 は部屋幅 14.1u に対して視野 35.6u。**画面の2/3が部屋の外**。
Col_112 も部屋幅 9.0u に対して視野 32.0u。

逆に、部屋が視野より大きくて**プレイヤーが画面外に出られる**部屋も2つある:

| 部屋 | 部屋サイズ | 視野 | はみ出し |
|---|---|---|---|
| Col_112 | 9.0 x **20.6** | 32.0 x 18.0 | 縦 **2.8u** |
| Col_109 | 38.8 x **28.7** | 42.7 x 24.0 | 縦 **2.5u** |

Col_112 / Col_109 では、**プレイヤーが上下に動くと確実に画面から消える。**

---

## ⑤ 部屋境界が65ペアで重なっている ＝ 「切り替えタイミングが変」

`RoomCameraTrigger.ValidateActiveRoomAgainstPlayer()` は毎 LateUpdate で
`FindSmallestRoomContaining()` を呼び、**「面積が一番小さい部屋」** をアクティブにする。

重なっている部屋ペア: **65 / 全1653ペア**。
うち **面積差5%未満が8ペア**:

| 部屋A | 部屋B | 面積A | 面積B | 面積差 | 重なり |
|---|---|---|---|---|---|
| Col_213 | Col_212 | 774 | 773 | **0.1%** | 0.3 x 19.8 |
| Col_306 | Col_307 | 791 | 789 | **0.3%** | 0.4 x 20.2 |
| Col_304 | Col_307 | 791 | 789 | **0.3%** | **37.7 x 0.2** |
| Col_218 | Col_217 | 820 | 827 | 0.9% | 0.1 x 21.3 |
| Col_201 | Col_202 | 786 | 775 | 1.4% | 0.2 x 20.2 |
| Col_306 | Col_305 | 791 | 806 | 1.9% | **37.7 x 0.3** |
| Col_215 | Col_216 | 754 | 769 | 2.0% | **37.9 x 0.8** |

意味するところ:

- **Col_213 と Col_212 の勝敗は「面積1平方ユニットの差」で決まる。**
  コライダーを少し触っただけで、カメラの挙動が逆転する。
- `Col_215/Col_216`、`Col_304/Col_307`、`Col_306/Col_305` は
  **部屋の全幅（37〜38u）にわたる高さ0.2〜0.8uの帯** で重なっている。
  床の高さがちょうどこの帯に入ると、**歩いている間ずっと両方の部屋に属する**。
- 判定は「**どちらから入ってきたか** と無関係」に面積だけで決まるので、
  入った部屋ではなく隣の部屋のカメラが選ばれることがある。これが「タイミングが変」。
- `Col_301/Col_223` は **2.9u 幅 x 20.3u 高** の縦帯で重なっている（最大の重なり面積59.1）。

---

## ⑥ 設定フラグの矛盾

### 4部屋がフラグ二重ON

`Col_311` / `Col_301` / `Col_102` / `Col_321` は
`_useDefaultCameraWhenEntered` と `_useHorizontalFollowCameraWhenEntered` が**両方ON**。

`ActivateCamera()` は horizontal を先に判定するので horizontal が勝つ。
しかし別の場所では `IsDefaultTrigger` を見ている:

- `DeactivateAllRoomCamerasExcept()` → `IsDefaultTrigger && !HasRoomCamera` なら**スキップする**
- `RoomCameraPortal.ShouldBlendDefaultFollowZoom()` → `UsesDefaultCameraWhenEntered` を見る

**同じ部屋が、通った経路によって別の種類の部屋として扱われる。**

### Col_322 の専用カメラが死んでいた

`Col_322` は `_roomCamera = CN_322` を持っているのに、
`_useHorizontalFollowCameraWhenEntered = 1` のため `ActivateCamera()` が
horizontal 経路に入り、**CN_322 は一度も使われていなかった**。

---

## 今回シーンに適用した修正（コードは未変更）

`Fix_1115_CameraRework.unity` のみ、**18行の値変更**。
ドキュメント数4588・行数1030765は変更前と完全一致（構造破壊なし）。

### 修正1: ブレンド時間 2.4s → 0.35s

```diff
  CinemachineBrain.DefaultBlend:
    Style: 1 (EaseInOut)
-   Time: 2.4
+   Time: 0.35
```

| | 修正前 | 修正後 |
|---|---|---|
| ブレンド中にプレイヤーが進む距離 | 16.80u（部屋幅の44.2%） | **2.45u（6.4%）** |
| default→固定の19.2u追従速度 | 8.0 u/s（プレイヤー7.0に対し実質1.0） | **54.9 u/s** |

※ 38u を 0.35秒 = 108 u/s なので、部屋↔部屋は「速いスナップパン」になる。
2.4秒の漂流よりは確実に良いが、好みに応じて `0.25〜0.5` で調整するか、
部屋↔部屋だけ `Style: 0 (Cut)` にする選択もある（`CinemachineBlenderSettings` で
ペアごとに指定可能。固定↔フォローは短いブレンドのままにしたいのでアセット分割が要る）。

### 修正2: 長廊下カメラの共有をやめ、Cinemachine 本来の追従に戻す

7部屋すべてで `_useHorizontalFollowCameraWhenEntered: 1 → 0`。
`_roomCamera` 未設定だった6部屋には共有カメラをそのまま `_roomCamera` として割当。

これで `ActivateCamera()` は通常の `_roomCamera` 経路（**Priority を上げるだけ**）に入り:

- `TrackingTarget`（Player）が null にされない → **Player を追従する**
- `CinemachinePositionComposer` の DeadZone / HardLimits / Damping が**機能する**
- `transform.position` への手動書き込みが消える → **1,064uの横滑りと131uの縦ワープが消滅**

`Col_322` は専用カメラ `CN_322` が正しく使われるようになる。

### 修正3: フラグ矛盾の解消

`Col_311` / `Col_301` / `Col_102` / `Col_321` の
`_useDefaultCameraWhenEntered: 1 → 0`（horizontal 側を正とし、二重指定を解消）。

---

## 修正でも直らない残課題（コード/アセット変更が必要）

| 課題 | 必要な変更 | 影響範囲 |
|---|---|---|
| 部屋境界65ペアの重なり | コライダーの再調整 | シーンのみ（ただし58部屋の手作業） |
| Col_112 / Col_109 で縦に見切れる | Confiner2D 追加 or ズーム調整 | シーンのみ |
| 31/32本が部屋外を映す | Confiner2D 追加 | シーンのみ（コンポーネント追加） |
| 「面積最小の部屋が勝つ」判定 | `FindSmallestRoomContaining` の見直し | **コード（全シーン影響）** |
| フォロー↔固定の19u追従遅れ | 固定カメラをやめ Confiner2D 1本化（案B） | **コード（全シーン影響）** |
| ポータル5種のコピー実装 | 1種に統合（案C） | **コード（全シーン影響）** |

Confiner2D の追加は**シーンへのコンポーネント追加だけ**で済み、他シーンに影響しない。
効果が大きいので、次に手を付けるならここを勧める。

---

## Editor で確認する手順

Unity がプロジェクトを開いたままシーンファイルを書き換えたので、まず反映させる。

1. Unity に戻る（シーンファイルの変更を検出して再インポートされる）
   - `Fix_1115_CameraRework.unity` を**開いていた場合**は、保存せずに開き直す
2. `Fix_1115_CameraRework.unity` を開いて再生
3. 比較ルート: `101 → 102 → 103 → 104 → 105 → 107`（長廊下を含む）
   と `201 → 202 → 203 → 204 → 205`（最大跳び量48.1uのPortal_205_206を含む）
4. 元シーン `Fix_0925_NYAA.unity` は未変更なので、同じルートで直接比較できる

重点的に見る箇所:

| 箇所 | 見るもの |
|---|---|
| Col_105 / Col_107（長廊下） | 横滑りと縦ワープが消えたか |
| Col_322 | CN_322 の構図になったか（従来は長廊下カメラ） |
| Portal_205_206 | 48.1uの跳びが許容範囲か（0.35秒が速すぎないか） |
| Col_112 / Col_109 | 縦の見切れ（**未修正**。再現するはず） |
| Col_215/216、Col_304/307 境界 | 重なり帯でのちらつき（**未修正**） |
