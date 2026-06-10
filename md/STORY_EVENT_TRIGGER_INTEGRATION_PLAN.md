# Story Event Trigger Integration Plan

## 目的

現在のストーリーイベントは、主に「イベント再生君」からの手動再生やシーン開始時の自動再生で確認している。
次の段階では、作成済みイベントをゲーム内の具体的な発火タイミングへ紐づける。

対象にする発火タイミングは次の通り。

- プレイヤーが特定のコリジョンを踏んだとき
- ボス戦が始まったとき
- ボスを倒したとき
- 特定の敵を倒したとき
- 必要に応じて、アイテム取得、フラグ変化、部屋遷移など

方針は、イベントの再生処理そのものを増やすのではなく、既存の `StoryEventRuntimeService` / `StoryEventRunner` / `StoryEventController` を使い、発火源だけを追加していくこと。

## 現状の構造

### イベント再生側

既存の主な構成は次の通り。

- `StoryEventController`
  - Timeline ベースのイベントを再生するコンポーネント。
  - `PlayableDirector`、会話、パネル、カメラ、レターボックス、プレイヤー操作ロックなどをまとめて扱う。
  - `PlayEvent()` で直接再生できる。

- `StoryEventRunner`
  - `StoryEventDefinition` をキューに積んで順番に実行する。
  - Yarn ノード再生、前後アクション、フラグ条件、完了時フラグ、オートセーブを扱う。

- `StoryEventRuntimeService`
  - DontDestroyOnLoad の常駐サービス。
  - シーン読み込み後のイベント起動、デバッグ再生、イベントカタログ読み込みを担当する。
  - 現状は `TryPlayEventFromDebugger(eventId, ignoreFlags)` が主な外部入口。

- `StoryEventDefinition`
  - `eventId`
  - `sceneName`
  - `dialogueNodeName`
  - `runOnceFlagKey`
  - `conditions`
  - `onStartMutations`
  - `onCompleteMutations`
  - `preActions`
  - `postActions`
  - `autoSaveOnComplete`
  - `skipWhenDialogueRunning`

### 発火できそうな既存ポイント

- `BossAreaController`
  - `EncounterStarted`
  - `EncounterCompleted`
  - ボス撃破フラグ設定
  - ボスカメラ/BGM/封鎖解除などの終了処理

- `IBossHealthSource`
  - `Died`
  - `HealthChanged`
  - `EnemyController` / `LastBossController` などが実装している。

- 既存のコリジョン系
  - `AutoSaveTrigger2D`
  - `TutorialTriggerZone`
  - `LocationAreaTrigger`
  - `WarpArea2D`
  - それぞれ `OnTriggerEnter2D` でプレイヤー判定している。

## 基本方針

### 1. イベント再生の入口を通常用に公開する

現在の `TryPlayEventFromDebugger` はデバッグ用途で、`ignoreFlags` によって条件を無視できる。
ゲーム本編の発火源からは、条件を無視しない通常入口を使いたい。

追加する API のイメージ。

```csharp
public static bool TryPlayEvent(string eventId)
```

役割。

- `eventId` が空なら失敗。
- 現在シーンに合うイベント定義を探す。
- 見つからなければ全体カタログから探す。
- `StoryEventRunner.Enqueue(definition)` へ渡す。
- `runOnceFlagKey` や `conditions` は `StoryEventRunner` / `StoryEventDefinition` 側で評価する。
- デバッグ用の `ignoreFlags` は使わない。

これにより、コリジョン、ボス、敵死亡、アイテム取得など、すべての発火源が同じ入口を使える。

### 2. イベント定義はカタログ中心にする

発火源が直接 Timeline や Yarn ノードを持つと、あとで管理が分散する。
発火源は原則として `eventId` だけを持ち、内容は `StoryEventCatalog` / `StoryEventDefinition` 側に寄せる。

望ましい責務分担。

- 発火源: いつ発火するか
- イベント定義: 何を再生するか
- ランナー: どう順番に再生するか
- フラグ: 一回だけか、条件を満たすか

現状の `sceneStartEvents` はシーン開始イベントという名前だが、ID検索にも使われている。
将来的には `events` のような汎用リストを追加し、既存互換として `sceneStartEvents` も検索対象に残すのがよい。

## 追加するコンポーネント案

### StoryEventTrigger2D

プレイヤーが 2D トリガーを踏んだときにイベントを発火する汎用コンポーネント。

想定配置。

- 空 GameObject または既存トリガー GameObject に付与。
- `Collider2D.isTrigger = true`。
- `eventId` に再生したいイベントIDを設定。

主なフィールド案。

```csharp
[SerializeField] private string eventId;
[SerializeField] private string playerTag = "Player";
[SerializeField] private bool triggerOnce = true;
[SerializeField] private bool disableColliderAfterTrigger;
[SerializeField] private bool fireOnEnter = true;
[SerializeField] private bool fireOnStay;
[SerializeField] private bool fireIfPlayerAlreadyInsideOnEnable;
[SerializeField] private bool skipWhenStoryEventRunning;
```

挙動。

- `OnTriggerEnter2D` でプレイヤーを判定。
- 必要なら `OnTriggerStay2D` でも判定。
- `triggerOnce` が true なら、同一プレイ中は一度だけ発火。
- セーブをまたいだ一回制御は、イベント定義の `runOnceFlagKey` に任せる。
- 発火成功後、必要なら Collider を無効化。

プレイヤー判定は、単純な `CompareTag("Player")` だけにしない。
子コライダーや本体コライダーの差があるため、既存の `PlayerHealth` / `PlayerController` / ルートタグ確認に寄せる。

### BossStoryEventTrigger

ボスエリアの開始/完了イベントにストーリーイベントを紐づけるコンポーネント。

想定配置。

- `BossAreaController` と同じ GameObject
- または、参照で対象 `BossAreaController` を指定する管理用 GameObject

主なフィールド案。

```csharp
[SerializeField] private BossAreaController bossArea;
[SerializeField] private string onEncounterStartedEventId;
[SerializeField] private string onEncounterCompletedEventId;
[SerializeField] private float startDelaySeconds;
[SerializeField] private float completedDelaySeconds;
```

挙動。

- `BossAreaController.EncounterStarted` を購読。
- `BossAreaController.EncounterCompleted` を購読。
- 通知された `BossAreaController` が自分の対象と一致した場合のみ発火。

ボス撃破後イベントは、基本的に `EncounterCompleted` を使う。
理由は、ボス本体の `Died` 直後よりも、ボスエリア側の完了処理が終わった後の方が安定するため。

`EncounterCompleted` までに行われる可能性がある処理。

- ボス撃破フラグ設定
- ボスAI停止
- ボスカメラ解除
- BGM復帰
- プレイヤー/ボスの閉じ込め解除
- セーブ状態反映

この後にイベントを出す方が、演出とゲーム状態が衝突しにくい。

### EnemyDeathStoryEventTrigger

特定の敵を倒したときにイベントを発火するコンポーネント。

主なフィールド案。

```csharp
[SerializeField] private MonoBehaviour healthSourceBehaviour;
[SerializeField] private string eventId;
[SerializeField] private float delaySeconds;
[SerializeField] private bool triggerOnce = true;
```

挙動。

- `healthSourceBehaviour` から `IBossHealthSource` を取得。
- 未設定なら同一 GameObject / 子から探す。
- `Died` を購読。
- 死亡時に `eventId` を再生。

ボス撃破には原則 `BossStoryEventTrigger` を使い、通常敵や特殊敵にはこちらを使う。

### Optional: StoryEventFlagTrigger

フラグが立った瞬間にイベントを発火したい場合の追加案。
ただし、フラグ監視は更新頻度や責務が膨らみやすいので、最初は作らない。

必要になった場合だけ検討する。

例。

- 特定アイテムを所持した状態で部屋に入った
- 3つのスイッチがすべて押された
- 進行フラグが切り替わった

この場合も、まずはスイッチ側/アイテム側など具体的な発火源から `TryPlayEvent(eventId)` を呼ぶ方が分かりやすい。

## 優先実装順

### Phase 1: 通常再生 API

`StoryEventRuntimeService` に通常用 API を追加する。

完了条件。

- `StoryEventRuntimeService.TryPlayEvent("some_event")` を外部から呼べる。
- デバッグ再生と違い、フラグ条件を無視しない。
- 存在しない `eventId` なら false を返して警告ログを出す。

### Phase 2: コリジョン発火

`StoryEventTrigger2D` を追加する。

完了条件。

- トリガーを踏むと指定 `eventId` が再生される。
- 同一プレイ中の連打が防げる。
- `runOnceFlagKey` によるセーブ跨ぎ一回制御が効く。
- 子コライダーで踏んでもプレイヤーとして認識できる。

### Phase 3: ボス開始/撃破発火

`BossStoryEventTrigger` を追加する。

完了条件。

- ボス戦開始時にイベントを出せる。
- ボス撃破完了時にイベントを出せる。
- 複数ボスエリアがあっても、対象エリアだけが反応する。

### Phase 4: 特定敵死亡発火

必要になったタイミングで `EnemyDeathStoryEventTrigger` を追加する。

完了条件。

- 特定の敵死亡でイベントを出せる。
- ドロップ処理など既存の `Died` 購読処理と共存できる。

### Phase 5: 運用整理

イベントを量産すると、`eventId` とフラグ名の管理が重要になる。
最低限、命名規則と一覧を作る。

例。

- `story_prologue_start`
- `area_library_enter_first`
- `boss01_intro`
- `boss01_defeated`
- `enemy_guardian_defeated`

フラグ例。

- `story.prologue.completed`
- `area.library.enter_first.seen`
- `boss.01.defeated_event.seen`

## 推奨する運用ルール

### 発火源には eventId だけを持たせる

発火源に会話ノード名や Timeline 参照を直接持たせると、イベントの全体像が追いづらくなる。
基本は `eventId` だけを設定し、内容はカタログ側に集約する。

### 一回だけ再生は runOnceFlagKey を使う

`triggerOnce` は同一プレイ中の多重発火防止。
セーブデータをまたいだ一回制御は `runOnceFlagKey` を使う。

この分担にすると、次のケースに強い。

- セーブしてロードした後に同じ場所へ戻る
- ボス撃破済みでシーンに入り直す
- デバッグでフラグを戻す

### ボス撃破イベントは EncounterCompleted に寄せる

ボスの `Died` は「HPがゼロになった瞬間」。
`EncounterCompleted` は「ボス戦としての終了処理が走った後」。

ストーリーイベントは後者の方が扱いやすい。

ただし、死亡した瞬間にスロー演出や短い叫びを出したい場合は、ボス本体側の `Died` に近いイベントも検討する。
その場合も、終了後の会話/演出とは分ける。

### イベント中の追加発火はキューに任せる

`StoryEventRunner` はキューを持っているため、基本は発火した順に積む。
ただし、同時発火が問題になる場所では、発火源側に `skipWhenStoryEventRunning` を持たせる。

例。

- 通路の連続トリガー
- 戦闘後にプレイヤーが移動し続けて別トリガーも踏む
- イベント再生中に再度同じコリジョンへ入る

## リスクと対策

### リスク: イベントIDの入力ミス

対策。

- 発火失敗時に `eventId` と GameObject 名をログに出す。
- 可能なら後で Editor 検証を追加する。

### リスク: 複数トリガーが同じイベントを同時発火する

対策。

- `runOnceFlagKey` を設定する。
- `triggerOnce` を有効にする。
- 必要なら RuntimeService 側で「同じ eventId がキュー済みなら積まない」オプションを追加する。

### リスク: ボス撃破直後の演出とイベントが衝突する

対策。

- ボス撃破後は `EncounterCompleted` を使う。
- 必要なら `completedDelaySeconds` を設定する。

### リスク: プレイヤー判定が子コライダーで漏れる

対策。

- `CompareTag` だけにしない。
- `GetComponentInParent<PlayerHealth>()` や `PlayerController`、ルートタグも確認する。
- 既存の `PlayerBodyColliderUtility` が使える場面では利用する。

## 最初に作るべき実例

### コリジョン踏みイベント

1. `StoryEventCatalog` に `area_xxx_enter_first` を追加。
2. `runOnceFlagKey` を設定。
3. 対象エリアに `BoxCollider2D` と `StoryEventTrigger2D` を配置。
4. `eventId = area_xxx_enter_first` を設定。
5. 実機で踏んで再生確認。
6. セーブ/ロード後に再発火しないことを確認。

### ボス撃破イベント

1. `StoryEventCatalog` に `boss01_defeated` を追加。
2. `runOnceFlagKey` を設定。
3. 対象 `BossAreaController` に `BossStoryEventTrigger` を追加。
4. `onEncounterCompletedEventId = boss01_defeated` を設定。
5. ボス撃破後にイベントが出ることを確認。
6. ボス撃破済みロードでイベントが出ないことを確認。

## まとめ

この方針では、イベント再生システムは既存のものを維持する。
新しく作るのは「いつ再生するか」を決める薄い発火コンポーネントだけにする。

最初の実装単位は次の3つ。

- `StoryEventRuntimeService.TryPlayEvent(eventId)`
- `StoryEventTrigger2D`
- `BossStoryEventTrigger`

これで、手動再生だけだったイベントを、ゲーム内の実際の進行に安全に接続できる。
