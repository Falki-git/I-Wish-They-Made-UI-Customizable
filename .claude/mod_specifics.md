# Mod-specific guidance — Customizable UI

Per-mod companion to the mod-agnostic root `CLAUDE.md`. Covers the mod being developed in
this repo, its legacy origin, and how it maps onto the Redux/SpaceWarp2 template.

## What the mod is

**"Customizable UI"** (full title *"I Wish They Made UI Customizable"*) lets players
**move, scale, and show/hide flight-scene HUD elements** — navball, staging, throttle,
resources, time-warp, IVA portraits, app bar, etc. — and **save/load** those layouts. The
player opens an editor window from the flight app-bar button (or `CTRL+I`), picks a UI
"group", then nudges / jumps / snaps it around the screen, scales it, or toggles its
visibility.

- **Author:** Falki · **Current version:** `1.0.0` (Redux/SpaceWarp2) · **Legacy mod_id:**
  `com.github.falki.customizable-ui` · **Last legacy version:** `0.3.2`
- **Legacy source (read-only reference):** `E:\GitHub\KSP2\CustomizableUI Legacy`
  (Unity/BepInEx project; core code under `CustomizableUIProject\`).
- **Legacy upstream:** <https://github.com/Falki-git/I-Wish-They-Made-UI-Customizable>

**Status: the Redux/SpaceWarp2 port is complete and released as v1.0.0.** The port replaced
every weakness called out below (global singletons, IMGUI, hardcoded lookups,
resolution-coupled math, DLL-adjacent save file) with the architecture described next. Treat
the "legacy behavioral spec" section further down as historical context only, not as a guide
to how the mod currently works.

## Current architecture (Redux/SpaceWarp2, `Assets/CustomizableUI/Code/`)

| File | Role |
|------|------|
| `CustomizableUIPlugin.cs` | `KerbalMod` entry point. Registers the flight app-bar button, subscribes to `FlightViewEnteredMessage`/`FlightViewLeftMessage`, and drives the per-frame `Update()` loop (init-polling fallback, `EnforceVisibility()`, keybind toggle) inside a try/catch that logs once per failure streak. |
| `Groups/GroupCatalog.cs` | Static display-name lookup (GameObject key → friendly name like `NAVBALL`) and the default "attach/scale with navball" key set. Purely cosmetic labeling — falls back to a prettified raw name for anything not listed, so an unrecognized group still shows up rather than being dropped. |
| `Groups/GroupHandle.cs` | Runtime wrapper around one discovered top-level group. All anchor/edge/center math is derived live from the resolved `RectTransform`'s own pivot/rect/lossyScale — no hardcoded per-group pixel offsets. Owns `Position`, `Scale` (clamped `0.25`–`3`), `IsActive`, `AttachToNavball`, `ScaleWithNavball`, and `ToLayout()`/`ApplyLayout()` for save/load. |
| `Groups/GroupLayout.cs` | Plain-data (de)serialized shape of one group's saved state — plain floats, not `Vector3`, so it doesn't depend on how the JSON library (de)serializes Unity value types. |
| `Groups/GroupRegistry.cs` | Singleton owning the discovered `List<GroupHandle>` for the current flight session. Discovers groups dynamically from `UIFlightHud`'s live children (plus the app-bar `ButtonBar`) instead of a hardcoded name/index table; warns (doesn't crash) on duplicate names or missing expected groups. Handles navball-follow position cascading and navball-scale cascading. |
| `Groups/OverlayCorrections.cs` | Small, explicit per-group pixel correction table for the selection overlay only (a few groups' resolved `RectTransform` bounds don't match where their content visually renders) — movement/slider math is untouched by this. |
| `UI/MainGuiController.cs` | Wires the UXML controls to `GroupRegistry` (MVVM-ish, no ViewModelBase). Owns the D-pad/jump/nudge buttons, X/Y/scale fields+sliders, toggles, Save/Load/Reset/FUBAR, the pending-changes indicator on Save, and the yellow selection overlay (converted into UitkForKsp2's fixed reference resolution). |
| `UI/SceneController.cs` | Owns the editor window's open/closed state; builds/destroys the `UIDocument` via `UitkForKsp2.API.Window.Create` and syncs the app-bar toggle. |
| `UI/Uxmls.cs` | Loads the mod's UXML through Addressables, lazily, on first access. |
| `Utilities/SaveLoadUtility.cs` | Saves/loads the layout as a **versioned** JSON file (`SaveFile { Version, Groups }`) in the mod's own data folder (not per-KSP2-save, matching legacy scope). Detects and transparently reads the old unversioned bare-array format from pre-1.0 saves. |
| `Utilities/Settings.cs` | `SWConfiguration`-backed keybind config (`EnableKeybinding`, `Keybind1`/`Keybind2`, default `CTRL+I`). |
| `Utilities/TransformExtensions.cs` | `FindDescendant` (BFS by name) and `ResolvePositionable` (BFS for the shallowest non-full-stretch `RectTransform` in a group's subtree — Redux commonly wraps the real widget in an invisible full-canvas stretch container). |

The 15 known top-level groups (GameObject key → display name) are listed in
`GroupCatalog.cs`'s `DisplayNames` map; see that file rather than duplicating the list here.

## Known limitations

- **ORBITAL.INFO (`OrbitalReadoutInstrument_Widget(Clone)`) cannot currently be selected.**
  Confirmed by decompiling `KSP.UI.OrbitalReadoutInstrumentManager`: unlike every other
  group (classic uGUI/`RectTransform`), this one is implemented with its own **UI
  Toolkit** window (`Window.Create(..., Parent = this.transform)`), and
  `UitkForKsp2.API.Window`'s internal `CreateInternal` builds that window's GameObject as a
  plain `Transform` + `UIDocument` with **no `RectTransform` anywhere in the subtree**.
  `GroupRegistry.Initialize`'s discovery loop requires a `RectTransform`
  (`TransformExtensions.ResolvePositionable`), so this group is silently skipped before it's
  even added to the discovered set. Fixing this needs a real design change — an abstraction
  over "positionable thing" that supports moving a `VisualElement` (via `style.left`/`top`)
  as an alternative to `RectTransform`/`Transform.position` — not a one-line patch. Deferred
  for a future version.
- **Cosmetic 1-2px layout jiggle on first hover** of any control in the editor window, once
  per control per window-open. Root-caused to Yoga (Unity's layout engine) rounding
  non-determinism under UitkForKsp2's fractional reference-resolution scale factor —
  see `.claude/HOVER_JIGGLE_INVESTIGATION.md` for the full investigation. Not pursued
  further; assessed as sub-pixel on real monitors and not fixable from application-level
  USS/C#.

## Legacy behavioral spec (historical reference only)

The legacy SpaceWarp 1.x / BepInEx mod's design was the starting behavioral reference for
the port above; it is **not** what's currently deployed. Kept here only in case a future
change needs to understand original intent or compare against pre-Redux behavior.

Core files in `CustomizableUIProject\`:

| File | Role |
|------|------|
| `CustomizableUIPlugin.cs` | `BaseSpaceWarpPlugin` entry point. Registered the flight app-bar button, subscribed to `GameStateEnteredMessage`, bound keybind config, drove `OnGUI()` + `Update()`. |
| `Manager.cs` | Singleton. Found the flight HUD root, built the 15 top-level groups, held `ScaleFactor`/`Resolution`, handled navball-follow and reset-all. |
| `Groups\BaseGroup.cs` | Base model wrapping a Unity `Transform`. Anchor math (left/center/right × top/center/bottom), nudge, move-to, `IsActive`→`SetActive`, reset. JSON opt-in fields. |
| `Groups\TopLevelGroups.cs` | `TopLevelGroup` + 15 concrete group subclasses, each hardcoding a game-object name/index and per-group pixel offsets. Static `SelectedIndex`. |
| `Groups\ChildGroup.cs` | Recursive child wrapper (only lightly used; child-editing UI was commented out). |
| `UI.cs` | The whole editor window in IMGUI (`OnGUI` + `GUILayout`): group selector, X/Y/Z fields + sliders, a D-pad of move/nudge/jump buttons, Follow-Navball/Show toggles, Reset/FUBAR, Save/Load, a yellow overlay drawn over the selected group. |
| `Styles.cs` | IMGUI `GUIStyle`/`GUISkin` setup (SpaceWarp `Skins.ConsoleSkin`) + loaded the close-button texture. |
| `Utility.cs` | `GameState` accessor, group-name display map, JSON `SaveData`/`LoadData` to `SavedData.json`. |

**Weaknesses the port fixed (do not reintroduce):**
- Global mutable singletons (`Manager`, `UI`, `Utility`) and static shared state
  (`TopLevelGroup.SelectedIndex`) → replaced with an owned `GroupRegistry`/`SceneController`.
- IMGUI (`OnGUI`) for the whole window → replaced with UitkForKsp2 (UXML/USS).
- Fragile initialization (message + `Update()` polling, try/catch swallowing) → the
  message+poll fallback pattern was kept (Redux's own timing is still not fully reliable)
  but no longer swallows errors silently.
- Hardcoded GameObject names/indices → dynamic discovery (`GroupRegistry`) with graceful
  skip + warning when a group is missing.
- Scale/resolution-coupled coordinate math (`Manager.CreateGroups` mutating offsets by
  `*= ScaleFactor` in place) → pure, resolution-independent live `RectTransform` math.
- Save location next to the DLL → the mod's own data folder, now versioned.
- Navball-follow drift from per-frame delta accumulation → cascaded from a single
  authoritative delta per change (`GroupRegistry.RecalculatePositionsOfGroupsAttachedToNavball`).

**Legacy → Redux/SpaceWarp2 mapping (for reference if porting something similar again):**

| Legacy (SpaceWarp 1.x / BepInEx) | Redux / SpaceWarp2 |
|----------------------------------|------------------------------|
| `BaseSpaceWarpPlugin` + `[BepInPlugin]`/`[BepInDependency]` | `KerbalMod` with `OnPreInitialized`/`OnInitialized`/`OnPostInitialized`. See `architectural_patterns_ksp2.md` §6. |
| `BepInEx.Logging.Logger.CreateLogSource(...)` | `ReduxLib.ReduxLib.GetLogger($"CustomizableUI|{GetType().Name}")` per class (see CLAUDE.md → Logging). |
| `Config.Bind(...)` (BepInEx) for keybinds | `SWConfiguration` (IConfigFile). |
| IMGUI `OnGUI` window + `GUIStyle`/`Skins.ConsoleSkin` | UitkForKsp2 window (UXML/USS), see `architectural_patterns_ksp2.md` §8. |
| `SpaceWarp.API.UI.Appbar.RegisterAppButton` + `UIValue_WriteBool_Toggle` | `SpaceWarp2.UI.API.Appbar.RegisterAppButton`; toggle sync unchanged (`KSP.UI.Binding.UIValue_WriteBool_Toggle`). |
| `AssetManager.GetAsset<Texture2D>(...)` (SW1) | Direct file read from `SWMetadata.Folder` for the icon; UXML loaded via Addressables. |
| GameLibs `Publicize="true"` for `_scaledMainCanvas` etc. | Not needed after all — `GameInstance.UI` exposes public `GetScaledMainCanvas()`/`GetScaledPopupCanvas()` accessors; no reflection required. |
| `swinfo.json` (spec 1.3, dep `com.github.x606.spacewarp`) | `swinfo.asset` for SpaceWarp2 (dep `SpaceWarp2 >= 2.0.0`). |

Confirm exact game-side type/field/GameObject names by decompiling the current assemblies
(`ilspycmd`, see `ksp2_assembly_csharp_reference.md`) before trusting anything above — the
game has moved several versions past the legacy `ksp2_version` `0.2.0` target this table
was written against.
