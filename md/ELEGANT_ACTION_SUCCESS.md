# Elegant action success chain

## Qualifying actions

- Glide: once when a glide session starts (temporary recoil interruption is not another session).
- Dive attack: once on an enemy hit. A real enemy bounce refines the result to `DiveBounce`; it does not add a second level. Existing combat physics still bounce on a kill, not on every hit.
- Dodge: once per dodge when the player crosses an enemy body or a hostile projectile. A hostile bullet's body contact during dodge also counts, since that bullet is destroyed by the existing collision handler. Reflected bullets do not count.
- Airborne recoil / recoil jump: once per recoil execution after at least 2 world units of displacement. Teleport-sized steps and a replacement recoil invalidate pending tracking.
- Normal attack: only a kill from behind against the same enemy whose head was crossed from front to back in the previous 2 seconds. Ordinary hits, front kills and unrelated enemy kills do not count.

## Chain and settlement

Any qualifying action starts level 1. Each subsequent successful execution adds one level, including repeated executions of the same action. There is no internal level-2 cap.

Default timeout is 1 scaled second. A successfully sustained action keeps the window open until it finishes; an unsuccessful attempt does not. Pausing scaled time pauses the window. At timeout or explicit settlement, award 10 points per success, apply the existing equipment multiplier, emit the existing `ElegantPointGainEvents.Gained` notification, then publish level 0. Settlement cannot award the same chain twice.

The point amount, timeout and recoil distance are serialized on `PlayerElegantPointController`. Head clearance (0.05 to 2 world units), rear-kill window and teleport threshold are on `ElegantActionSuccessSensor`. The sensor is also installed automatically on older player instances that have the point controller.

## FX integration

`PlayerElegantPointController` is the single source of truth:

- `ActionSucceeded(action, level)`: a new successful execution.
- `ActionRefined(action, level)`: hit result refined to bounce without another level.
- `LevelChanged(level)`: current chain level, zero on settlement.
- Existing `ElegantPointGainEvents.Gained`: committed points and absorption trigger.

`PlayerGracefulActionEffectManager.CurrentLevel` follows this chain, but legacy sparkle emission is now opt-in. `PlayerMovementTrail` reads the point controller directly and smoothly blends four visual levels; level 0 stops births without clearing particles, and levels above 4 retain the level-4 appearance. The old FX objects and manager are not required for the new trail. Gauge gain still uses the existing notification and pastel fallback absorption; the new ribbon geometry itself is not attracted into the HUD.

## Verification

Unity menu: `Tools > Effects > Verify Elegant Success System`. EditMode results are written to `Temp/ElegantSuccessTests/Results.xml` and `Result.txt`. Coverage includes chain timeouts, deduplication, swept physics detection, reflected bullets, head crossings, rear-kill qualification and recoil displacement. The suite also runs existing point integration tests; their HUD layout expectations predate the current HUD implementation.
