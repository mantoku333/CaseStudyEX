# Last Boss Animation Plan

## 目的

ラスボス `LastBoss` に、`Assets/Art/Sprites/Thanatos` のスプライトを使ったアニメーションを追加する。

実装するパターン:

- 左右移動: `Move`
- 横攻撃: `A_Said_Start`, `A_Said_End`
- 通常攻撃: `A_Normal`
- ダウン: `Down_Start`, `Down_End`
- 上空攻撃: `A_Sky_Start`, `A_Sky_End`

Player や雑魚敵と同じく、見た目は `SpriteView` に分け、`Animator Controller` のステートをスクリプトから再生する方針にする。

## 方針

### SpriteView 分離

Prefab 構成は次の形にする。

```text
LastBoss
  - Rigidbody2D
  - Collider2D
  - LastBossController
  - EnemySfxEmitter
  SpriteView
    - SpriteRenderer
    - Animator
    - LastBossSpriteAnimator
```

責務:

- `LastBossController`: 移動、攻撃選択、攻撃判定、ダウン、死亡などのゲームロジック
- `SpriteView`: sprite 表示、Animator 再生、向き反転、色変更
- `LastBossSpriteAnimator`: `LastBossController` から呼ばれる薄い View API

Root の `LastBoss` は判定と移動の基準として維持し、見た目だけを `SpriteView` 側へ寄せる。

### Animator Controller

Thanatos 用に次を追加する。

```text
Assets/Art/Animations/Thanatos/Thanatos.controller
```

ステート名:

- `idle`
- `move`
- `attack_normal`
- `attack_horizontal_start`
- `attack_horizontal_end`
- `attack_vertical_start`
- `attack_vertical_end`
- `down_start`
- `down_hold`
- `down_end`

Controller 側に複雑な遷移は作らず、`LastBossSpriteAnimator` から `Animator.Play(stateName, layer, 0f)` で直接再生する。

### Animation Clip

同じフォルダに各ステート対応の `.anim` を置く。

```text
Assets/Art/Animations/Thanatos/idle.anim
Assets/Art/Animations/Thanatos/move.anim
Assets/Art/Animations/Thanatos/attack_normal.anim
Assets/Art/Animations/Thanatos/attack_horizontal_start.anim
Assets/Art/Animations/Thanatos/attack_horizontal_end.anim
Assets/Art/Animations/Thanatos/attack_vertical_start.anim
Assets/Art/Animations/Thanatos/attack_vertical_end.anim
Assets/Art/Animations/Thanatos/down_start.anim
Assets/Art/Animations/Thanatos/down_hold.anim
Assets/Art/Animations/Thanatos/down_end.anim
```

初期設定では `move` と `idle` はループ、攻撃とダウン開始/復帰は単発。
タイミングは Unity Editor 上で clip を見ながら後調整する。

## Controller 連携

`LastBossController` に `LastBossSpriteAnimator` 参照を持たせる。

```csharp
[SerializeField] private LastBossSpriteAnimator spriteView;
```

未設定時は `Awake()` で `GetComponentInChildren<LastBossSpriteAnimator>(true)` から解決する。

既存の `SpriteRenderer` 参照が必要な処理は、`spriteView.MainRenderer` を優先して使う。
これにより、被弾フラッシュ、怒り色、攻撃予兆の sorting order なども `SpriteView` 側の renderer に効く。

## 再生タイミング

### 移動

- 移動中: `PlayMove()`
- 停止時: `PlayIdle()`
- 向き変更時: `SetFacing(facingDirection)`

### 通常攻撃

- 通常攻撃開始時: `PlayNormalAttack()`
- Recovery へ戻るタイミングで `PlayIdle()`

### 横攻撃

- 予兆開始時: `PlayHorizontalStart()`
- ブレード生成開始時: `PlayHorizontalEnd()`
- Recovery へ戻るタイミングで `PlayIdle()`

### 上空攻撃

- 予兆開始時: `PlayVerticalStart()`
- RainBlade 落下開始時: `PlayVerticalEnd()`
- Recovery へ戻るタイミングで `PlayIdle()`

### ダウン

- ダウン突入時: `PlayDownStart()`
- ダウン維持中: `PlayDownHold()`
- 復帰直前: `PlayDownEnd()`
- 復帰後: `PlayIdle()`

`Down_Start` と `Down_End` の待ち時間は `LastBossSpriteAnimator.DownStartDuration` / `DownEndDuration` で調整する。

## 実装チェック

- `LastBoss.prefab` の root ではなく `SpriteView` に `SpriteRenderer` がある
- `SpriteView` に `Animator` があり、`Thanatos.controller` が設定されている
- `LastBossSpriteAnimator` の state name が controller 内の state 名と一致している
- `LastBossController.spriteView` が `SpriteView` の `LastBossSpriteAnimator` を参照している
- 移動、通常攻撃、横攻撃、上空攻撃、ダウンで対応 clip が再生される
- 被弾フラッシュと怒り色が `SpriteView` 側の renderer に効く
- 攻撃判定、ブレード生成、ダウン処理のロジックは既存挙動を維持する

## 注意

- 攻撃判定は Animation Event に移さない。今は `LastBossController` の既存タイミングを正とする。
- `A_Said_*` は素材フォルダ名をそのまま使い、コード上の意味は Horizontal に寄せる。
- `SpriteView` の Transform は見た目調整用。攻撃判定や移動基準には使わない。
