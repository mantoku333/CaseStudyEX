# TestResultsEX 結果要約

対象ファイル: `C:\Users\manto\Downloads\TestResultsEX.xml`  
実行日時: 2026-06-24 12:26:05Z - 12:26:18Z  
実行モード: EditMode

## 全体結果

| 項目 | 件数 |
|---|---:|
| 総テスト数 | 172 |
| 成功 | 145 |
| 失敗 | 27 |
| スキップ | 0 |
| Inconclusive | 0 |
| 実行時間 | 12.323 秒 |

結果は `Failed(Child)` です。子テストの一部が失敗しています。

前回の `TestResults_20260624_141348.xml` は `159 件中 134 成功 / 25 失敗` だったため、今回はテスト数が 13 件増えています。成功数は 11 件増えましたが、失敗も 2 件増えています。

## 追加カテゴリの状況

| クラス | カテゴリ | 成功 / 総数 | 状態 |
|---|---|---:|---|
| `StoryEventPlaybackTests` | `Story/Event` | 8 / 8 | 全部成功 |
| `OptionsMenuTests` | `UI/Menu` | 4 / 6 | 2 件失敗 |

イベント再生周りの軽量テストは通っています。  
UI/Menu は追加した選択表示・メニュー停止復元のテストで失敗が出ています。

## クラス別サマリ

| クラス | カテゴリ | 成功 / 総数 | 失敗 |
|---|---|---:|---:|
| `AttackHitboxOcclusionTests` | Gameplay | 3 / 6 | 3 |
| `DodgeControllerTests` | Gameplay | 4 / 5 | 1 |
| `EnemyGameplayPauseTests` | Gameplay | 1 / 6 | 5 |
| `LastBossEffectIntegrationTests` | Boss, VFX | 42 / 46 | 4 |
| `LastBossPlayerControlLockTests` | Boss, Gameplay | 0 / 1 | 1 |
| `LeverSwitch2DTests` | Gameplay | 3 / 5 | 2 |
| `OptionsMenuTests` | UI/Menu | 4 / 6 | 2 |
| `StageBossIntroTests` | Story/Event, Boss | 6 / 15 | 9 |
| `StoryEventPlaybackTests` | Story/Event | 8 / 8 | 0 |

## 失敗一覧

### AttackHitboxOcclusionTests

攻撃判定の遮蔽/重なり検出系で、期待では 1 件ヒットするはずが 0 件になっています。

- `ScanCurrentOverlaps_WhenTargetBeforeVerticalGroundWall_AttacksOnce`  
  `Expected: 1 / But was: 0`  
  `Assets/Editor/AttackHitboxOcclusionTests.cs:49`
- `ScanCurrentOverlaps_WhenHorizontalGroundBetweenPlayerAndTarget_DoesNotBlock`  
  `Expected: 1 / But was: 0`  
  `Assets/Editor/AttackHitboxOcclusionTests.cs:62`
- `ScanCurrentOverlaps_WhenFallThroughPlatformBetweenPlayerAndTarget_DoesNotBlock`  
  `Expected: 1 / But was: 0`  
  `Assets/Editor/AttackHitboxOcclusionTests.cs:75`

### DodgeControllerTests

- `Dodge_WhenCooldownIsActive_RejectsNextDodgeUntilCooldownEnds`  
  `Expected: True / But was: False`  
  `Assets/Editor/DodgeControllerTests.cs:90`

クールダウン中の再ドッジ拒否判定が想定とずれています。

### EnemyGameplayPauseTests

プレイヤー操作ロック中に敵の移動・攻撃・弾・接触ダメージが止まるべきテストが広く失敗しています。

- `PatrolEnemy_StopsWhilePlayerControlLocked_AndResumesAfterUnlock`  
  `Expected: 0 / But was: 2`  
  `Assets/Editor/EnemyGameplayPauseTests.cs:49`
- `EnemyContact_DoesNotDamageWhilePlayerControlLocked_AndDamagesAfterUnlock`  
  `Expected: 0 / But was: 1`  
  `Assets/Editor/EnemyGameplayPauseTests.cs:88`
- `TackleEnemy_DoesNotEnterWindupWhilePlayerControlLocked`  
  `Expected: False / But was: True`  
  `Assets/Editor/EnemyGameplayPauseTests.cs:104`
- `RangedEnemy_DoesNotCompleteWindupWhilePlayerControlLocked`  
  `Expected: True / But was: False`  
  `Assets/Editor/EnemyGameplayPauseTests.cs:126`
- `EnemyBullet_FreezesAndBlocksPlayerDamageWhileLocked_ThenResumes`  
  `Expected: (0.00, 0.00) / But was: (4.00, 0.00)`  
  `Assets/Editor/EnemyGameplayPauseTests.cs:152`

### LastBossEffectIntegrationTests

LastBoss の演出・エフェクト・プレビュー表示周りで 4 件失敗しています。

- `LastBossPrefab_RestoresEffectControllerAndTransparentPreviewBoxes`  
  `Expected: 0.55 / But was: 1.5`  
  `Assets/Editor/LastBossEffectIntegrationTests.cs:588`
- `EffectController_NormalSlashKeepsFixedUniformScaleAcrossFrames`  
  矩形位置が `x:10` 期待に対して `x:20`  
  `Assets/Editor/LastBossEffectIntegrationTests.cs:977`
- `GroundBladeVisual_KeepsRisenTopThroughVanishFrames`  
  `GroundBladeUprightFrame` 期待に対して `GroundBladeVanishFrame`  
  `Assets/Editor/LastBossEffectIntegrationTests.cs:1119`
- `LastBoss_RainBladePreviewSpawnsOutsideFacingBottomCornerAndKeepsSlotSpacing`  
  `NullReferenceException`  
  `Assets/Editor/LastBossEffectIntegrationTests.cs:1608`

### LastBossPlayerControlLockTests

- `LastBoss_DoesNotLeaveInitialDelayWhilePlayerControlLocked`  
  `Expected: 0 / But was: 8`  
  `Assets/Editor/LastBossPlayerControlLockTests.cs:79`

プレイヤー操作ロック中でも LastBoss の Rigidbody 速度が止まっていません。

### LeverSwitch2DTests

開いた状態で始まるレバー/シャッターが、Opened Sprite ではなく Closed Sprite になっています。

- `ActivateFromAttack_WhenOpenStarts_AppliesOpenedSprite`  
  `Expected: Opened / But was: Closed`  
  `Assets/Editor/LeverSwitch2DTests.cs:51`
- `Awake_WhenShutterStartsOpened_AppliesOpenedSprite`  
  `Expected: Opened / But was: Closed`  
  `Assets/Editor/LeverSwitch2DTests.cs:62`

### OptionsMenuTests

今回追加した UI/Menu テストのうち 2 件が失敗しています。

- `PauseAndRestoreGameplay_RestoresTimeScaleAndPausedPlayerVelocity`  
  `Expected: -4 / But was: 0`  
  `Assets/Editor/OptionsMenuTests.cs:77`
- `OptionsMenuButtonState_TracksKeyboardSelectionAndPointerHover`  
  `Expected: True / But was: False`  
  `Assets/Editor/OptionsMenuTests.cs:151`

メニュー停止中の速度復元と、ボタン選択表示テスト側のセットアップまたは実装挙動にずれがあります。

### StageBossIntroTests

StageBoss のイントロ・プレイヤーロック・風抑制・イベント待機周りで 9 件失敗しています。

- `TryStartEncounter_ForStageBoss_DelaysHpEventAndActivationUntilIntroCompletes`  
  `Expected: FreezeAll / But was: None`  
  `Assets/Editor/StageBossIntroTests.cs:107`
- `TryStartEncounter_ForStageBoss_FreezesPlayerDuringIntro`  
  `Expected: FreezeAll / But was: None`  
  `Assets/Editor/StageBossIntroTests.cs:164`
- `TryStartEncounter_ForStageBoss_WaitsForGroundBeforeFreezingPlayer`  
  `Expected: False / But was: True`  
  `Assets/Editor/StageBossIntroTests.cs:206`
- `RestoreStageBossForCombat_ReappliesPassThroughCollisionAfterColliderRestore`  
  `Expected: True / But was: False`  
  `Assets/Editor/StageBossIntroTests.cs:245`
- `StageBossIntroWindSuppression_BlocksOnlyWindRiseInsideBossAreaUntilIntroEnds`  
  `Expected: True / But was: False`  
  `Assets/Editor/StageBossIntroTests.cs:270`
- `OnTriggerEnter2D_WhenPlayerOnlyPartiallyInside_DoesNotStartOrMovePlayer`  
  `Expected: False / But was: True`  
  `Assets/Editor/StageBossIntroTests.cs:327`
- `OnTriggerStay2D_WhenPlayerLeavesThenFullyEnters_StartsWithoutMovingPlayer`  
  `Expected: False / But was: True`  
  `Assets/Editor/StageBossIntroTests.cs:352`
- `StartEncounterAfterStoryRoutine_ForLastBoss_WaitsForExternalStoryEventTrigger`  
  `DontDestroyOnLoad` は EditMode では使えないエラー  
  `Assets/Editor/StageBossIntroTests.cs:498`
- `StartEncounterAfterStoryRoutine_ForLastBoss_StartsAfterObservedExternalEventBecomesIdle`  
  `DontDestroyOnLoad` は EditMode では使えないエラー  
  `Assets/Editor/StageBossIntroTests.cs:546`

## 優先対応

1. `OptionsMenuTests` の新規失敗 2 件を先に確認  
   今回追加したテスト由来なので、実装バグかテストセットアップ不足かを切り分ける価値が高いです。

2. `StoryEventRuntimeService` の EditMode 呼び出し問題を修正  
   `DontDestroyOnLoad` が EditMode テストで例外になっています。該当テストを PlayMode に移すか、EditMode ではサービス生成しない分岐が必要です。

3. `EnemyGameplayPauseTests` と `LastBossPlayerControlLockTests` をまとめて見る  
   どちらも「プレイヤー操作ロック中に敵/ボスが止まるべき」系なので、共通の pause 判定または lock 判定が崩れている可能性があります。

4. `StageBossIntroTests` のプレイヤーロック/入室判定を確認  
   FreezeAll にならない、部分入室でも開始してしまう、風抑制が効かないなど、ボスイントロ制御の複数点が落ちています。

5. `LastBossEffectIntegrationTests` と `LeverSwitch2DTests` は演出/初期表示系として別枠で確認  
   ロジック停止系とは別の表示状態・Prefab 設定・初期化順の問題に見えます。

## 成功している追加項目

`StoryEventPlaybackTests` は 8 件すべて成功しています。  
イベントID整形、シーン名一致、シーン開始イベント定義、イベント再生ウィンドウの選択解決/並び順は現状問題なしです。
