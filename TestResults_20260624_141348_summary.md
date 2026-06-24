# テストランナー結果まとめ

元ファイル: `C:\Users\manto\Downloads\TestResults_20260624_141348.xml`

実行時刻: `2026-06-24 05:13:02Z` - `2026-06-24 05:13:12Z`  
実行モード: `EditMode`  
実行時間: `9.5126812秒`

## 全体結果

| 総数 | 成功 | 失敗 | 判定不能 | スキップ |
| ---: | ---: | ---: | ---: | ---: |
| 159 | 134 | 25 | 0 | 0 |

総合判定: **失敗**

## ざっくり状況

失敗はランダムに散らばっているというより、いくつかの機能にまとまっています。

- 攻撃判定のOverlap Scan: 3件
- プレイヤー操作ロック / ゲームプレイ停止まわり: 6件
- ステージボスのイントロ処理: 9件
- ラスボス演出 / Prefab設定: 4件
- レバースイッチの開閉表示: 2件
- EditMode中に `DontDestroyOnLoad` を呼んでいる問題: ステージボスイントロ系に含まれる

## 優先対応

1. `StoryEventRuntimeService` のEditMode例外を直す
   - EditModeテスト中に `DontDestroyOnLoad` を呼んで例外になっています。
   - エディタ上のテストランナーを安定して使うため、最優先で直したい箇所です。

2. プレイヤー操作ロック中の挙動を直す
   - 操作ロック中でも敵、弾、接触ダメージ、ラスボス初期ディレイが進んでいます。
   - Fix版のゲーム進行に直結するので優先度高めです。

3. ステージボスイントロの開始条件とFreeze処理を直す
   - イントロが早く始まる、`FreezeAll` にならない、地面待ちやイベント待ちが効いていないテストがあります。

4. 攻撃判定とラスボス演出の回帰を直す
   - 攻撃が当たるべきケースで当たっていない。
   - ラスボス演出のフレーム、位置、Prefab設定値にズレがあります。

5. レバースイッチの表示ズレを直す
   - 開始時に開いている設定なのに、見た目が閉じたSpriteのままになっています。

## 失敗テスト一覧

### AttackHitboxOcclusionTests

| テスト | 期待値 / 実際の値 | 場所 |
| --- | --- | --- |
| `ScanCurrentOverlaps_WhenFallThroughPlatformBetweenPlayerAndTarget_DoesNotBlock` | 期待値 `1`、実際 `0` | `Assets/Editor/AttackHitboxOcclusionTests.cs:74` |
| `ScanCurrentOverlaps_WhenHorizontalGroundBetweenPlayerAndTarget_DoesNotBlock` | 期待値 `1`、実際 `0` | `Assets/Editor/AttackHitboxOcclusionTests.cs:61` |
| `ScanCurrentOverlaps_WhenTargetBeforeVerticalGroundWall_AttacksOnce` | 期待値 `1`、実際 `0` | `Assets/Editor/AttackHitboxOcclusionTests.cs:48` |

### DodgeControllerTests

| テスト | 期待値 / 実際の値 | 場所 |
| --- | --- | --- |
| `Dodge_WhenCooldownIsActive_RejectsNextDodgeUntilCooldownEnds` | 期待値 `True`、実際 `False` | `Assets/Editor/DodgeControllerTests.cs:89` |

### EnemyGameplayPauseTests

| テスト | 期待値 / 実際の値 | 場所 |
| --- | --- | --- |
| `EnemyBullet_FreezesAndBlocksPlayerDamageWhileLocked_ThenResumes` | 期待値 `(0.00, 0.00)`、実際 `(4.00, 0.00)` | `Assets/Editor/EnemyGameplayPauseTests.cs:151` |
| `EnemyContact_DoesNotDamageWhilePlayerControlLocked_AndDamagesAfterUnlock` | 期待値 `0`、実際 `1` | `Assets/Editor/EnemyGameplayPauseTests.cs:87` |
| `PatrolEnemy_StopsWhilePlayerControlLocked_AndResumesAfterUnlock` | 期待値 `0.0f +/- 0.0001f`、実際 `2.0f` | `Assets/Editor/EnemyGameplayPauseTests.cs:48` |
| `RangedEnemy_DoesNotCompleteWindupWhilePlayerControlLocked` | 期待値 `True`、実際 `False` | `Assets/Editor/EnemyGameplayPauseTests.cs:125` |
| `TackleEnemy_DoesNotEnterWindupWhilePlayerControlLocked` | 期待値 `False`、実際 `True` | `Assets/Editor/EnemyGameplayPauseTests.cs:103` |

### LastBossEffectIntegrationTests

| テスト | 期待値 / 実際の値 | 場所 |
| --- | --- | --- |
| `EffectController_NormalSlashKeepsFixedUniformScaleAcrossFrames` | 期待位置 `x:10`、実際 `x:20` | `Assets/Editor/LastBossEffectIntegrationTests.cs:975` |
| `GroundBladeVisual_KeepsRisenTopThroughVanishFrames` | 期待 `GroundBladeUprightFrame`、実際 `GroundBladeVanishFrame` | `Assets/Editor/LastBossEffectIntegrationTests.cs:1117` |
| `LastBoss_RainBladePreviewSpawnsOutsideFacingBottomCornerAndKeepsSlotSpacing` | `NullReferenceException` | `Assets/Editor/LastBossEffectIntegrationTests.cs:1606` |
| `LastBossPrefab_RestoresEffectControllerAndTransparentPreviewBoxes` | 期待値 `0.55 +/- 0.001`、実際 `1.5` | `Assets/Editor/LastBossEffectIntegrationTests.cs:586` |

### LastBossPlayerControlLockTests

| テスト | 期待値 / 実際の値 | 場所 |
| --- | --- | --- |
| `LastBoss_DoesNotLeaveInitialDelayWhilePlayerControlLocked` | 期待値 `0.0f +/- 0.0001f`、実際 `8.0f` | `Assets/Editor/LastBossPlayerControlLockTests.cs:77` |

### LeverSwitch2DTests

| テスト | 期待値 / 実際の値 | 場所 |
| --- | --- | --- |
| `ActivateFromAttack_WhenOpenStarts_AppliesOpenedSprite` | 期待 `Opened` Sprite、実際 `Closed` Sprite | `Assets/Editor/LeverSwitch2DTests.cs:50` |
| `Awake_WhenShutterStartsOpened_AppliesOpenedSprite` | 期待 `Opened` Sprite、実際 `Closed` Sprite | `Assets/Editor/LeverSwitch2DTests.cs:61` |

### StageBossIntroTests

| テスト | 期待値 / 実際の値 | 場所 |
| --- | --- | --- |
| `OnTriggerEnter2D_WhenPlayerOnlyPartiallyInside_DoesNotStartOrMovePlayer` | 期待値 `False`、実際 `True` | `Assets/Editor/StageBossIntroTests.cs:325` |
| `OnTriggerStay2D_WhenPlayerLeavesThenFullyEnters_StartsWithoutMovingPlayer` | 期待値 `False`、実際 `True` | `Assets/Editor/StageBossIntroTests.cs:350` |
| `RestoreStageBossForCombat_ReappliesPassThroughCollisionAfterColliderRestore` | 期待値 `True`、実際 `False` | `Assets/Editor/StageBossIntroTests.cs:243` |
| `StageBossIntroWindSuppression_BlocksOnlyWindRiseInsideBossAreaUntilIntroEnds` | 期待値 `True`、実際 `False` | `Assets/Editor/StageBossIntroTests.cs:268` |
| `StartEncounterAfterStoryRoutine_ForLastBoss_StartsAfterObservedExternalEventBecomesIdle` | EditMode中に `DontDestroyOnLoad` を呼んで例外 | `Assets/Scripts/Managers/StoryEventRuntimeService.cs:119` |
| `StartEncounterAfterStoryRoutine_ForLastBoss_WaitsForExternalStoryEventTrigger` | EditMode中に `DontDestroyOnLoad` を呼んで例外 | `Assets/Scripts/Managers/StoryEventRuntimeService.cs:119` |
| `TryStartEncounter_ForStageBoss_DelaysHpEventAndActivationUntilIntroCompletes` | 期待 `FreezeAll`、実際 `None` | `Assets/Editor/StageBossIntroTests.cs:105` |
| `TryStartEncounter_ForStageBoss_FreezesPlayerDuringIntro` | 期待 `FreezeAll`、実際 `None` | `Assets/Editor/StageBossIntroTests.cs:162` |
| `TryStartEncounter_ForStageBoss_WaitsForGroundBeforeFreezingPlayer` | 期待値 `False`、実際 `True` | `Assets/Editor/StageBossIntroTests.cs:204` |

## Fix版を書き出す前の判断

この結果のままFix版を書き出すのは避けた方がよさそうです。

最低限、先に直したいもの:

- EditMode中の `DontDestroyOnLoad` 例外
- 操作ロック中に敵・弾・接触・ラスボス初期ディレイが進む問題
- ステージボスイントロの開始条件とFreeze処理
- EditModeテストを再実行して失敗数が `0` になること
