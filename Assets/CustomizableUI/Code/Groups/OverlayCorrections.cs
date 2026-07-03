using System.Collections.Generic;
using UnityEngine;

namespace CustomizableUI.Groups
{
    /// <summary>
    /// Manual pixel corrections for the overlay highlight box, for the rare group where
    /// Positionable's own rect doesn't match where its content actually renders. Confirmed for
    /// two navball-docked indicators: dragging Positionable does move the real widget correctly
    /// (so movement/slider-range math is untouched and correct), but Positionable's own bounds
    /// sit at dead-center screen while the content actually renders next to the navball.
    /// Throttle is a different case -- it resolves to its own real, correctly-positioned rect,
    /// but that rect's own bounds apparently don't cover the full visually-rendered bar (the
    /// legacy mod needed a similar per-group correction for throttle specifically).
    ///
    /// These values were measured directly from screenshots -- comparing the overlay's drawn
    /// position against where the group's content actually renders, at the user's screen
    /// resolution -- not derived from any layout data. They're an approximation and may need
    /// further manual tuning; this is a deliberately narrow, explicit exception, not a general
    /// heuristic (see GroupHandle/TransformExtensions for the actual geometry system).
    /// </summary>
    public static class OverlayCorrections
    {
        private static readonly Dictionary<string, Vector2> Offsets = new()
        {
            ["widget_indicator_verticalspeed_horizontal_new(Clone)"] = new Vector2(-722, -311),
            ["group_atmospheric_indicator(Clone)"] = new Vector2(-723, -504),
            ["group_throttle(Clone)"] = new Vector2(0, 120),
        };

        public static Vector2 Get(string key) => Offsets.TryGetValue(key, out var offset) ? offset : Vector2.zero;
    }
}
