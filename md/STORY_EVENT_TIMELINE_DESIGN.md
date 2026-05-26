# Story Event Timeline Design

## 目的

イベント制作を、できるだけ新規実装なしで量産できる形にする。

これまで Yarn 内に直接書いていた演出コマンドや、コード側に寄っていたマーカー指定を減らし、イベントごとの Timeline 上で会話、キャラ移動、物の移動、暗転、音、カメラ演出を完結させる。

最終的には、イベント制作者が C# を触らずに、Prefab と Timeline の複製・編集だけで新規イベントを作れる状態を目指す。

ただし、この仕組みの一番の目的は単なる量産効率ではない。

企画担当者が、イベントの仮組み、確認、修正、再確認を短いサイクルで回せるようにすることを最重要の思想に置く。つまり、アジャイル的にイベントを作れる制作環境を作る。

そのため、GUI は「最低限触れる」だけでは不十分。企画担当者が迷わず、安全に、気持ちよく試行錯誤できる品質を目指す。

## 基本方針

### 企画がアジャイルに作れることを最優先する

このシステムは、プログラマーがイベントを組みやすくするためだけのものではない。

主な利用者は企画担当者を想定する。

企画担当者がその場で以下を行えることを重視する。

- 会話の流れを置く
- キャラや物の位置を調整する
- 暗転、音、カメラ演出を試す
- 再生して違和感を確認する
- すぐに修正して再テストする
- C# 実装待ちにならず、イベントの品質を自分で上げる

この思想のため、Editor GUI の品質は機能本体と同じくらい重要とする。

### 1イベント = 1 Timeline

原則として、1つのストーリーイベントに対して1つの Timeline アセットを持たせる。

```text
StoryEvent_Prologue
  StoryEventController
  PlayableDirector
  Timeline: TL_Prologue
  Markers
    Marker_01
    Marker_02
    Marker_03
```

StoryEvent 側は「いつ実行するか」「一度きりか」「完了時に何をするか」を管理し、イベントの中身は Timeline に寄せる。

```text
StoryEvent
  - eventId
  - sceneName
  - timeline
  - runOnceFlag
  - conditions
  - autoSaveOnComplete
```

### Timeline に集約するもの

Timeline はイベント演出の中心になる。

- Yarn 会話の開始
- キャラ移動
- オブジェクト移動
- キャラアニメーション再生
- 暗転
- カメラ移動
- ズーム
- 画面揺れ
- SE再生
- BGM再生/停止
- オブジェクトの表示/非表示
- フラグ変更
- オートセーブ

Yarn は会話本文、選択肢、分岐を担当する。

## Yarn と Timeline の関係

Yarn も Timeline から開始する。

基本形は Timeline Signal で Yarn ノードを再生する方式にする。

```text
Timeline
  0.0s  暗転
  1.0s  キャラ配置
  2.0s  Signal: Yarn node "Prologue_01" 開始
        Timeline Pause
        会話終了
        Timeline Resume
  5.0s  キャラ移動
  6.0s  Signal: Yarn node "Prologue_02" 開始
        Timeline Pause
        会話終了
        Timeline Resume
  9.0s  暗転
```

会話中は原則 Timeline を一時停止する。

プレイヤーが会話送りに時間をかけても、キャラ移動や暗転などの演出タイミングが先に進まないようにするため。

ただし、ボイス付き演出や自動進行イベントでは Timeline を停止しない設定も許可する。

```text
Pause Timeline Until Dialogue Complete: true / false
```

## イベントローカルマーカー

マーカーはシーン全体で固有名詞管理しない。

`bed_right` や `window` のような名前ではなく、イベント内だけで通じる番号を使う。

```text
Markers
  Marker_01
  Marker_02
  Marker_03
```

参照は名前ではなく番号で行う。

```text
Move Actor
  Actor: iris
  Target Marker No: 2
  Duration: 1.0
```

同じシーン内に複数イベントがあっても、それぞれのイベント内で `Marker_01` を持ってよい。

Timeline Clip や Signal は、直接シーン全体から名前検索せず、対象の `StoryEventController` に問い合わせる。

```text
Timeline Clip
  markerNo = 2

Runtime
  StoryEventController.GetMarker(2)
```

## イベントの自己完結性

イベントはできるだけ自己完結ユニットとして扱う。

```text
StoryEvent_XXX
  StoryEventController
  PlayableDirector
  Markers
  Event-only Objects
  Optional Actor Bindings
```

イベント外部への依存は最低限にする。

- DialogueManager
- SaveManager
- CameraManager
- StageBgmController
- Player
- 既存の共有キャラ、共有カメラ

イベント専用の座標、演出小物、一時的なオブジェクトはイベントの子階層に置く。

## キャラとオブジェクトの参照

キャラは番号より短いIDで扱う。

```text
actorKey: iris
actorKey: nox
actorKey: enemy_01
```

マーカーは番号、キャラはIDにする。

理由:

- マーカーはイベント内の位置なので番号で十分
- キャラは Timeline 上で誰を動かすか分かる必要がある

将来的には `StoryActor` を用意し、移動、向き変更、Animator/Spine再生を共通化する。

```text
StoryActor
  - actorKey
  - Animator
  - Spine/SkeletonAnimation
  - Rigidbody2D
```

物を動かす場合も、同じように `StoryObject` または Transform 参照を Timeline Clip から扱う。

## 制作フロー

イベント量産時の理想フロー。

1. `StoryEventTemplate` Prefab を複製する
2. `eventId` と `runOnceFlag` を設定する
3. 子階層に `Marker_01`, `Marker_02` などを配置する
4. Timeline アセットを複製または新規作成する
5. Timeline に必要な Clip / Signal を並べる
6. Yarn ノード名を Timeline 側に設定する
7. Play Mode でイベントをテスト実行する

この流れで、新規イベント追加時に C# を触らない状態を目指す。

同時に、1つのイベントを何度も作り直す試行錯誤の流れも重視する。

```text
仮配置
  -> Timeline で再生確認
  -> マーカー位置を調整
  -> 会話ノードや演出タイミングを調整
  -> その場で再テスト
  -> 必要なら複数パターンを比較
```

このサイクルが短いほど、企画担当者がイベントの手触りを詰めやすくなる。

## 必要な汎用 Timeline 部品

最初に揃えるべき汎用部品。

### Yarn

- Yarn ノード再生
- 会話完了まで Timeline Pause
- Bubble / ADV の表示形式指定
- Bubble の追従対象指定

### Transform

- Actor を Marker 番号へ即時配置
- Actor を Marker 番号へ移動
- Object を Marker 番号へ即時配置
- Object を Marker 番号へ移動
- 向き変更
- Active 切替

### Animation

- Animator state 再生
- Animator trigger 発火
- Spine animation 再生

### Camera

- Camera を Marker 番号へ移動
- Zoom
- Shake
- Cinemachine camera priority 切替

### Screen

- 暗転
- フェード色、アルファ、秒数指定

### Audio

- SE 再生
- ループSE再生
- ループSE停止
- BGM再生
- BGM停止
- BGMクロスフェード

BGM は Timeline の Audio Track 直再生より、ゲーム内音量設定やクロスフェードに対応した `StageBgmController` 経由を優先する。

### Save / Flags

- フラグON/OFF
- オートセーブ
- イベント完了処理

## 既存システムからの移行方針

現状の `PrologueDialogue.yarn` には `pg_actor_move` などの演出コマンドが直接書かれている。

移行後は、Yarn から演出コマンドを減らし、Timeline 側に移す。

```yarn
before:
<<pg_actor_move iris window 1.0>>
イリス: ...

after:
イリス: ...
```

会話の途中で演出を挟みたい場合は、Yarn ノードを分割して Timeline から順番に呼ぶ。

```text
Timeline
  Yarn: Prologue_01
  MoveActor to Marker 02
  Yarn: Prologue_02
```

Yarn 内にイベント制御コマンドを残す場合も、原則として最小限にする。

## 実装優先度

### Phase 1: Signal ベースで成立させる

- `StoryEventController` を作る
- イベントローカル Marker 管理を作る
- Timeline Signal から Yarn を再生する
- Yarn 完了まで Timeline を Pause/Resume する
- Timeline 完了時にフラグ更新・オートセーブする

まずはプロローグ1本をこの方式で動かす。

### Phase 1 実装メモ

初期実装として、以下のコンポーネントを用意する。

```text
StoryEventController
  Timeline主導イベントの中心。
  PlayableDirector と同じ GameObject に付ける。

StoryEventMarker
  イベント内ローカル番号を持つ座標マーカー。
  子階層の Markers/Marker_01 などに付ける。

StoryEventActor
  イベント内で参照するキャラIDを持つ。
  Bubble表示位置もここで指定できる。

StoryYarnDialogueMarker
  Timeline上に置くYarn再生用Marker。
  指定ノードを再生し、必要なら会話完了までTimelineを止める。

StoryAutoSaveMarker
  Timeline上に置くオートセーブ用Marker。
```

基本セットアップ:

```text
StoryEvent_XXX
  StoryEventController
  PlayableDirector
  Markers
    Marker_01 + StoryEventMarker
    Marker_02 + StoryEventMarker
  Actors
    Iris + StoryEventActor(actorKey: iris)
```

Timeline 上では `Story/Yarn Dialogue` Marker を追加して、Yarn node名を設定する。

```text
Story/Yarn Dialogue
  Node Name: Prologue_01
  Pause Timeline Until Complete: true
  Bubble Actor Key: iris
```

`Pause Timeline Until Complete` が true の場合:

```text
Timeline Marker到達
  -> Yarn再生
  -> Timeline Pause
  -> 会話完了
  -> Timeline Resume
```

既存の `StoryEventRunner` から呼ぶ場合は、Action Type に `PlayStoryEventTimeline` を選び、`Target Name` に `StoryEventController.eventId` または GameObject 名を入れる。

### Phase 2: よく使う演出を Timeline Clip 化する

- Actor移動
- Object移動
- 暗転
- カメラ移動
- ズーム
- Shake
- Audio

Signal だけで作ると数が増えて見づらくなるため、頻出処理は専用 Clip にする。

### Phase 3: Editor 補助

- イベント作成テンプレート
- Marker 自動採番
- Marker 一覧表示
- 未設定項目の警告
- Yarn ノード存在チェック
- Play Mode 中のイベントテスト実行

この段階で、イベント制作者がかなり量産しやすくなる。

### Phase 4: GUI品質の強化

企画担当者がアジャイル的にイベントを作るため、最終的には専用 GUI の品質を上げる。

- イベント全体を一覧できる専用 Window
- Timeline、Yarn、Marker、Actor Binding の状態を一画面で確認
- Marker 番号と Scene View 上の表示を同期
- 選択中 Marker へ Scene View をフォーカス
- Scene View 上で Marker 番号ラベルを表示
- 未設定、参照切れ、重複番号、存在しない Yarn ノードを分かりやすく警告
- ワンクリックで Play Mode テスト
- 任意の Timeline 時刻や Yarn ノードからテスト再生
- イベント複製時の eventId / flag / Timeline 名の自動リネーム補助
- よく使う演出テンプレートの挿入
- 企画担当者向けの用語で表示する Inspector

GUI は、内部構造をそのまま露出するのではなく、イベント制作の作業手順に合わせて設計する。

例えば `PlayableDirector` や `SignalReceiver` の細かい実装名を前面に出すより、以下のような制作上の言葉で操作できる状態を目指す。

```text
会話を流す
キャラを移動
暗転
カメラを寄せる
画面を揺らす
SEを鳴らす
セーブする
```

## 判断基準

今後迷ったときは、以下を基準にする。

- 企画担当者が自分で試行錯誤できるか
- 修正から再確認までのサイクルが短いか
- GUI が分かりやすく、ミスに気づきやすいか
- イベント制作者が C# を触らなくて済むか
- Timeline 上でイベント全体の流れが見えるか
- マーカーや演出対象がイベント内で完結しているか
- 固有名詞やシーン全体検索に依存しすぎていないか
- 既存の DialogueManager / SaveManager / CameraManager / StageBgmController を活かせるか

## 結論

このイベントシステムは、以下を基軸にする。

```text
1イベント = 1 Timeline
StoryEvent = 実行条件と完了管理
Timeline = 演出の本体
Yarn = 会話本文
Marker = イベント内ローカル番号
GUI = 企画担当者が試行錯誤するための制作面
```

この形にすることで、ハードコードされた演出コマンドや固有名詞マーカーを減らし、Timeline と Prefab の複製だけでイベントを量産できる構造にする。

量産効率は重要だが、それ以上に、企画担当者がイベントのテンポ、見せ方、違和感を自分で触りながら詰められることを重視する。
