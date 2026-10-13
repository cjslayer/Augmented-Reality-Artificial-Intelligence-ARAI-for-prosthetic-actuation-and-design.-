# Morphology viewer (2026-10-13)

The viewer is an Editor-only, Play-mode tool. It shows the run-014 hand compositions and their actuation-mask states without a trainer or a policy.

**Files:**
- `Assets/Scripts/Diagnostics/MorphologyViewer.cs`: a runtime MonoBehaviour under `#if UNITY_EDITOR`, compiled out of players.
- `Assets/Editor/MorphologyViewerMenu.cs`: the menu item.

No scene, prefab, agent, MorphologyManager or ArticulatedHand file was changed.

## How to launch

1. Open the project in the Editor, stopped (not in Play).
2. Choose the menu **ARAI → Morphology Viewer**.
   - The menu item opens `Assets/Scenes/Dynamic_Scene.unity` if another scene is active. It asks to save a modified scene first and never edits Dynamic_Scene itself.
   - It sets a one-shot SessionState flag and enters Play.
   - The viewer spawns as `MorphologyViewer (runtime only)`. An ordinary Play (without the menu) does not spawn it.
3. Click into the Game view so it receives the keys.
4. Exit Play to close the viewer. Everything it created is runtime-only and disappears with Play mode.

**Refusals:**
- The viewer does not start if `Temp/gates.json` exists, because that means a harness pass is pending.
- The menu item does nothing while Play mode is already on.
- Never run the Editor harness (`tools/bo/gates_runner.run_pass`, `tools/eval/eval_014.py`) while the viewer is in Play. Exit Play first.

## Keys

| key | action |
|---|---|
| 3, 4, 5, 7 | build the 3-, 4-, 5- or 7-digit hand |
| 6 | toggle between 6a (sixth finger) and 6b (second thumb) |
| A | auto-cycle through all six compositions, every 3 s (toggle) |
| M | next mask state: all on → each present joint group off in turn → all on; masked links are tinted red |
| T | close-until-contact driver: close / open (toggle) |
| R | reset: 5-digit, all groups on, auto-cycle off |

The number-pad digits work too.

## What each view shows

**The hand:**
- It is the articulated skeleton, built by `ArticulatedHand.Rebuild` from the HandSpec for the selected composition (`HandLayout.Composition(digits, secondThumb)`, slot layout), at length scale 1 and inertia scale 1.
- It is drawn as its colliders: the capsule per link, the palm box and the forearm.
- The scene's skinned mesh has only five fingers, so it is hidden for the session.
- Normal links are grey-blue; masked links are red.

**The camera:**
- The main camera is repositioned for each composition. It looks at the palmar face from above the fingertips, so every finger and both thumbs of 6b and 7 are in view.
- The scene's object (the red cylinder in front of the palm) is left in place; it projects across the palm.
- Mask red is the darker tint on the digits. The panel also names the masked groups.

**Panel (top left):**
- composition name and slot string (slot order: primary thumb, index, middle, ring, pinky, extra finger, second thumb);
- link count (palm + driven groups + the thumb's fixed-MCP phalanx per thumb);
- hand mass in g (palm + digit links);
- palm width in mm (widened by 21 mm when the extra finger exists);
- powered / present groups, the masked groups and the mask-state index;
- driver state and auto-cycle state;
- the key legend.

**Masked groups:**
- A masked group is held stiffly at its neutral pose, exactly as in training: `ArmGraspAgent.ConfigureDrives` → `ArticulatedHand.ConfigureGroup(masked = true)`. That is an acceleration drive, k 4000, b 200, held at `neutralPoseDeg`.
- The extra finger takes the pinky's neutral angles; the second thumb takes the thumb's.

**T (driver):**
- T runs the harness's close-until-contact law: each powered group flexes at 90 °/s toward its power-grasp cap (base 60°, middle 70°, end 30°, thumb 45° / 45°). On contact with the Cylinder-tagged object, it holds 12° beyond the contact angle.
- Rates, caps and preload are read from `ArticulatedGates.Config` defaults.
- Pressing T again opens the hand at the same rate.
- Masked groups stay at their hold.

### Readouts per composition (nominal θ)

| composition | slots | links | groups | hand mass | palm width |
|---|---|---|---|---|---|
| 3 | 1110000 | 10 | 8 | 378 g | 85.0 mm |
| 4 | 1111000 | 13 | 11 | 399 g | 85.0 mm |
| 5 | 1111100 | 16 | 14 | 411 g | 85.0 mm |
| 6a | 1111110 | 19 | 17 | 498 g | 106.0 mm |
| 6b | 1111101 | 19 | 16 | 446 g | 85.0 mm |
| 7 | 1111111 | 22 | 19 | 533 g | 106.0 mm |

Screenshots, one per composition with all groups on, are in `results/014/viewer/composition_<c>.png`.

## How it works, and its limits

**Layout:**
- The Editor resolves the legacy layout. `HandLayout.Resolve` selects slots only from `--topology slots` on the command line, or from `Temp/gates.json`, and the latter would boot the harness.
- So the viewer does not run the agent in the slot layout. Instead it disables the scene's `ArmGraspAgent` (and its DecisionRequester) for the session and drives its `ArticulatedHand` directly.
- After each `Rebuild` it calls the agent's own private `CacheLinks` and `ConfigureDrives` by reflection. Drive gains, joint limits and the masked hold are therefore the agent's code, not a copy.
- The morphology watch overlay is switched off for the session, because it would show the disabled agent's stale state.

**Drive values:**
- k is the class reference stiffness, ζ = `referenceZeta` (0.7), from `MorphologyManager.Initialize`.
- In the legacy layout MorphologyManager computes nominal inertia for the five-digit groups only. The extra finger and second thumb therefore take their damping from the analogous pinky / thumb group's inertia. This only affects how they move under T; their geometry, mass and mask hold are exact.

**Driver:**
- The driver is a copy of the private `ArticulatedGates.ControllerStep` (power grasp, thumb active), plus a re-open. It lives in Diagnostics and uses only the public `ArmGraspAgent.SetGroupSetpoint`, the same setter the harness uses.
- No ArmGraspAgent code path was changed.

**No policy runs.** The viewer shows morphology and actuation, not 014CD's behaviour.

## Verification (2026-10-13, Editor log)

- All six compositions rebuilt with **0 errors** and 0 exceptions:
  - The `[MorphViewer] composition <c> built` lines are in the table above.
  - The only warnings were 4 "referenced script (Unknown) … is missing" at scene load, logged before the viewer started. They are pre-existing in Dynamic_Scene.
- M cycled every present group in every composition:
  - 8 / 11 / 14 / 17 / 16 / 19 single-group-off states plus all-on;
  - each `mask state i/N: <group> off` line was logged once per group, and the cycle returned to all on.
- The red tint was checked in screen captures: thumbBase on the 5- and 7-digit hands.
- T on the 5-digit hand (joint angles, deg):

  | | open | closed (4 s) | re-opened |
  |---|---|---|---|
  | finger base / middle / end | about 0 | −40 to −50 / −38 to −57 / −20 to −30 |  about 0 |
  | thumbEnd | 3 | 15 | 1 |
  | thumbBase | −9 | −10 | −9 |

  The thumb rests against the scene's cylinder.
- The keys were exercised through the same public methods (`Select`, `CycleMask`, `ToggleDriver`) from the Editor's eval tool. They were not pressed on a physical keyboard during verification.
