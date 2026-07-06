# Hover-triggered layout jiggle — investigation notes

## Symptom

The first time the player hovers a button, slider, or toggle after opening the
`CustomizableUI` window, some unrelated sibling controls shift by 1-2px (or, for the
slider track, visibly change thickness). Distinct control groups (action buttons, D-pad,
sliders, toggles) each show their own one-time shift the first time *any* control in that
group is hovered; subsequent hovers of already-affected elements cause no further change.
Closing and reopening the window resets the effect (it reproduces fresh every time the
window is rebuilt).

## Theories tried and ruled out

1. **`-unity-font-definition` (async custom font loading).** Stripped all custom font
   overrides from the stylesheet. Shift persisted.
2. **`.row { justify-content: center }` (centering rounding).** Changed to `flex-start`.
   Shift persisted.
   - *(Both of the above were later found to have been edited in a file that wasn't even
     the one the UI was loading at the time — see "Detour" below. They were retested after
     fixing that, and still didn't change the outcome.)*
3. **UI Toolkit "shared style" / `ComputedStyle` pooling.** Hypothesis: buttons with
   identical class lists (e.g. all four `.action-button`s) share a pooled computed style
   object; the first pseudo-class differentiation forces a pool split that could leave
   siblings with stale layout values. Tested by giving each action button a unique,
   imperceptible inline `opacity` override (1.0, 0.9999, 0.9998, 0.9997) so none of them
   could be pooled together. **No effect** — ruled out.
4. **CSS `transition-property` in the shared KerbalUI theme.** Checked
   `uitkforksp2.controls@.../Theme/KerbalUI.uss` for animated properties. Found
   `transition-property: left, background-color` on the `.switch`-style toggle checkmark
   (not used by this mod's `.led-toggle`s) and `transition-property: width` on
   `ProgressBar` (not used at all). The sliders/buttons this mod uses only transition
   `background-color`/`border-color` — non-layout properties. Not the cause.
5. **Synthetic pointer-event "pre-warm."** Hypothesis: if the jiggle is caused by each
   control's first real `:hover` pseudo-class match, forcing that transition
   programmatically (via `PointerEnterEvent`/`PointerLeaveEvent` sent through
   `VisualElement.SendEvent`) immediately after the window builds should make it happen
   invisibly before the player ever hovers for real. Implemented in
   `MainGuiController.OnEnable` for every button, slider (and slider drag handles), and
   toggle. **No effect** — ruled out. (Likely because `:hover` pseudo-state bookkeeping is
   driven by the panel's own pointer-position tracking, not by dispatching a synthetic
   event to a single target — so the synthetic events never actually touched the internal
   state that real hover does.)

## Root cause (confirmed via instrumentation)

Added temporary diagnostic logging to `MainGuiController.cs`
(`RegisterGeometryDiagnostics`) that logs every `GeometryChangedEvent` (old rect → new
rect) and every `PointerEnterEvent` under the window, tagged `HoverJiggleDiag` in
`Player.log`, with a shared elapsed-time clock to correlate the two.

Reproduction: opened the UI, hovered only the FUBAR button once, closed the game. Log
excerpt (elapsed ms in brackets):

```
[2247ms] POINTER-ENTER Button#fubar-button...
[2251ms] GEOMETRY x-slider-row       height 33.75 -> 34.50
[2251ms] GEOMETRY reset-button       height 26.25 -> 25.50
[2251ms] GEOMETRY fubar-button       y 32.25 -> 31.50, height 25.50 -> 26.25
[2251ms] GEOMETRY controls-row       y 43.50 -> 44.25
[2252ms] GEOMETRY save-load-row      y 201.00 -> 201.75
[2252ms] GEOMETRY main-column        height 237.00 -> 237.75
[2252ms] GEOMETRY y-slider-column    height 237.00 -> 237.75
```

`reset-button` and `fubar-button` are both styled by the identical rule
`.action-button.unity-button { height: 26px; ... }` — same class, same declared height.
Yet on the very first layout pass they resolved to two *different* actual sizes (26.25 vs
25.50), and after the hover-triggered relayout, those two values **swapped**. The same
pattern shows on `x-slider-row`, which has an explicit `height: 34px` in the stylesheet
but resolved to 33.75 on the first pass and 34.50 on the second.

This is the signature of **floating-point rounding in Unity's layout engine (Yoga), not a
CSS or C# bug**. This window renders through UitkForKsp2's fixed 1920x1080 reference
resolution, scaled to the player's actual screen (see the existing comment in
`MainGuiController.UpdateOverlay`) — so every declared pixel value (`26px`, `34px`, ...)
gets multiplied by a scale factor that is very likely not a clean integer ratio on most
monitors. A value that lands close to a rounding boundary (e.g. `26 * scale`) can round
one way on the first layout pass and a different way on any *subsequent* relayout, even
though nothing about its authored style changed. `:hover` reliably triggers a relayout
(even a rule that only changes `border-color` still dirties the style system), which is
why hovering *anything* is enough to trigger it once, and why it only ever happens once
per control per window instance (once relayout has "landed" on a value, later relayouts
reproduce the same rounding deterministically).

## Why the three fix attempts didn't work

None of them addressed the actual mechanism:
- Style-sharing/pooling and CSS transitions were both real, testable hypotheses, but
  neither is what's happening — the cause is rounding non-determinism in the layout
  engine itself, not selector/style resolution.
- The synthetic pointer-event pre-warm assumed the trigger was `:hover` pseudo-class
  *matching* specifically, and tried to force that early — but the actual trigger is any
  relayout at all, and forcing a relayout on the very first pass just makes the "first"
  and "second" pass rounding land the same way at a different time, not eliminate the
  discrepancy between passes.

## Assessment

The magnitude here (0.75 units in the 1920x1080 reference space, before the final
scale-down to the player's actual screen) is very likely sub-single-pixel on most real
monitors — it was only visible because of a pixel-level before/after screenshot
comparison. This looks like an inherent trait of Unity's Yoga-based layout engine
combined with a non-integer UI scale factor, not something fixable from application-level
USS or mod C# code.

### Options going forward

1. **Leave it.** Cosmetically negligible in normal play.
2. **Investigate UitkForKsp2 for a non-scaled / pixel-perfect window mode** as an
   alternative to `WindowOptions.Default` (`UseStockScale`), which would remove the
   fractional scale factor at its source. Not yet explored.
3. Avoid stacking identically-sized flex siblings in auto-height containers as a
   mitigation for specific instances (fragile, whack-a-mole, not a general fix).