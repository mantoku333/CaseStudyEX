Camera ownership update for Fix_0925_NYAA — verified in Unity 6000.3.8f1 with Cinemachine 3.1.6.

The restored game was measured before the first production edit. A supplementary reference replay temporarily restored the original gameplay scripts from revision `abc1924ecf1bf74f22bb3b4f609c90ef7eeac736` to compare the same conversation-first entry order. The final verified source was restored afterward and its file hashes checked. Camera identities below were read from `CinemachineBrain.ActiveVirtualCamera` in Play mode; none of these cameras needed replacing.

| Encounter | Brain-selected combat camera, before and after |
| --- | --- |
| First StageBoss | `Stage/Boss/BossArea1/CN_Boss` |
| Second StageBoss | `Stage/Boss/BossArea (1)/CN_Boss` |
| LastBoss | `Stage/Area/324/CN_324_Boss` |

These are recorded runtime measurements at 16:9. For each entry order, 43 samples per encounter compared identical player and boss coordinates: center, both reachable horizontal limits, movement between those positions, and boss movement with the player held in place. Maximum actor-coordinate differences between each reference and final replay were zero. The camera remained free to move during these measurements.

| Encounter | Center camera position: before → after | Center orthographic size: before → after | Left-to-right camera travel: before → after |
| --- | --- | --- | --- |
| First StageBoss, arena first | `(244.4794, -54.7406, -34)` → same | `10.5000` → same | `12.8112` → `12.8112` |
| Second StageBoss, arena first | `(658.9501, 53.8494, -34)` → same | `10.0000` → same | `15.3555` → `15.3555` |
| LastBoss, arena first | `(1065.8483, 117.5512, -10)` → `(1065.8489, 117.5661, -10)` | `12.9850` → `13.0000` | `10.8871` → `10.8865` |

For arena-first entry, the first boss's left/right camera X positions were `238.453873 / 251.265045`, with orthographic size approximately `10.5` at both edges. The second boss's positions were `653.0763 / 668.4318`, with size approximately `10`. These values match the final implementation exactly. LastBoss's reference edge positions were `1065.84827 / 1076.73535`; final positions were `1065.84888 / 1076.73535`, with size approximately `13` at both edges.

Both StageBoss encounters also matched all 43 conversation-first reference samples exactly, including position, size, and normalized actor framing. LastBoss's maximum absolute difference on any camera-position axis was `0.02179` units for arena-first entry and `0.01611` for conversation-first entry. Maximum size differences were `0.0150156` and `0.0110302`, respectively. Its largest normalized actor-framing difference was `0.0005989`, under 0.06% of a viewport dimension. These are measured residuals; no claim of bit-for-bit equality is made for LastBoss.

The change introduces a live Brain override above Timeline camera overrides. Ownership begins at player-body overlap or the matching pre-fight event, while the original fully-inside test still controls combat, confinement, and introduction timing. Story camera move/zoom playables and room/portal handoffs are suppressed while ownership is held. Dialogue, actor movement, other Timeline tracks, UI, audio, and progression continue through their existing paths. Ownership lasts through post-fight event cleanup, and room restoration is guarded against a second handoff.

The existing `DualTargetCameraTarget` remains the movement driver. Its only change is an optional incoming-view position for its one-time initialization. An early ownership handoff otherwise makes `Camera.main` refer to the boss camera and can change the initial panning position. Capturing the incoming view removes that reseeding effect. Per-frame panning and zoom calculations, smoothing, follow offsets, target framing, zoom settings, and bounds calculations were not changed. No camera-bounds cache or new viewport clamp was added. The disabled-collider bounds behavior is left as restored. The scene asset and camera asset settings were not edited.

Final verification completed with:

- 32 passing automated Edit mode regression tests: 6 ownership/initialization tests, 21 existing StageBoss introduction tests, and 5 existing story-completion tests.
- 66 passing runtime lifecycle assertions across all three encounters, covering confirmed partial-body entry before combat, conversation-first entry, introduction and combat activation, competing room/portal/Timeline requests, cancellation, death, the real game-over retry action, same-scene loading, boss defeat, post-fight conversation cleanup, repeat completion, and normal room cameras afterward.
- Zero Brain-selection violations across 16,967 observed owned frames in the final framing and lifecycle runs. The lifecycle observer recorded 63 dialogue frames, including 23 post-fight dialogue frames.
- A separate active-scene transition probe: ownership released synchronously, stayed released, and the Brain selected `Stage/Area/109/CN_109` afterward.
- Successful final compilation, unchanged verified source hashes after the reference checks, and a clean whitespace check.

The tests used a background Editor in real Play mode. They teleported the player to encounter approaches, advanced dialogue through the existing runner, let introduction/combat activation execute, then held actors for repeatable framing sweeps. Defeat used existing damage APIs. Retry used the existing game-over action with isolated checkpoint slot 10 selected. Test saves were redirected through the project's existing Editor-only save-directory override into `.scraps/encounter-camera-saves`. This was not a manual controller playthrough, an audio listening review, or a visual audit of every UI transition. Other aspect ratios and other production scenes were not exhaustively tested.

Reload stress tests logged an existing SaveManager registry error involving the authored disabled `Stage/Area/221/Col_221`. A fresh save/reload on the restored original code reproduced the same destroyed-module exception and mapped the stale module to that object. The original checkpoint still loaded, and the final camera cleanup/reacquisition assertions passed. That separate save-registry issue and an original inactive `Event_01` coroutine warning were not changed by this camera task. This limits claims about the wider save/event systems beyond the camera results above.

The initial body-entry probe did not establish a physical overlap for two arenas; the corrected probe explicitly recorded `Collider2D.Distance(...).isOverlapped == true` and passed for all three. The final lifecycle run incorporates that corrected setup and has no failed assertions. Superseded measurements are retained locally under `.scraps/EncounterCamera/FirstPassEvidence`.

Recorded evidence:

- [Original arena-first baseline](<C:/Unity projects for classes/4thyr/CaseStudyEX/outputs/boss-camera/baseline.jsonl>) and [baseline source context](<C:/Unity projects for classes/4thyr/CaseStudyEX/outputs/boss-camera/baseline-context.json>).
- [Restored conversation-first reference](<C:/Unity projects for classes/4thyr/CaseStudyEX/outputs/boss-camera/baseline-conversation-first.jsonl>) and [reference context](<C:/Unity projects for classes/4thyr/CaseStudyEX/outputs/boss-camera/baseline-conversation-context.json>).
- [Final arena-first comparison](<C:/Unity projects for classes/4thyr/CaseStudyEX/outputs/boss-camera/comparison-arena-first-final.json>) and [final conversation-first comparison](<C:/Unity projects for classes/4thyr/CaseStudyEX/outputs/boss-camera/comparison-conversation-first-final.json>).
- [Final lifecycle record](<C:/Unity projects for classes/4thyr/CaseStudyEX/outputs/boss-camera/lifecycle-final.jsonl>), [body/portal challenge](<C:/Unity projects for classes/4thyr/CaseStudyEX/outputs/boss-camera/partial-body.jsonl>), and [different-scene release](<C:/Unity projects for classes/4thyr/CaseStudyEX/outputs/boss-camera/scene-change.json>).
- [Restored-code save-registry reproduction](<C:/Unity projects for classes/4thyr/CaseStudyEX/outputs/boss-camera/save-registry-reference.json>) and [its console record](<C:/Unity projects for classes/4thyr/CaseStudyEX/outputs/boss-camera/save-registry-reference-console.json>).

Follow-up: gradual encounter entry, verified on 2026-10-20.

All three encounters now use the Brain's existing blend definition for entry: `EaseInOut`, `2.4` seconds in this scene. The ownership override takes effect immediately, but interpolates from a frozen copy of the Brain's rendered view into the live encounter camera. Capturing the rendered view also handles a room transition already in progress. The outgoing room/story camera cannot continue steering this blend. Repeated acquisition does not restart it, and unscaled time lets it finish during a dialogue pause. The snapshot is discarded on completion or release. No camera pose, lens, target, driver, follow offset, damping, zoom limit, or bounds code was changed for this follow-up.

A fresh Play mode recording before this edit showed the output already at the encounter camera's pose and zoom by the fourth sampled entry frame, about `0.07–0.10` seconds after acquisition. After the edit, the six arena-first/conversation-first cases had destination weights of `0.00072–0.00277` at that point, `0.482–0.509` around 1.2 seconds, and were fully blended by the first sampled completed frame at `2.43–2.50` seconds. Sampling occurs every four frames, so these completion observations are not claims that the configured duration exceeds 2.4 seconds. All six weights advanced monotonically despite a repeated acquisition request and a deliberate gameplay pause. The Brain still selected the expected encounter camera throughout all 1,077 observed entry frames.

Combat was replayed at 16:9 with the same controlled actor positions and 43 center/edge/movement samples per encounter in each entry order. Compared with the previously accepted ownership implementation:

| Encounter | Arena-first maximum position / size difference | Conversation-first maximum position / size difference | Horizontal edge travel after this edit |
| --- | --- | --- | --- |
| First StageBoss | `0 / 0` | `0 / 0` | `12.811172` |
| Second StageBoss | `0 / 0` | `0 / 0` | `15.355500` |
| LastBoss | `0 / 0` | `0.016110 / 0.0110302` | `10.886470` arena-first; `10.887350` conversation-first |

The LastBoss conversation-first actor-framing difference from the accepted recording was at most `0.0004425` of a viewport dimension (0.045%). That replay matched the original restored conversation-first baseline exactly. The arena-first comparison with the original restored baseline retained the previously measured LastBoss residuals of at most `0.02179` units and `0.0150156` orthographic size. These are measured settling differences; exact equality is claimed only where the recorded comparison is zero. All actor-coordinate comparisons were zero. The Brain-selected camera identities remain the three paths listed above.

Fresh follow-up verification passed 9 ownership/entry regression tests and all 66 runtime lifecycle assertions, including introduction, dialogue, combat, room/portal/Timeline challenges, cancellation, death, real game-over retry, loading, defeat, post-fight dialogue, and normal room-camera use afterward. The entry, two framing, and lifecycle runs observed 18,034 owned Brain updates with zero selection violations. A separate scene-change test cancelled ownership during an active entry blend, released synchronously, and selected `Stage/Area/109/CN_109` afterward. Source hashes confirmed that the only gameplay-source change since the accepted implementation is `EncounterCameraOwnership.cs`; its test file also changed.

Verification used the background Unity Editor, automated dialogue advancement, controlled actor sweeps, and isolated test saves as described above. A rendered camera image was inspected; the background Editor had no Game-view render target, so this image omits overlay UI. This was not a manual controller playthrough or an audio/UI audit. The same 14 previously reproduced stale SaveManager-module errors appeared during reload stress; no new error category appeared in the recorded lifecycle console. The wider save-system limitation described above still applies.

Follow-up evidence:

- [Entry before](<C:/Unity projects for classes/4thyr/CaseStudyEX/outputs/boss-camera/entry-transition-before.jsonl>), [entry after](<C:/Unity projects for classes/4thyr/CaseStudyEX/outputs/boss-camera/entry-transition-after.jsonl>), and [transition measurements](<C:/Unity projects for classes/4thyr/CaseStudyEX/outputs/boss-camera/entry-transition-summary.json>). The before recorder's selection counter was sampled before the Brain's LateUpdate; ownership assertions in the after recorder use `CameraUpdatedEvent`.
- [Comparison with the accepted implementation](<C:/Unity projects for classes/4thyr/CaseStudyEX/outputs/boss-camera/comparison-smooth-vs-accepted.json>), [arena-first comparison with the restored baseline](<C:/Unity projects for classes/4thyr/CaseStudyEX/outputs/boss-camera/comparison-arena-first-smooth.json>), and [conversation-first comparison with the restored baseline](<C:/Unity projects for classes/4thyr/CaseStudyEX/outputs/boss-camera/comparison-conversation-first-smooth.json>).
- [Entry regression tests](<C:/Unity projects for classes/4thyr/CaseStudyEX/outputs/boss-camera/smooth-entry-tests.json>), [lifecycle assertions](<C:/Unity projects for classes/4thyr/CaseStudyEX/outputs/boss-camera/lifecycle-smooth.jsonl>), [scene-change release](<C:/Unity projects for classes/4thyr/CaseStudyEX/outputs/boss-camera/scene-change-smooth.json>), and [runtime console](<C:/Unity projects for classes/4thyr/CaseStudyEX/outputs/boss-camera/smooth-runtime-console.json>).

