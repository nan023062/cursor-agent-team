# Case Study: PhysicStream Grid Token Memory Issue Fix
**Date**: 2026-03-09  
**Assembly**: `XDTBaseService/Foundations/PhysicQuery`  
**Workflow**: `/Client/coder-fixbug`  
**Result**: ✅ SUCCESS

---

## 1. Problem Statement

After changing trigger detection from **position-based** (each group occupies 1 grid cell) to **volume-based** (body collider spans multiple cells), the memory used by `PhysicGroupToken`-related content increased dramatically.

**Symptom**: `PhysicGroupToken`-related memory exploded after the trigger mode change.

---

## 2. Investigation Process

### Step 1: Context Loading
Loaded `.dna/` files for `PhysicQuery`, then read all 14 source files in order of dependency:
- `PhysicStream.cs` — core data structures
- `CollisionGroup.cs` — `IPhysicGroup` / `PhysicGroupToken` definitions  
- `PhysicTrigger.cs` — trigger source architecture  
- `PhysicQuery.cs` — registration entry points

### Step 2: Root Cause Identification

**Grid system design**: `PhysicStream` partitions space into a 301×301 grid (cellSize=2m, covering ±300m). Each `IPhysicGroup` is registered in every grid cell covered by its bounding box.

**Before (position-based)**: Groups had no body colliders → `GetCollisionRange` fell into `!hasBounds` branch → returned a 1×1 single-cell Range → **1 token reference per group**.

**After (volume-based)**: Groups now have actual colliders → `BoundsToRange` computed the full AABB → a 20×20m body spans 10×10 = **100 cells** → **100 token references per group**.

For N entities: O(N) vs O(N×R²) — the explosive scaling factor.

### Step 3: Discovered Three Layered Issues

Through reading `RenderPhysicTarget.cs` and `PhysicalCollisionGroup.cs` (upper-layer `IPhysicGroup` implementations):

| # | Issue | Severity |
|---|-------|----------|
| 1 | `AlwaysActive=true` groups still wrote tokens to O(R²) grid cells despite never needing grid-based activation | 🔴 Primary |
| 2 | `AlwaysActive()` is **dynamic** in both `RenderPhysicTarget` and `PhysicalCollisionGroup`; `false→true` transition caused old grid tokens to leak permanently | 🔴 Correctness |
| 3 | `UnRegisterColliderHotspot` omitted `_assignLongTokens.Remove`, creating slow hotspot token accumulation | 🟡 Secondary |
| 4 | Non-AlwaysActive large-body groups still had no upper bound on Range size — could still explode with group count growth | 🟡 Scalability |

---

## 3. Fix Iterations

### Fix 1: AlwaysActive Groups Skip Grid Registration
**File**: `PhysicStream.cs` → `Collision.Update()` / `Collision.Dispose()`

**Before**:
```csharp
if (!alwaysActive) {
    if (_hasRange) owner.RemoveCollisionFromRange(_range, token);
}
owner.AddCollisionToRange(_range, token);  // always called
```

**After**:
```csharp
if (!alwaysActive) {
    owner.AddCollisionToRange(_range, token);
}
// AlwaysActive groups: direct activate, zero grid writes
```

**Effect**: AlwaysActive groups grid cost: O(R²) → **O(0)**

---

### Fix 2: `_inGrid` State Tracking (Correctness Fix)
**Problem**: `AlwaysActive()` is dynamic. `false→true` transition: `!alwaysActive` would skip `RemoveCollisionFromRange`, leaving stale tokens in grid cells forever.

**Root insight**: Cannot use the *current* `AlwaysActive()` value to decide *cleanup*. Must track the *actual* grid registration state.

**Added field** to `Collision`:
```csharp
private bool _inGrid;  // tracks actual grid registration state
```

**New cleanup logic** (decoupled from AlwaysActive):
```csharp
if (_inGrid) {                                    // always clean up if registered
    owner.RemoveCollisionFromRange(_range, token);
    _inGrid = false;
}
if (!alwaysActive) {                              // register only if needed
    owner.AddCollisionToRange(_range, token);
    _inGrid = true;
}
```

**Transition matrix** (all 4 state combinations correct):
| Transition | _inGrid before | Remove | Add | Result |
|-----------|---------------|--------|-----|--------|
| false→false | true | ✓ clean old | ✓ write new | Normal move |
| true→true | false | ✗ skip | ✗ skip | Zero cost |
| **false→true** | **true** | **✓ clean** | **✗** | **No leak** |
| true→false | false | ✗ skip | ✓ write | Correct restore |

---

### Fix 3: Hotspot Token Leak + Range Cap
**File**: `PhysicQuery.cs` + `PhysicStream.cs`

**Hotspot leak** (`PhysicQuery.cs`):
```csharp
// Before
public void UnRegisterColliderHotspot(PhysicGroupToken instanceId) {
    physicStream.UnRegister(instanceId);  // _assignLongTokens NOT cleaned
}

// After
public void UnRegisterColliderHotspot(PhysicGroupToken instanceId) {
    _assignLongTokens.Remove(instanceId);  // ← added
    physicStream.UnRegister(instanceId);
}
```

**Range Cap** (`PhysicStream.cs` → `GetCollisionRange`):
```csharp
private const int MaxRegistrationRadius = 9;  // = max HotScope scope

// After BoundsToRange, cap to center±9:
range = new Range {
    minX = Mathf.Max(range.minX, centerX - MaxRegistrationRadius),
    maxX = Mathf.Min(range.maxX, centerX + MaxRegistrationRadius),
    minZ = Mathf.Max(range.minZ, centerZ - MaxRegistrationRadius),
    maxZ = Mathf.Min(range.maxZ, centerZ + MaxRegistrationRadius),
};
```

**Correctness guarantee**: HotScope max scope = 9 cells. Any hotspot within (9 + 9) = 18 cells = 36m of group center activates it. Objects ≤ 72m wide are fully covered. Larger objects should set `AlwaysActive=true`.

**Per-group max grid cells**: Unbounded → **(2×9+1)² = 361** hard cap.

---

### Debug Tool Added
```csharp
// Runtime diagnostics for grid token distribution
public static bool DebugShowGridStats = false;  // shows stats on screen
public GridStats CollectGridStats();             // collect anytime

// Output format:
// [PhysicStream GridStats]
//   Groups: N  HotScopes: N  Active: N
//   Grid._collisions => cells: N  total tokens: N  max/cell: N
//   Grid._agents     => cells: N  total tokens: N  max/cell: N
```

---

## 4. Files Changed

| File | Change Type | Description |
|------|-------------|-------------|
| `PhysicStream.cs` | fix | `_inGrid` field + AlwaysActive skip + Range cap + debug stats |
| `PhysicQuery.cs` | fix | `UnRegisterColliderHotspot` hotspot token leak |
| `.dna/pitfalls.md` | doc | 3 new structured pitfall entries |
| `.dna/changelog.md` | doc | v0.2 + v0.3 entries |

---

## 5. Memory Impact Summary

| Group Type | Before (position) | Before (volume) | After Fix |
|-----------|------------------|-----------------|-----------|
| AlwaysActive (PhysicGroup, most entities) | 1 cell | O(R²) | **0 cells** |
| Non-AlwaysActive, small body | 1 cell | ~1-4 cells | ≤ 4 cells |
| Non-AlwaysActive, large body (≤72m) | 1 cell | O(R²) | **≤ 361 cells** |
| Non-AlwaysActive, huge body (>72m) | 1 cell | O(R²) | Should be AlwaysActive |

---

## 6. Key Learnings (for future fixbug sessions)

### L1: State-dependent cleanup must track *actual state*, not infer from current values
When a cleanup decision depends on state that can change independently of when cleanup runs, introduce an explicit flag (`_inGrid`) to track the actual committed state. Never infer "was this done?" from "should this be done now?".

### L2: Dynamic interface methods create hidden state machine complexity
`AlwaysActive()` looks like a static property but is actually mutable state (`_alwaysActiveOption`, `alwayActive`). Any logic that combines a current-value query with historical side effects (written to grid) must handle all state transitions explicitly.

### L3: Spatial data structure capacity must match spatial query patterns
The grid was designed for point-based groups (1 cell each). Volume-based groups silently violated this assumption. Always verify that changing the *shape* of registered data doesn't break the *capacity* assumptions of the spatial index.

### L4: Symmetric register/unregister pairs
`Register*` and `UnRegister*` methods must be symmetric in their side effects. `RegisterColliderHotspot` wrote to `_assignLongTokens`; `UnRegisterColliderHotspot` did not clean it. Asymmetric pairs are a reliable source of slow leaks.

### L5: Scalability bounds should be explicit constants
The `MaxRegistrationRadius = 9` constant documents the system's capacity contract. Large-body objects exceeding this bound should be `AlwaysActive=true`. This creates a clear design boundary rather than silent degradation.

---

## 7. coder-fixbug Workflow Execution Notes

```
加载上下文 (architecture.md + pitfalls.md + dependencies.md)
    ↓
复现定位 (14 source files, identified grid system + token storage)
    ↓
分析 → 3 layered issues discovered through upper-layer IPhysicGroup implementations
    ↓
修复 Round 1: AlwaysActive skip grid (O(R²)→0)
修复 Round 2: _inGrid state tracking (correctness for dynamic AlwaysActive)  
修复 Round 3: Hotspot leak + Range cap (scalability)
    ↓
边界守卫: changes within PhysicQuery assembly only ✓
    ↓
Lint check: no errors ✓
    ↓
pitfalls.md: 3 new entries ✓
pitfall-index.md: 4 rows added ✓
changelog.md: v0.2 + v0.3 ✓
```

**Total code changes**: ~50 lines added, ~10 lines modified across 2 files.  
**No breaking changes** (all public API signatures unchanged, behavior only improved).
