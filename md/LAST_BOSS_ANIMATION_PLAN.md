# Last Boss Animation Plan

## 目的

ラスボス `LastBoss` に、用意済みの Thanatos スプライトを使ったアニメーションを追加する。

実装したいパターンは次の5種類。

- 左右移動
- 横攻撃
- 通常攻撃
- ダウン
- 上空攻撃

戦闘ロジックは既存の `LastBossController` を維持し、見た目だけを安全に差し込む。

## 現状

### ラスボス制御

ラスボス本体は `Assets/Scripts/Enemy/LastBossController.cs` で制御されている。

主な状態。

- `Inactive`
- `InitialDelay`
- `MovingToRange`
- `Telegraphing`
- `AttackVisible`
- `Recovery`
- `Downed`
- `Dead`

主な攻撃種別。

- `Normal`
- `Horizontal`
- `Vertical`

現在の見た目制御はかなり軽い。

- `SpriteRenderer` を直接参照している。
- 向きは `spriteRenderer.flipX` で反転している。
- 被弾/怒り状態は `spriteRenderer.color` で表現している。
- `LastBoss.prefab` には `Animator` が付いていない。

そのため、最初から Animator Controller 主導にするより、`LastBossController` の状態に合わせて専用 SpriteView へ命令する方が安全。

プレイヤー側は `PlayerController` が状態を公開し、`PlayerSpriteAnimator` や装備表示が View として読む構成になっている。
ラスボスも同じく、ロジック本体と見た目階層を分ける。

## 素材

スプライトは `Assets/Art/Sprites/Thanatos` 配下にある。

| 用途 | フォルダ | 内容 |
| --- | --- | --- |
| 左右移動 | `Move` | 移動ループ |
| 通常攻撃 | `A_Normal` | 近接通常攻撃 |
| 横攻撃開始 | `A_Said_Start` | 横攻撃の構え/予兆 |
| 横攻撃発生 | `A_Said_End` | 横攻撃の振り切り/発生 |
| 上空攻撃開始 | `A_Sky_Start` | 上空攻撃の構え/予兆 |
| 上空攻撃発生 | `A_Sky_End` | 上空攻撃の発生 |
| ダウン開始 | `Down_Start` | ダウンへ入る |
| ダウン終了 | `Down_End` | ダウン復帰 |

`A_Said_*` は名前だけ見ると `Side` の typo の可能性がある。
既存フォルダ名はそのまま使い、コード上の表示名は `Side` または `Horizontal` に寄せる。

## 方針

### 1. SpriteView を分ける

Prefab の基本構成は次の形にする。

```text
LastBoss
  - Rigidbody2D
  - Collider2D
  - LastBossController
  - EnemySfxEmitter
  - その他ロジック系コンポーネント

  SpriteView
    - SpriteRenderer
    - LastBossSpriteAnimator
    - Animator optional
```

責務分担。

- `LastBossController`: 移動、攻撃選択、攻撃判定、HP、ダウン、死亡通知
- `SpriteView`: sprite 差し替え、Animator 再生、向き反転、被弾/怒り色、表示調整
- `GroundBlade` / `RainBlade`: 既存通り攻撃 prefab 側で管理

`LastBossController` は `SpriteRenderer` を直接操作しない方向へ寄せる。
ただし一気に全部移すとリスクが大きいので、まずは参照先を `SpriteView` 側の renderer に置き換え、色/向き/アニメ再生を段階的に View へ逃がす。

### 2. 専用アニメータを追加する

新規スクリプト案。

```text
Assets/Scripts/Enemy/LastBossSpriteAnimator.cs
```

役割。

- `SpriteView` 配下の `SpriteRenderer` の sprite を差し替える。
- 各アニメーションのフレーム配列を Inspector から受け取る。
- ループ再生と単発再生を扱う。
- 現在再生中の状態が同じなら無駄に先頭へ戻さない。
- `LastBossController` から明示的に再生命令を受ける。
- `SetFacing(int facingDirection)` で向き反転を担当する。
- `SetBaseColor(Color color)` や `PlayHitFlash()` で色演出を担当する。

想定する公開メソッド。

```csharp
public SpriteRenderer MainRenderer { get; }
public void SetFacing(int facingDirection);
public void PlayIdle();
public void PlayMove();
public void PlayNormalAttack();
public void PlayHorizontalStart();
public void PlayHorizontalEnd();
public void PlayVerticalStart();
public void PlayVerticalEnd();
public void PlayDownStart();
public void PlayDownHold();
public void PlayDownEnd();
public void PlayDead();
```

`Idle` 専用素材がないため、初期実装では次のどちらかにする。

- `Move` の1枚目を待機絵として使う。
- `A_Normal` や既存 `LastBoss.prefab` の初期 sprite を待機絵として保持する。

おすすめは、初期 sprite を `defaultSprite` として保持する方式。

### 3. Animator Controller は後回しにする

現時点では、ラスボスの攻撃判定とタイミングは `LastBossController` のタイマー/コルーチンで動いている。

Animator Controller へ主導権を移すと、以下の同期が必要になる。

- 攻撃判定発生タイミング
- ブレード生成タイミング
- パリィ受付
- ダウン突入/復帰
- 死亡通知

まずはコード駆動の `LastBossSpriteAnimator` で見た目を合わせる。
必要になったら後から Animator Controller 化する。

## `LastBossController` への差し込み位置

### フィールド追加

`LastBossController` に SpriteView 参照を追加する。

```csharp
[SerializeField] private LastBossSpriteAnimator spriteView;
```

`Awake()` で未設定なら `GetComponentInChildren<LastBossSpriteAnimator>(true)` する。

既存の `spriteRenderer` 参照は、最終的には `spriteView.MainRenderer` へ置き換える。
攻撃予兆の sorting order など、renderer が必要な処理も `SpriteView` 側の renderer を使う。

### 移動

対象箇所。

- `FixedUpdate()`
- `MoveForPendingAction()`
- `StopMotion()`

方針。

- `state == MovingToRange` で速度が出ている間は `PlayMove()`。
- `StopMotion()` で攻撃/ダウン中でなければ `PlayIdle()`。
- 向きが変わる箇所では `spriteView.SetFacing(facingDirection)` を呼ぶ。

### 通常攻撃

対象箇所。

- `BeginAction(BossAction action)`

方針。

- `action == Normal` の攻撃開始時に `PlayNormalAttack()`。
- 通常攻撃は赤箱表示とダメージ判定が即時寄りなので、アニメは単発再生でよい。
- Recovery に入ったら `PlayIdle()` へ戻す。

### 横攻撃

対象箇所。

- `BeginAction(BossAction.Horizontal)`
- `BeginPrefabRangeAttack(BossAction.Horizontal, activeAttackBox)`
- `FinishPrefabRangeAttack(BossAction.Horizontal)`

方針。

- 予兆開始時に `PlayHorizontalStart()`。
- ブレード生成開始時に `PlayHorizontalEnd()`。
- 攻撃終了後の Recovery で `PlayIdle()`。

横攻撃は `GroundBlade` の生成が本体なので、アニメーションは演出の同期だけ担当する。

### 上空攻撃

対象箇所。

- `BeginAction(BossAction.Vertical)`
- `BeginPrefabRangeAttack(BossAction.Vertical, activeAttackBox)`
- `FinishPrefabRangeAttack(BossAction.Vertical)`

方針。

- 予兆開始時に `PlayVerticalStart()`。
- `RainBlade` 落下開始時に `PlayVerticalEnd()`。
- 攻撃終了後の Recovery で `PlayIdle()`。

上空攻撃は予兆中にプレイヤー位置をロック/追従する処理があるため、見た目再生は判定ロジックへ影響させない。

### ダウン

対象箇所。

- `EnterDownRoutine()`

方針。

- ダウン突入時に `PlayDownStart()`。
- `Down_Start` 再生後、最後のフレームまたは専用 hold で停止。
- `downDuration` 終了直前に `PlayDownEnd()`。
- 復帰後 `Recovery` に戻るタイミングで `PlayIdle()`。

初期実装では、`Down_Start` 再生後に最後のフレームを保持するだけでよい。
`Down_End` の再生時間を待つ場合、`EnterDownRoutine()` 内で復帰前に短い wait を足す。

## Prefab 設定

対象。

```text
Assets/Prefabs/Enemies/LastBoss.prefab
```

追加するもの。

- 子 GameObject `SpriteView`
- `SpriteView` 配下の `SpriteRenderer`
- `SpriteView` 配下の `LastBossSpriteAnimator`
- `LastBossController.spriteView` への参照
- 各フレーム配列

設定するフレーム。

- `moveFrames`
- `normalAttackFrames`
- `horizontalStartFrames`
- `horizontalEndFrames`
- `verticalStartFrames`
- `verticalEndFrames`
- `downStartFrames`
- `downEndFrames`

`LastBoss.prefab` の Collider、Rigidbody2D、攻撃 prefab 参照は変更しない。
既存の root `SpriteRenderer` は、移行時に次のどちらかにする。

- `SpriteView` へ移動して root から削除する。
- 初期段階では root に残し、`SpriteView` 構成が安定した後に整理する。

おすすめは、Prefab 編集時に `SpriteView` へ移動する方式。
ロジック本体に描画コンポーネントが残らないため、以後の装備/エフェクト/演出の基準点が分かりやすい。

## 実装順

1. `LastBoss.prefab` に `SpriteView` 子オブジェクトを作る。
2. root の `SpriteRenderer` を `SpriteView` 側へ移す、または同等設定の renderer を作る。
3. `LastBossSpriteAnimator.cs` を追加する。
4. `LastBossController` に `spriteView` 参照と呼び出しを追加する。
5. Thanatos スプライトを各配列に設定する。
6. テストシーンで移動、通常攻撃、横攻撃、上空攻撃、ダウンを確認する。
7. 問題なければ必要に応じて `Anim_Boss` シーンへ反映する。

## 確認項目

- 移動中に `Move` がループする。
- 左右の向きが今まで通り `flipX` で切り替わる。
- `flipX` の対象が root ではなく `SpriteView` の renderer になっている。
- 通常攻撃時に `A_Normal` が再生される。
- 横攻撃の予兆で `A_Said_Start`、発生で `A_Said_End` が再生される。
- 上空攻撃の予兆で `A_Sky_Start`、発生で `A_Sky_End` が再生される。
- ダウン時に `Down_Start`、復帰時に `Down_End` が再生される。
- 被弾フラッシュと怒り色の `spriteRenderer.color` がアニメ差し替え後も効く。
- パリィ、ダウン、死亡、ブレード生成の挙動が変わらない。

## 注意点

- `Assets/Scenes/Anim_Boss.unity` には既存の未コミット変更があるため、実装時は不用意に上書きしない。
- 初期実装では Animator Controller を作らない。
- 攻撃判定を Animation Event に移さない。
- root `LastBoss` の Transform は判定/移動基準として維持する。
- `SpriteView` の Transform は見た目調整用に使い、攻撃判定の中心計算には使わない。
- フレーム数が少ない攻撃は、再生速度を Inspector で調整できるようにする。
- `Down_End` の再生待ちを入れる場合、ゲームテンポが重くならない秒数にする。

## 次の判断

実装に入る場合は、まず `SpriteView` 分離 + `LastBossSpriteAnimator` のコード駆動方式で進める。
その後、演出チーム側で Animator Controller 運用に寄せたい要望が出たら、`SpriteView` 配下に Animator を追加し、同じ状態名を使って Controller 化する。
