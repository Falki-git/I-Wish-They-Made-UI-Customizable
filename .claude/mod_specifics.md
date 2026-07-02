# Mod-specific guidance — Customizable UI

Per-mod companion to the mod-agnostic root `CLAUDE.md`. Covers the mod being developed in
this repo, its legacy origin, and how it maps onto the Redux/SpaceWarp2 template.

## What the mod is

**"Customizable UI"** (full title *"I Wish They Made UI Customizable"*) lets players
**move and show/hide flight-scene HUD elements** — navball, staging, throttle, resources,
time-warp, IVA portraits, app bar, etc. — and **save/load** those layouts. The player opens an
editor window from the flight app-bar button (or a keybind), picks a UI "group", then nudges /
jumps / snaps it around the screen or toggles its visibility.

- **Author:** Falki · **Legacy mod_id:** `com.github.falki.customizable-ui` · **Last legacy version:** `0.3.2`
- **Legacy source (read-only reference):** `E:\GitHub\KSP2\CustomizableUI Legacy`
  (Unity/BepInEx project; core code under `CustomizableUIProject\`).
- **Legacy upstream:** <https://github.com/Falki-git/I-Wish-They-Made-UI-Customizable>

**Status: porting the legacy SpaceWarp 1.x / BepInEx mod onto this Redux + SpaceWarp2
template.** The legacy code is admittedly rough (global singletons, IMGUI, brittle
game-object lookups) — treat it as a behavioral spec, not a blueprint to copy verbatim. The
template's per-mod folder for the port is `Assets/CustomizableUI/` (entry point scaffolded at
`Code/CustomizableUIPlugin.cs`).

## How the legacy mod works (behavioral spec)

Core files in `CustomizableUIProject\`:

| File | Role |
|------|------|
| `CustomizableUIPlugin.cs` | `BaseSpaceWarpPlugin` entry point. Registers the flight app-bar button, subscribes to `GameStateEnteredMessage`, binds keybind config, drives `OnGUI()` + `Update()`. |
| `Manager.cs` | Singleton. Finds the flight HUD root, builds the 15 top-level groups, holds `ScaleFactor`/`Resolution`, handles navball-follow and reset-all. |
| `Groups\BaseGroup.cs` | Base model wrapping a Unity `Transform`. Anchor math (left/center/right × top/center/bottom), nudge, move-to, `IsActive`→`SetActive`, reset. JSON opt-in fields. |
| `Groups\TopLevelGroups.cs` | `TopLevelGroup` + **15 concrete group subclasses**, each hardcoding a game-object name/index and per-group pixel offsets. Static `SelectedIndex`. |
| `Groups\ChildGroup.cs` | Recursive child wrapper (currently only lightly used; child-editing UI is commented out). |
| `UI.cs` | The whole editor window in **IMGUI** (`OnGUI` + `GUILayout`): group selector, X/Y/Z fields + sliders, a D-pad of move/nudge/jump buttons, Follow-Navball/Show toggles, Reset/FUBAR, Save/Load, a yellow overlay drawn over the selected group. |
| `Styles.cs` | IMGUI `GUIStyle`/`GUISkin` setup (SpaceWarp `Skins.ConsoleSkin`) + loads the close-button texture. |
| `Utility.cs` | `GameState` accessor, group-name display map, JSON `SaveData`/`LoadData` to `SavedData.json`. |

**The 15 flight-HUD groups it targets** (game-object name → display name), from
`Utility.InitializeGroupNames` / `TopLevelGroups.cs`:

`group_gameview(Clone)`→GAME.VIEW · `widget_indicator_verticalspeed_horizontal_new(Clone)`→VERTICAL.SPEED ·
`group_gobutton(Clone)`→GO.BUTTON · `OrbitalReadoutInstrument_Widget(Clone)`→ORBITAL.INFO ·
`group_burntimer(Clone)`→BURN.TIMER · `group_instruments(Clone)`→TIME.WARP ·
`group_atmospheric_indicator(Clone)`→ATMOSPHERIC.INDICATOR · `group_ivaportraits(Clone)`→IVA.PORTRAITS ·
`group_flightcontrol(Clone)`→SAS.CONTROL · `group_actionbar(Clone)`→VESSEL.ACTIONS ·
`NonStageableResources(Clone)`→VESSEL.RESOURCES · `group_navball(Clone)`→NAVBALL ·
`group_flightstaging(Clone)`→STAGING · `group_throttle(Clone)`→THROTTLE · `ButtonBar`→APP.BAR.

**Key game-internal dependencies** (all brittle — verify against the current game build):
- Flight HUD lives under `GameManager.Instance.Game.UI._scaledMainCanvas` → child
  `FlightHudRoot(Clone)`; the app bar under `Game.UI._scaledPopupCanvas` →
  `Container/ButtonBar`. `_scaledMainCanvas`/`_scaledPopupCanvas` are **private** fields.
- Groups are located by **exact GameObject name + child index** via a `GetChild(name)` helper —
  no stable IDs. Names may have changed since the legacy KSP2 `0.2.0` target.
- Positioning uses **world-space `Transform.position`** plus per-group magic offsets
  (`ToCenterOffset`, `OffsetToZero`, `ToMaxOffset`, `OverlayOffset`) scaled by canvas
  `scaleFactor` and `Screen.width/height`.
- App-bar button via SpaceWarp 1.x `Appbar.RegisterAppButton`; button toggle state synced
  through `KSP.UI.Binding.UIValue_WriteBool_Toggle`.
- Init is unreliable: it subscribes to `GameStateEnteredMessage` **and** polls in `Update()`
  because the app bar isn't always ready when the flight scene is entered.

## Known weaknesses to fix during the port (don't carry these over)

- **Global mutable singletons** (`Manager`, `UI`, `Utility`) and **static shared state**
  (`TopLevelGroup.SelectedIndex`) — replace with owned instances / proper lifetime.
- **IMGUI (`OnGUI`)** for the whole window — replace with **UitkForKsp2** (UXML/USS + MVVM),
  the template's UI path.
- **Fragile initialization** (message + `Update()` polling, try/catch swallowing) — use the
  SpaceWarp2 lifecycle + a reliable flight-scene-ready signal.
- **Hardcoded GameObject names/indices** — centralize and re-verify against the live hierarchy;
  fail gracefully when a group is missing.
- **Scale/resolution-coupled coordinate math** — `Manager.CreateGroups` mutates each group's
  offsets in place by `*= ScaleFactor`, so re-initialization double-applies. Rework as pure,
  resolution-independent layout.
- **Save location:** writes `SavedData.json` next to the assembly DLL, one global file. Move to
  `SWConfiguration`/a proper save path; consider per-save persistence.
- **Navball-follow** recomputed from a per-frame delta — prone to drift/jitter.

## Legacy → Redux/SpaceWarp2 mapping (for the port)

| Legacy (SpaceWarp 1.x / BepInEx) | Redux / SpaceWarp2 template |
|----------------------------------|------------------------------|
| `BaseSpaceWarpPlugin` + `[BepInPlugin]`/`[BepInDependency]` | `KerbalMod` (default; MonoBehaviour `Update` available) with `OnPreInitialized`/`OnInitialized`/`OnPostInitialized`. See `architectural_patterns_ksp2.md` §6. |
| `BepInEx.Logging.Logger.CreateLogSource(...)` | `ReduxLib.ReduxLib.GetLogger($"CustomizableUI|{GetType().Name}")` (see CLAUDE.md → Logging). |
| `Config.Bind(...)` (BepInEx) for keybinds | `SWConfiguration` (IConfigFile) / PatchManager config. |
| IMGUI `OnGUI` window + `GUIStyle`/`Skins.ConsoleSkin` | UitkForKsp2 window (UXML/USS + `ViewModelBase`). See `architectural_patterns_ksp2.md` §8. |
| `SpaceWarp.API.UI.Appbar.RegisterAppButton` + `UIValue_WriteBool_Toggle` | SpaceWarp2 app-bar API (verify current signature by decompiling `SpaceWarp2.*`). |
| `AssetManager.GetAsset<Texture2D>(...)` (SW1) | SpaceWarp2 asset API, registered in `OnPreInitialized`. |
| GameLibs `Publicize="true"` for `_scaledMainCanvas` etc. | **Reflection** to reach private UI fields — the publicizer is broken and must never be run (CLAUDE.md → Private game members). |
| `swinfo.json` (spec 1.3, dep `com.github.x606.spacewarp`) | `swinfo.asset` for SpaceWarp2 (dep `SpaceWarp2 >= 2.0.0`), refreshed `ksp2_version`. |

Confirm exact game-side type/field/GameObject names by decompiling the current assemblies
(`ilspycmd`, see `ksp2_assembly_csharp_reference.md`) rather than trusting the legacy names —
the game has moved several versions past the legacy `ksp2_version` `0.2.0` target.
