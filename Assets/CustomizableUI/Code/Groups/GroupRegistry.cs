using System.Collections.Generic;
using System.Linq;
using KSP.Game;
using UnityEngine;
using CustomizableUI.Utilities;
using ILogger = ReduxLib.Logging.ILogger;

namespace CustomizableUI.Groups
{
    /// <summary>
    /// Owns the discovered set of top-level flight-HUD groups. Replaces the legacy static
    /// Manager singleton -- groups are discovered dynamically from the live UI hierarchy
    /// (KSP.UI.Flight.UIFlightHud's own child instruments, plus the app-bar ButtonBar) instead
    /// of a hardcoded GameObject-name/child-index table, since Redux's flight HUD no longer
    /// guarantees the same layout the legacy mod targeted.
    /// </summary>
    public class GroupRegistry
    {
        private static readonly ILogger Logger = ReduxLib.ReduxLib.GetLogger($"CustomizableUI|{nameof(GroupRegistry)}");

        public static GroupRegistry Instance { get; } = new();

        public List<GroupHandle> Groups { get; private set; } = new();
        public bool IsInitialized { get; private set; }

        private int _selectedIndex;
        public int SelectedIndex
        {
            get => _selectedIndex;
            set => _selectedIndex = Groups.Count == 0 ? 0 : Mathf.Clamp(value, 0, Groups.Count - 1);
        }

        public GroupHandle SelectedGroup => Groups.Count == 0 ? null : Groups[SelectedIndex];

        private GroupRegistry() { }

        public void SelectPrevious() => SelectedIndex--;
        public void SelectNext() => SelectedIndex++;

        /// <summary>Call when leaving the flight scene -- the discovered Transforms are about to be destroyed.</summary>
        public void Reset()
        {
            Groups = new List<GroupHandle>();
            _selectedIndex = 0;
            IsInitialized = false;
        }

        public void InitializeIfNeeded()
        {
            if (IsInitialized)
                return;

            var gameState = GameManager.Instance?.Game?.GlobalGameState?.GetGameState()?.GameState ?? GameState.Invalid;
            if (gameState != GameState.FlightView)
                return;

            Initialize();
        }

        public void Initialize()
        {
            var ui = GameManager.Instance?.Game?.UI;
            if (ui == null)
            {
                Logger.LogWarning("Cannot initialize: GameInstance.UI isn't available yet.");
                return;
            }

            var mainCanvas = ui.GetScaledMainCanvas();
            if (mainCanvas == null)
            {
                Logger.LogWarning("Cannot initialize: scaled main canvas isn't available yet.");
                return;
            }

            var flightHud = ui.FlightHud;
            if (flightHud == null)
            {
                Logger.LogWarning("Cannot initialize: UIFlightHud isn't available yet.");
                return;
            }

            var discovered = new Dictionary<string, Transform>();
            var hudTransform = flightHud.transform;
            for (var i = 0; i < hudTransform.childCount; i++)
            {
                var child = hudTransform.GetChild(i);
                if (child.ResolvePositionable().GetComponent<RectTransform>() == null)
                    continue;

                discovered[child.name] = child;
            }

            var popupCanvas = ui.GetScaledPopupCanvas();
            var buttonBar = popupCanvas != null ? popupCanvas.transform.FindDescendant("ButtonBar") : null;
            if (buttonBar != null)
                discovered[buttonBar.name] = buttonBar;
            else
                Logger.LogWarning("Could not find the app-bar (ButtonBar) under the scaled popup canvas; it won't be editable.");

            var ordered = new List<GroupHandle>();

            // Known groups first, in the legacy mod's order, so the editor UI feels familiar.
            foreach (var key in GroupCatalog.KnownKeys)
            {
                if (discovered.TryGetValue(key, out var t))
                {
                    AddIfPositionable(ordered, new GroupHandle(key, t));
                    discovered.Remove(key);
                }
            }

            // Anything left over is a group the legacy catalog doesn't know about -- surface it
            // anyway rather than silently dropping it, and log it so the catalog's display-name
            // list can be extended later.
            foreach (var kvp in discovered.OrderBy(kvp => kvp.Key))
            {
                Logger.LogInfo($"Discovered flight-HUD group not in the known catalog: \"{kvp.Key}\".");
                AddIfPositionable(ordered, new GroupHandle(kvp.Key, kvp.Value));
            }

            Groups = ordered;
            _selectedIndex = 0;
            IsInitialized = true;

            foreach (var key in GroupCatalog.DefaultAttachToNavballKeySet)
            {
                if (Groups.All(g => g.Key != key))
                    Logger.LogWarning(
                        $"Expected group \"{key}\" (default-attaches to the navball) was not discovered -- " +
                        "if Redux renamed it, GroupCatalog.DefaultAttachToNavballKeys needs updating.");
            }

            Logger.LogInfo(
                $"Initialization successful. Top level UI groups created: {Groups.Count}. " +
                $"Canvas: renderMode={mainCanvas.renderMode} scaleFactor={mainCanvas.scaleFactor} " +
                $"renderingDisplaySize={mainCanvas.renderingDisplaySize} Screen={Screen.width}x{Screen.height}");

            foreach (var group in Groups)
                LogGroupDiagnostics(group);
        }

        /// <summary>
        /// A handful of groups have no meaningfully-sized widget anywhere in their subtree (e.g.
        /// world-space label overlays, full-screen interact-prompt canvases) -- their resolved
        /// RectTransform still covers the whole screen after ResolvePositionable's search. Those
        /// aren't something a player can usefully "move", so skip adding them to the editable
        /// list instead of showing a group whose slider range is a single degenerate point.
        /// </summary>
        private void AddIfPositionable(List<GroupHandle> list, GroupHandle group)
        {
            const float coverageThreshold = 0.95f;
            var coversScreen = group.WidthPx >= Screen.width * coverageThreshold &&
                                group.HeightPx >= Screen.height * coverageThreshold;

            if (coversScreen)
            {
                Logger.LogInfo(
                    $"Skipping \"{group.Key}\" -- no positionable widget found in its subtree " +
                    $"(resolved rect is {group.WidthPx:F0}x{group.HeightPx:F0}px, covers the whole screen).");
                return;
            }

            list.Add(group);
        }

        /// <summary>
        /// Temporary troubleshooting aid -- dumps the RectTransform data behind each discovered
        /// group so mismatches between the legacy pixel-space assumptions and Redux's actual HUD
        /// layout (anchors/pivot/rect size) can be diagnosed from a log instead of guesswork.
        /// </summary>
        private static void LogGroupDiagnostics(GroupHandle group)
        {
            var rt = group.RectTransform;
            if (rt == null)
            {
                Logger.LogInfo($"[Diag] {group.Key} -> Positionable=\"{group.Positionable.name}\" has NO RectTransform.");
                return;
            }

            Logger.LogInfo(
                $"[Diag] {group.Key} -> Positionable=\"{rt.name}\" " +
                $"anchorMin={rt.anchorMin} anchorMax={rt.anchorMax} pivot={rt.pivot} " +
                $"anchoredPosition={rt.anchoredPosition} sizeDelta={rt.sizeDelta} rect={rt.rect} " +
                $"worldPosition={rt.position} lossyScale={rt.lossyScale}");
        }

        public void RecalculatePositionsOfGroupsAttachedToNavball(GroupHandle movedGroup, Vector3 previousPosition)
        {
            if (movedGroup.Key != GroupCatalog.NavballKey)
                return;

            var deltaDistance = movedGroup.Position - previousPosition;
            if (deltaDistance == Vector3.zero)
                return;

            foreach (var group in Groups)
            {
                if (group != movedGroup && group.AttachToNavball)
                    group.Position += deltaDistance;
            }
        }

        /// <summary>
        /// Mirrors RecalculatePositionsOfGroupsAttachedToNavball, but for size instead of
        /// position. Scaling a RectTransform resizes it around its own pivot, not around the
        /// navball -- so a ScaleWithNavball group left at its own Position after a scale change
        /// would visually drift toward/away from the navball instead of shrinking/growing in
        /// place next to it. Rescaling its offset from the navball's center by the same ratio its
        /// own size changed by keeps it visually anchored.
        ///
        /// Two cases: scaling the navball itself scales every other ScaleWithNavball group (and
        /// its distance from the navball) by the navball's own ratio; scaling a single
        /// ScaleWithNavball group directly (leaving the navball untouched) only rescales that
        /// group's own distance from the navball, using its own actual (post-clamp) scale ratio.
        /// </summary>
        public void RecalculateForScaleChange(GroupHandle changedGroup, float previousScale)
        {
            if (Mathf.Approximately(changedGroup.Scale, previousScale))
                return;

            var navball = Groups.Find(g => g.Key == GroupCatalog.NavballKey);
            if (navball == null)
                return;

            if (changedGroup == navball)
            {
                var navballRatio = changedGroup.Scale / previousScale;
                foreach (var group in Groups)
                {
                    if (group == navball || !group.ScaleWithNavball)
                        continue;

                    var groupPreviousScale = group.Scale;
                    group.Scale *= navballRatio;
                    RescaleOffsetFromNavball(group, navball, group.Scale / groupPreviousScale);
                }
                return;
            }

            if (changedGroup.ScaleWithNavball)
            {
                var ratio = changedGroup.Scale / previousScale;
                RescaleOffsetFromNavball(changedGroup, navball, ratio);
            }
        }

        private static void RescaleOffsetFromNavball(GroupHandle group, GroupHandle navball, float ratio)
        {
            var offsetFromNavball = group.Position - navball.Position;
            group.Position = navball.Position + offsetFromNavball * ratio;
        }

        public void ResetAllToDefault()
        {
            foreach (var group in Groups)
                group.ResetToDefault();
        }

        /// <summary>See GroupHandle.EnforceVisibility -- call every frame while initialized.</summary>
        public void EnforceVisibility()
        {
            foreach (var group in Groups)
                group.EnforceVisibility();
        }
    }
}
