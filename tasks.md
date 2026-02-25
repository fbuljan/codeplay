# Full Implementation Roadmap

## Context
GameManager + PlayerController + InputProcessor are working. This is the build order for all remaining systems, organized by dependency (each step builds on the previous).

---

## Phase 1 — Core Game Loop (Buttons Only, Single Lane)

### Step 1: Infinite Ground
**Why first:** Everything spawns on the ground. Can't test anything without it.
- Pool of ground plane segments (reuse the 3 lane planes concept)
- As player moves +Z, detect when approaching the end of current segment
- Reposition the farthest-behind segment ahead of the farthest-forward one
- Destroy/recycle anything behind the player (cleanup threshold)
- **New script:** `GroundScroller.cs` — managed by GameManager

### Step 2: Game State Machine
**Why second:** Need Play/GameOver flow before health matters.
- States: `Menu`, `Playing`, `GameOver`
- `Menu`: show start prompt, wait for any button press
- `Playing`: enable player movement, spawning, scoring
- `GameOver`: freeze game, show score, wait for restart input
- Lives in `GameManager.cs` (expand existing)
- **New script:** `UIManager.cs` — simple TextMeshPro overlays for state text, score, health

### Step 3: Player Health
**Why third:** Needed before obstacles/enemies can matter.
- Health pool (e.g., 3 HP)
- `TakeDamage()` method
- Invincibility frames after hit (brief flash/blink)
- On health <= 0 → tell GameManager → GameOver state
- **New script:** `PlayerHealth.cs` on Player object

### Step 4: Slide Mechanic
**Why now:** Completes the 4-button moveset before obstacles need it.
- On slide button press: shrink CapsuleCollider height (e.g., 2 → 0.5) and lower center
- Timer-based duration (e.g., 0.5s), auto-restore
- Can't jump while sliding, can't slide while airborne
- Add to `PlayerController.cs` — subscribe to `InputProcessor.OnSlidePressed`

### Step 5: Obstacle Spawning
**Why now:** First real gameplay challenge.
- **SpawnManager.cs** — spawns obstacles at intervals ahead of player (+Z)
- Two obstacle types:
  - **Low obstacle** (cube on ground) → must jump over
  - **High obstacle** (cube at head height) → must slide under
- Tag-based: `"ObstacleLow"`, `"ObstacleHigh"`
- On collision with player → `PlayerHealth.TakeDamage()`
- Object pooling for performance
- Destroy/recycle when behind player

### Step 6: Ground Enemies
- Spawn via SpawnManager in the player's lane
- Stationary or slowly moving toward player
- Health = 1, destroyed by player's shot
- On collision → damage player (unless shield active)
- Simple cube/capsule with distinct color
- **New script:** `Enemy.cs` (base), `GroundEnemy.cs`

### Step 7: Player Shooting
- On shoot button press: fire a projectile forward (+Z)
- Simple small cube/sphere, fast velocity
- Raycast OR physics projectile (projectile is more visible)
- Cooldown (e.g., 0.3s between shots)
- On hit enemy → destroy enemy, add score
- **New script:** `Projectile.cs`
- Add shooting logic to `PlayerController.cs`

### Step 8: Player Shield
- On shield button press: activate shield (brief invulnerability, e.g., 1s)
- Cooldown (e.g., 3s)
- Visual indicator: colored sphere around player or material swap
- Blocks ground enemy collision damage
- Blocks air enemy projectiles
- Add to `PlayerController.cs` or `PlayerHealth.cs`

### Step 9: Air Enemies
- Spawn via SpawnManager, fly alongside player (match Z speed)
- Hover above ground at fixed Y height
- Shoot projectiles at player at intervals
- Can be destroyed by player shooting at them
- When destroyed → score + stop shooting
- Eventually despawn if alive too long
- **New script:** `AirEnemy.cs` (extends Enemy base)
- **New script:** `EnemyProjectile.cs` — moves toward player, blocked by shield, damages on hit

### Step 10: Score System + Collectibles ✅
- Distance-based scoring: `pointsPerMeter` (default 1) in `GameManager.cs`
- Kill bonus: `enemyKillScore` (default 100), wired via `Enemy.OnDestroyed` → `SpawnManager` → `GameManager.OnEnemyKilled`
- Score multiplier: starts at x1.0, increases per coin (+0.1) and per kill (+0.25), resets to x1.0 on damage
- All score gains multiplied by current multiplier (distance, kills, coins)
- Multiplier displayed in HUD (white → green → gold at x2.0+)
- **Collectibles:**
  - `Coin.cs` — rotating sphere, 40% spawn chance, gives `coinScoreBase` (50) × multiplier
  - `HealthPickup.cs` — bobbing sphere, 5% spawn chance, restores 1 HP (if below max)
  - Both pooled in `SpawnManager`, spawned offset from obstacle spawn points
- `PlayerHealth.cs` — added `Heal()` method and `MaxHealth` property
- No separate `ScoreManager.cs` — scoring lives in `GameManager` directly

### Step 11: Difficulty Scaling + Smart Spawn System
- `DifficultyManager.cs` — ramps parameters over time/distance:
  - Player forward speed (gradual increase)
  - Spawn interval (shorter = more obstacles/enemies)
  - Enemy density
  - Mix of obstacle types
  - Air enemy frequency
- All values parameterized, tunable in inspector
- **Smarter spawn logic** — replace simple "every X distance, random obstacle" with:
  - Spawn across all 3 lanes (not just center)
  - Patterns that appear random but are curated (e.g., never block all 3 lanes at once)
  - Ensure at least one lane is always passable
  - Vary groupings: single obstacles, pairs across lanes, staggered sequences
  - Weighted randomness based on distance/difficulty tier
- **3rd obstacle type: Full-lane blocker** (Phase 3 only, requires lane switching)
  - Spans entire lane width, cannot be jumped or slid under
  - Forces player to lane-switch to dodge
  - Spawn rule: max 2 full-lane blockers per row — never 3, so there's always a free lane
  - Can combine freely with low/high obstacles (e.g., blocker + low + high across 3 lanes is valid)

---

## Phase 2 — Analog Stick (Aiming Across Lanes)

### Step 12: 3-Lane Obstacle/Enemy Distribution
- Obstacles and enemies now spawn in random lanes (not just center)
- Player is still in center lane (no movement yet)

### Step 13: Aiming System ✅
- Joystick X selects lane (left/center/right), Y selects height (ground/air)
- Hitscan shooting with BoxCast along aimed lane corridor
- Yellow laser tracer visual (LineRenderer, brief flash)
- Reticle diamond quad shows aim position, red=ground / blue=air
- Joystick amplitude normalization for hardware flexibility
- Air enemies now spawn on random lanes via `AirEnemy.SetLane()`
- **TODO (polish):** Fine-tune BoxCast half-extents, tweak tracer duration/width, review edge cases with shooting through obstacles

---

## Phase 3 — Tilt (Lane Switching + Coins)

### Step 14: Lane Switching ✅
- Tilt X axis normalized via `tiltAmplitude` (same pattern as joystick)
- Threshold-based lane detection (`tiltLaneThreshold`)
- Cooldown (`laneSwitchCooldown = 0.3s`) prevents rapid jitter
- Smooth velocity-based X movement in `MoveForward()` via Rigidbody
- Resets to center lane on game over/restart
- All in `PlayerController.cs` using `InputProcessor.GetTilt()`

### Step 15: Coins
- Spawn in random lanes via SpawnManager
- Rotating visual (small cube/sphere)
- Trigger collider → collect on touch
- Add to score
- Gives purpose to lane switching

---

## Script Summary

| Script | Purpose | Step |
|--------|---------|------|
| `GroundScroller.cs` | Infinite scrolling ground segments | 1 |
| `UIManager.cs` | HUD: score, health, state screens | 2 |
| `PlayerHealth.cs` | HP, damage, invincibility, death | 3 |
| `PlayerController.cs` | (expand) slide, shoot, shield, lane switch | 4,7,8,14 |
| `SpawnManager.cs` | Spawn obstacles, enemies, coins ahead of player | 5 |
| `Obstacle.cs` | Obstacle collision → damage | 5 |
| `Enemy.cs` | Base enemy class | 6 |
| `GroundEnemy.cs` | Walk/stand, collide for damage | 6 |
| `Projectile.cs` | Player bullet | 7 |
| `AirEnemy.cs` | Fly alongside, shoot at player | 9 |
| `EnemyProjectile.cs` | Enemy bullet, blocked by shield | 9 |
| `Coin.cs` | Rotating collectible, score + multiplier | 10 |
| `HealthPickup.cs` | Bobbing collectible, restores 1 HP | 10 |
| `DifficultyManager.cs` | Ramp speed, spawn rate, density | 11 |

---

## Polish (If Time Remains)

### Visual
- Emissive materials
- Bloom post-processing
- Trail behind player
- Particles on shoot

### Audio
- Jump, shoot, coin, game over sounds

### UX
- Screen shake on hit
- Flash effect on damage
- Score animation

---

## MVP Checkpoint

Minimum viable version must have:
- 1 lane
- Jump + Slide
- Enemy + Shoot
- Score
- Game Over

Everything else is bonus.
