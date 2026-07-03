using System.Collections.Generic;

namespace CustomizableUI.Groups
{
    /// <summary>
    /// Friendly display names for known flight-HUD groups, keyed by the GameObject's runtime
    /// name. Purely cosmetic labeling -- all positioning math is derived live from each group's
    /// own RectTransform (see GroupHandle), not from anything hardcoded here. A group with no
    /// entry still shows up in the editor with a prettified version of its raw name instead of
    /// being dropped.
    /// </summary>
    public static class GroupCatalog
    {
        /// <summary>The navball's own key -- it's the one group that can't "follow the navball".</summary>
        public const string NavballKey = "group_navball(Clone)";

        private static readonly Dictionary<string, string> DisplayNames = new()
        {
            ["group_gameview(Clone)"] = "GAME.VIEW",
            ["widget_indicator_verticalspeed_horizontal_new(Clone)"] = "VERTICAL.SPEED",
            ["group_gobutton(Clone)"] = "GO.BUTTON",
            ["OrbitalReadoutInstrument_Widget(Clone)"] = "ORBITAL.INFO",
            ["group_burntimer(Clone)"] = "BURN.TIMER",
            ["group_instruments(Clone)"] = "TIME.WARP",
            ["group_atmospheric_indicator(Clone)"] = "ATMOSPHERIC.INDICATOR",
            ["group_ivaportraits(Clone)"] = "IVA.PORTRAITS",
            ["group_flightcontrol(Clone)"] = "SAS.CONTROL",
            ["group_actionbar(Clone)"] = "VESSEL.ACTIONS",
            ["NonStageableResources(Clone)"] = "VESSEL.RESOURCES",
            [NavballKey] = "NAVBALL",
            ["group_flightstaging(Clone)"] = "STAGING",
            ["group_throttle(Clone)"] = "THROTTLE",
            ["ButtonBar"] = "APP.BAR",
        };

        /// <summary>Keys the registry should look for first, in this order, so the editor's group list feels familiar.</summary>
        public static IReadOnlyCollection<string> KnownKeys => DisplayNames.Keys;

        public static string GetDisplayName(string key) =>
            DisplayNames.TryGetValue(key, out var name) ? name : PrettifyUnknownKey(key);

        private static string PrettifyUnknownKey(string key)
        {
            var name = key.EndsWith("(Clone)") ? key.Substring(0, key.Length - "(Clone)".Length) : key;
            return name.Replace("_", " ").Replace("group ", "").Trim().ToUpperInvariant();
        }
    }
}
