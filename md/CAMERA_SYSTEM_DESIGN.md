# Camera System Design

## 目的

この資料は、現在のカメラ実装と、今後のエリア管理・ボス部屋対応を分かりやすく整理するためのものです。

カメラは大きく分けて次の4種類で考えます。

1. 通常フォローカメラ
2. 固定エリアカメラ
3. エリア間ポータルによるカメラ切り替え
4. ボス部屋用の制限付きデュアルターゲットフォローカメラ

## 基本方針

カメラは `CinemachineCamera` の Priority を切り替えて制御します。

ただし、すべてを固定カメラで解決しようとすると、縦長・横長の大きな部屋で見切れます。  
そのため、エリアの種類ごとにカメラ方式を分けます。

```text
廊下・通常移動エリア
  -> 通常フォローカメラ

小さめの部屋・1画面で見せたい部屋
  -> 固定エリアカメラ

エリアとエリアのつなぎ目
  -> RoomCameraPortal で早めに切り替え

ボス部屋・大きい特殊部屋
  -> 制限付きデュアルターゲットフォローカメラ
```

## 推奨ヒエラルキー

既存の配置ルールに合わせて、`Stage > Area > エリア名` をそのまま使います。

```text
Stage
  Area
    101
      Col_101
        RoomCameraTrigger
        BoxCollider2D

      CN_101
        CinemachineCamera

      Gates
        Gate_101_to_102
          RoomCameraGate
          BoxCollider2D

        Gate_101_to_108
          RoomCameraGate
          BoxCollider2D

    102
      Col_102
      CN_102

    Portal_101_102
      RoomCameraPortal
      BoxCollider2D
```

### 命名ルール

```text
エリア本体: 101 / 102 / 201 / 202 / 301 ...
エリア範囲: Col_101
エリアカメラ: CN_101
エリア間ポータル: Portal_101_102
```

古い `1-1` 形式は、エディタメニュー実行時に `101` 形式へ寄せます。  
`1-1 -> 101`、`1-11 -> 111`、`2-22 -> 222`、`3-4 -> 304` という変換です。

`Col_` はカメラエリア判定用です。  
`CN_` はそのエリアに対応する CinemachineCamera です。  
`Portal_` はエリア間のカメラ移動用コライダーです。

## 通常フォローカメラ

通常移動では `CN_FollowCam` を使います。

担当コンポーネント:

- `CameraManager`
- `FollowCameraFacingBias`

### CameraManager

`CameraManager` は Singleton として存在し、次のカメラを名前で探します。

```text
CN_FollowCam
CN_DirectFollowCam
```

`ToggleCamera()` で Priority を切り替えます。

```text
CN_FollowCam active:
  CN_FollowCam = 10
  CN_DirectFollowCam = 0

CN_DirectFollowCam active:
  CN_FollowCam = 0
  CN_DirectFollowCam = 10
```

### FollowCameraFacingBias

通常フォローカメラに自動付与されます。

プレイヤーが右を向いて動いているなら、少し右を見る。  
左を向いて動いているなら、少し左を見る。  
落下中は下方向を少し見る。

つまり、プレイヤーの進行方向や落下方向を自然に見せるための補助です。

## 固定エリアカメラ

小さな部屋や、1画面で構図を固定したい場所では固定エリアカメラを使います。

担当コンポーネント:

- `RoomCameraTrigger`

```text
101
  Col_101
    RoomCameraTrigger
    BoxCollider2D

  CN_101
    CinemachineCamera
```

`RoomCameraTrigger` は、プレイヤーがそのエリアに入ったら対応する `CN_101` の Priority を上げます。

```text
activePriority = 20
inactivePriority = 0
```

### なぜ Col に付けるのか

`RoomCameraTrigger` は Trigger イベントを受ける必要があります。  
そのため、基本的には `BoxCollider2D` がある `Col_101` に付けます。

エリア本体 `101` は整理用の親オブジェクトとして使います。

## エリア間ポータルによる切り替え

以前の問題は、エリア Collider に触れただけで切り替えていたため、出口ではない壁際でも隣カメラに切り替わることでした。

その対策として、エリアとエリアの間だけに `RoomCameraPortal` を置きます。

```text
Area
  101
  102
  Portal_101_102
    RoomCameraPortal
    BoxCollider2D
```

`RoomCameraPortal.roomA` と `RoomCameraPortal.roomB` に、つながっている2つの `RoomCameraTrigger` を指定します。

```text
Portal_101_102
  roomA = Col_101 の RoomCameraTrigger
  roomB = Col_102 の RoomCameraTrigger
```

これにより、エリアの中ではなく、つなぎ目として認めた場所だけでカメラが切り替わります。

### ポータル方式のメリット

- 出口ではない辺に近づいても隣カメラへ切り替わらない
- ポータルの幅を広げれば、見切れる前にカメラを先行切り替えできる
- 戻った場合は元のエリアカメラへ戻せる
- 片方向ゲートを2個置かなくてよい
- ヒエラルキー上で「どこで切り替わるか」が見える
- 後から調整しやすい

`RoomCameraGate` は、片方向だけの特殊な切り替えが必要な場所に残すためのものです。  
通常の部屋移動は `RoomCameraPortal` を優先します。

## エディターツール

追加した Editor メニュー:

```text
GameObject > Camera Area > Setup Camera Area
```

### Setup Camera Area

選択状態によって動きが変わる、1ボタン運用のセットアップです。

- `Area` を選んで実行: 直下のエリアをまとめてセットアップ
- `101` など個別エリアを選んで実行: 選択したエリアをセットアップ
- 複数エリアを選んで実行: 選択したエリアだけまとめてセットアップ
- 未選択で実行: 新しいエリアを1個作成

```text
101 を選んで実行
```

自動で探す/作るもの:

```text
Col_101
CN_101
RoomCameraTrigger
Gates/Gate_101_to_102
```

既に `Col_101` や `CN_101` がある場合は、それを使います。

```text
Area
  101
  102
  103
```

古い `1-1` 形式が残っている場合は、このメニュー実行時に `101` 形式へ寄せます。

```text
305
  Col_305
  CN_305
  Gates
    Gate_305_to_304
```

`304` を選んでいる場合は、次の名前として `305` を作ります。  
選択がない場合は、既存の `101`, `222`, `304` などを見て次の番号を作ります。

### Create Camera Portal

エリア間に置く、双方向のカメラ移動用コライダーを作ります。

```text
GameObject > Camera Area > Create Camera Portal
```

おすすめ手順:

```text
101 と 102 を2つ選択
↓
Create Camera Portal
↓
Area 直下に Portal_101_102 が作られる
```

生成される形:

```text
Area
  101
  102
  Portal_101_102
    RoomCameraPortal
    BoxCollider2D
```

`RoomCameraPortal` には `Room A` と `Room B` を設定します。  
2つのエリアを選択して作った場合は自動で入ります。

横につながるエリアなら `Axis = X`、上下につながるエリアなら `Axis = Y` にします。  
`Switch To Opposite Room On Enter` がオンのとき、プレイヤーがポータルに入った瞬間に反対側のエリアカメラへ切り替わります。  
つまり、横移動なら BoxCollider2D の X サイズを広げるほど、早めにカメラが切り替わります。上下移動なら Y サイズを広げます。

`Restore Entry Room When Backed Out` がオンなら、ポータルに入ったあと進まずに引き返したときだけ、元のエリアカメラへ戻ります。  
これにより、101 と 102 の境界でカメラが行ったり来たりしにくくなります。

### Setup Selected As Boss Area

選択したエリアをボス部屋化します。

```text
GameObject > Camera Area > Setup Selected As Boss Area
```

例:

```text
301 を選んで実行
```

自動で探す/作るもの:

```text
301
  Col_301
    BossAreaController
    RoomCameraTrigger
    BoxCollider2D

  CN_301_Boss
    CinemachineCamera

  BossCameraTarget
    DualTargetCameraTarget
```

`Col_301` の中に `StageBossAttack` または `LastBossController` を持つボスがあれば、`bossRoot` も自動で入ります。  
見つからない場合は、あとで `BossAreaController.bossRoot` にボス本体を手で入れます。

## ボス部屋の問題

ボス部屋のように縦にも横にも大きい部屋では、固定カメラだけでは対応しきれません。

固定カメラだと、カメラの中心とズームが固定されるため、プレイヤーやボスが上下に大きく動くと見切れます。

そのため、ボス部屋では次の方式を使います。

```text
制限付きデュアルターゲットフォローカメラ
```

## ボス部屋用カメラ

担当コンポーネント:

- `DualTargetCameraTarget`
- `CinemachineCamera`
- `CinemachineConfiner2D`
- `BossAreaController`

推奨ヒエラルキー:

```text
BossRoom
  Col_BossRoom
    BossAreaController

  CN_BossRoom
    CinemachineCamera
    CinemachineConfiner2D

  BossCameraTarget
    DualTargetCameraTarget

  CameraBounds_BossRoom
    Collider2D
```

### DualTargetCameraTarget

`DualTargetCameraTarget` は、プレイヤーとボスの中間地点へゆっくり移動する Follow Target です。

```text
Player -------- Boss
        ↑
    カメラ中心
```

さらに、プレイヤーとボスの距離が離れたら `OrthographicSize` をゆっくり広げます。  
近づいたらゆっくり戻します。

目的は、プレイヤーとボスの両方を画面に入れることです。

主な設定:

```text
secondaryWeight
  0.5 ならプレイヤーとボスのちょうど中間

smoothTime
  中心がどれくらいゆったり追従するか

minOrthographicSize
  最小ズーム

maxOrthographicSize
  最大ズームアウト

horizontalPadding / verticalPadding
  画面端に余白をどれくらい残すか

zoomSmoothTime
  ズーム変化の滑らかさ
```

### CinemachineConfiner2D

ボス部屋カメラには `CinemachineConfiner2D` を付けます。

これは、カメラが動いてよい範囲を制限するためのものです。

```text
プレイヤーとボスを追う
でも部屋の外までは映さない
```

`Bounding Shape 2D` に `CameraBounds_BossRoom` の Collider を指定します。

## BossAreaController との接続

`BossAreaController` には次を設定します。

```text
fixedBossCamera
  CN_BossRoom

dualTargetCameraTarget
  BossCameraTarget

bossRoot
  ボス本体
```

戦闘開始時、`BossAreaController` は `DualTargetCameraTarget` に次を渡します。

```text
primary = Player
secondary = Boss
camera = fixedBossCamera
```

そして `fixedBossCamera` の Priority を上げます。

名前はまだ `fixedBossCamera` のままですが、ボス部屋では実質的に「制限付きフォローカメラ」として使います。

## 使い分け表

| 場所 | 推奨カメラ | 理由 |
|---|---|---|
| 廊下 | 通常フォローカメラ | 移動が長く、固定すると見切れる |
| 小部屋 | 固定エリアカメラ | 1画面で構図を決めやすい |
| 上下階層のある隣接エリア | Portal + 固定/フォロー | つなぎ目だけで切り替えるため誤遷移しにくい |
| ボス部屋 | 制限付きデュアルターゲットフォロー | プレイヤーとボスを両方映せる |
| イベント演出 | イベントカメラ/Timeline | 演出用に Transform や Zoom を直接制御 |

## 現在の注意点

### Unity Editor でのコンパイル確認が必要

`dotnet build Assembly-CSharp.csproj --no-restore` は、Unity 生成 csproj の都合でエラー0件のまま失敗表示になります。  
そのため、最終確認は Unity Editor 上のコンパイルで行ってください。

### 未追跡シーン

作業中の Git 状態には、未追跡のシーンがあります。

```text
Assets/Scenes/Test_Fuyuno_Camera.unity
Assets/Scenes/Test_Fuyuno_Camera.unity.meta
```

これは今回のカメラコード変更では直接編集していません。

## 今後のおすすめ

1. 既存の `Stage > Area` に対して `Setup Camera Area` を実行する
2. エリア間のつなぎ目に `RoomCameraPortal` を配置する
3. 小部屋は固定エリアカメラのまま使う
4. ボス部屋は `DualTargetCameraTarget + CinemachineConfiner2D` にする
5. `fixedBossCamera` という名前は、将来的に `bossCamera` や `encounterCamera` に変える

この方針なら、カメラ切り替えの場所がヒエラルキーで見え、エリア追加もボタンで済み、ボス部屋の見切れにも対応できます。
