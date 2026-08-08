using System;
using System.Collections.Generic;
using System.Globalization;
using UitkForKsp2.API;
using UnityEngine;
using UnityEngine.UIElements;
using CustomizableUI.Groups;
using CustomizableUI.Utilities;
using ILogger = ReduxLib.Logging.ILogger;

namespace CustomizableUI.UI
{
    /// <summary>
    /// Wires the UXML controls to the GroupRegistry. Replaces the legacy IMGUI UI.FillWindow.
    /// </summary>
    public class MainGuiController : MonoBehaviour
    {
        private static readonly ILogger Logger = ReduxLib.ReduxLib.GetLogger($"CustomizableUI|{nameof(MainGuiController)}");

        // The PanelRenderer component of the window GameObject.
        // KSP2 0.2.9.0 / UitkForKsp2 26w32b moved windows off UIDocument onto Unity 6.5's
        // PanelRenderer: Window.Create returns a PanelRenderer and no UIDocument is ever added to
        // the window's GameObject, so GetComponent<UIDocument>() would silently return null here.
        private PanelRenderer _window;

        private VisualElement _root;

        // False until BuildWindow has resolved _root and re-run every element lookup and callback
        // registration against it. Gates Update(), which otherwise dereferences the cached elements
        // every frame -- against either null (root never resolved) or released elements (mid-rebuild).
        private bool _isWired;

        private Label _groupLabel;
        private Button _prevGroupButton;
        private Button _nextGroupButton;
        private Button _closeButton;

        private VisualElement _notReady;
        private VisualElement _body;

        private Toggle _followNavballToggle;
        private Toggle _scaleWithNavballToggle;
        private Toggle _showToggle;

        private TextField _xField;
        private TextField _yField;

        private Slider _xSlider;
        private Slider _ySlider;

        private Slider _scaleSlider;
        private Label _scaleValueLabel;

        private Button _jumpUpButton;
        private Button _jumpDownButton;
        private Button _jumpLeftButton;
        private Button _jumpRightButton;

        private RepeatButton _nudgeUpButton;
        private RepeatButton _nudgeDownButton;
        private RepeatButton _nudgeLeftButton;
        private RepeatButton _nudgeRightButton;

        private const long NudgeRepeatDelayMs = 300;
        private const long NudgeRepeatIntervalMs = 60;

        private Button _resetButton;
        private Button _fubarButton;
        private Button _saveButton;
        private Button _loadButton;

        private Label _messageLabel;
        private float _messageShownAt = float.NegativeInfinity;
        private const float MessageDurationSeconds = 3f;
        private bool _messageBeingShown;

        private const string PendingChangesClass = "pending-changes";
        private string _saveButtonBaseText;

        // Tracked separately from the button's own class/text so a panel rebuild -- which hands us a
        // fresh Save button carrying neither -- can put the indicator back rather than silently
        // telling the player their unsaved layout is saved.
        private bool _hasPendingChanges;

        private VisualElement _overlay;

        // One "undo" action per callback RegisterCallbacks attached, run before it attaches them
        // again. BuildWindow is re-entrant (see OnPanelUiReloaded) and the tree it re-wires is
        // usually the SAME one -- PanelRenderer only clones a fresh tree when the visual tree asset
        // itself changed, and the reload callback fires even for the very first build, one frame
        // after Window.Create already handed us a fully built tree. Without this, every
        // `clicked +=` / RegisterValueChangedCallback landed on the panel twice, so one click ran
        // its handler twice: "next group" skipped every second group (they looked like they didn't
        // exist), the jump buttons jumped two stops, and Save wrote the file twice.
        private readonly List<Action> _callbackTeardown = new();

        private GroupRegistry Registry => GroupRegistry.Instance;

        public void OnEnable()
        {
            _window = GetComponent<PanelRenderer>();

            // Re-wire whenever UI Toolkit rebuilds the panel's visual tree.
            // PanelRenderer.InitRootVisualElement clears the old tree with
            // VisualElementClearOptions.RecursiveReleaseResources and clones a fresh one, so every
            // element cached below (_root, _overlay, all the buttons/sliders/fields) is left pointing
            // at RELEASED elements. Those don't fail as clean NREs -- touching a released element's
            // style/computedStyle reads freed layout memory and throws from inside UnmanagedDataStore,
            // which for this controller would be once per frame out of Update(). (Orbital Survey hit
            // exactly this after the Unity 6.5 update.)
            // We never deactivate the window GameObject ourselves, but a rebuild isn't ours to rule
            // out, so reattach rather than assume. Unregistered in OnDisable so a re-enable doesn't
            // stack callbacks.
            if (_window != null)
            {
                _window.UnregisterUIReloadCallback(OnPanelUiReloaded);
                _window.RegisterUIReloadCallback(OnPanelUiReloaded);
            }

            BuildWindow();
        }

        // Fired by PanelRenderer once its root element is attached to a panel, so re-resolve and
        // re-run the whole wiring. Note this fires for the FIRST build too, not just later rebuilds:
        // Window.Create's InitRootVisualElement only flags the callback pending, and it's invoked a
        // frame later from PreUpdatePanelRenderers -- by which point OnEnable has already wired that
        // very same tree. So this must be safe to run against elements that are still carrying our
        // previous registrations, which is what _callbackTeardown is for.
        private void OnPanelUiReloaded(PanelRenderer renderer, VisualElement rootElement)
        {
            Logger.LogDebug("Panel UI reloaded -- re-resolving the window root and re-wiring the window.");
            BuildWindow();
        }

        private void BuildWindow()
        {
            _isWired = false;

            // Drop the previous wiring first -- on a same-tree re-run these are the exact elements
            // we're about to wire again, and on a real rebuild they're the released ones.
            TeardownCallbacks();

            // The panel root, not the window root: the yellow selection overlay is positioned
            // absolutely in UitkForKsp2's full 1920x1080 reference space (see UpdateOverlay), so it has
            // to hang off the element that covers the whole panel. Q<> lookups still reach every
            // control from here, since the window subtree lives under it.
            // GetPanelRoot() is UitkForKsp2's replacement for UIDocument.rootVisualElement --
            // PanelRenderer.rootVisualElement is internal.
            _root = _window != null ? _window.GetPanelRoot() : null;
            if (_root == null)
            {
                // Bail out loudly rather than NREing partway through the lookups below, each of which
                // dereferences _root -- a null here would otherwise produce a wall of unrelated errors.
                Logger.LogError(
                    _window == null
                        ? "No PanelRenderer on the window GameObject -- UitkForKsp2's Window.Create contract changed again; the window cannot be built."
                        : "PanelRenderer.GetPanelRoot() returned null -- the window UXML did not resolve; the window cannot be built.");
                return;
            }

            _groupLabel = _root.Q<Label>("group-label");
            _prevGroupButton = _root.Q<Button>("prev-group");
            _nextGroupButton = _root.Q<Button>("next-group");
            _closeButton = _root.Q<Button>("close-button");

            _notReady = _root.Q<VisualElement>("not-ready");
            _body = _root.Q<VisualElement>("body");

            _followNavballToggle = _root.Q<Toggle>("follow-navball-toggle");
            _scaleWithNavballToggle = _root.Q<Toggle>("scale-with-navball-toggle");
            _showToggle = _root.Q<Toggle>("show-toggle");

            _xField = _root.Q<TextField>("x-field");
            _yField = _root.Q<TextField>("y-field");
            _xField.isDelayed = true;
            _yField.isDelayed = true;

            _xSlider = _root.Q<Slider>("x-slider");
            _ySlider = _root.Q<Slider>("y-slider");

            _scaleSlider = _root.Q<Slider>("scale-slider");
            _scaleValueLabel = _root.Q<Label>("scale-value-label");

            _jumpUpButton = _root.Q<Button>("jump-up");
            _jumpDownButton = _root.Q<Button>("jump-down");
            _jumpLeftButton = _root.Q<Button>("jump-left");
            _jumpRightButton = _root.Q<Button>("jump-right");

            _nudgeUpButton = _root.Q<RepeatButton>("nudge-up");
            _nudgeDownButton = _root.Q<RepeatButton>("nudge-down");
            _nudgeLeftButton = _root.Q<RepeatButton>("nudge-left");
            _nudgeRightButton = _root.Q<RepeatButton>("nudge-right");

            _resetButton = _root.Q<Button>("reset-button");
            _fubarButton = _root.Q<Button>("fubar-button");
            _saveButton = _root.Q<Button>("save-button");
            // Captured once, not per build: on a same-tree re-run the button may already be carrying
            // the pending-changes suffix, and re-reading it there would bake "SAVE *" in as the base.
            // A real rebuild re-clones the button straight from the UXML, so the text is the same anyway.
            _saveButtonBaseText ??= _saveButton.text;
            _loadButton = _root.Q<Button>("load-button");

            _messageLabel = _root.Q<Label>("notification-label");

            BuildOverlay();
            RegisterCallbacks();

            // Only now is it safe for Update() to touch the cached elements.
            _isWired = true;

            // The freshly cloned Save button doesn't know about edits made before the rebuild.
            ApplyPendingChangesIndicator();

            // Same for the notification label: the new one has no "notification--show" class, so drop
            // the cached "it's currently shown" flag and let Update() re-apply it if the message
            // hasn't timed out yet.
            _messageBeingShown = false;

            RefreshForSelection();
        }

        private void BuildOverlay()
        {
            // The overlay isn't part of the UXML -- we add it ourselves, so a re-wire against the
            // same tree would otherwise leave the previous one parented and visible, frozen at
            // whatever bounds Update last gave it.
            _overlay?.RemoveFromHierarchy();

            _overlay = new VisualElement { pickingMode = PickingMode.Ignore };
            _overlay.AddToClassList("overlay");
            _overlay.style.position = Position.Absolute;
            _root.Add(_overlay);
        }

        /// <summary>
        /// Detaches every callback the last <see cref="RegisterCallbacks"/> attached. Guarded per
        /// action: a real panel rebuild hands the released elements back, and one that refuses to
        /// be unhooked shouldn't stop the rest from being cleaned up.
        /// </summary>
        private void TeardownCallbacks()
        {
            foreach (var undo in _callbackTeardown)
            {
                try
                {
                    undo();
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Failed to detach a callback from the previous window wiring.\n{ex}");
                }
            }

            _callbackTeardown.Clear();
        }

        /// <summary>Attaches a click handler and records how to detach it again.</summary>
        private void OnClick(Button button, Action handler)
        {
            button.clicked += handler;
            _callbackTeardown.Add(() => button.clicked -= handler);
        }

        /// <summary>Attaches a value-changed handler and records how to detach it again.</summary>
        private void OnValueChanged<T>(INotifyValueChanged<T> field, EventCallback<ChangeEvent<T>> handler)
        {
            field.RegisterValueChangedCallback(handler);
            _callbackTeardown.Add(() => field.UnregisterValueChangedCallback(handler));
        }

        /// <summary>
        /// Every registration here goes through OnClick/OnValueChanged so BuildWindow can undo it --
        /// see <see cref="_callbackTeardown"/>. The RepeatButtons are the exception: SetAction
        /// replaces its manipulator rather than stacking, so re-running it is already idempotent.
        /// </summary>
        private void RegisterCallbacks()
        {
            OnClick(_prevGroupButton, () => { BlurPositionFields(); Registry.SelectPrevious(); RefreshForSelection(); });
            OnClick(_nextGroupButton, () => { BlurPositionFields(); Registry.SelectNext(); RefreshForSelection(); });
            OnClick(_closeButton, () => SceneController.Instance.ToggleUI(false));

            OnValueChanged(_followNavballToggle, evt =>
            {
                if (Registry.SelectedGroup == null)
                    return;

                Registry.SelectedGroup.AttachToNavball = evt.newValue;
                MarkPendingChanges();
            });

            OnValueChanged(_scaleWithNavballToggle, evt =>
            {
                if (Registry.SelectedGroup == null)
                    return;

                Registry.SelectedGroup.ScaleWithNavball = evt.newValue;
                MarkPendingChanges();
            });

            OnValueChanged(_showToggle, evt =>
            {
                if (Registry.SelectedGroup == null)
                    return;

                Registry.SelectedGroup.IsActive = evt.newValue;
                MarkPendingChanges();
            });

            OnValueChanged(_xField, evt => MutateSelected(g => g.Position = WithX(g.Position, ParseOrKeep(evt.newValue, g.Position.x))));
            OnValueChanged(_yField, evt => MutateSelected(g => g.Position = WithY(g.Position, ParseOrKeep(evt.newValue, g.Position.y))));

            OnValueChanged(_xSlider, evt => MutateSelected(g => g.Position = WithX(g.Position, evt.newValue)));
            OnValueChanged(_ySlider, evt => MutateSelected(g => g.Position = WithY(g.Position, evt.newValue)));

            OnValueChanged(_scaleSlider, evt =>
            {
                var selected = Registry.SelectedGroup;
                if (selected == null)
                    return;

                var previousScale = selected.Scale;
                selected.Scale = evt.newValue;
                Registry.RecalculateForScaleChange(selected, previousScale);
                MarkPendingChanges();

                // Scaling changes the group's own on-screen width/height, which shifts where its
                // edges touch the screen -- refresh everything (not just the label) so the X/Y
                // sliders' ranges stay in sync and still let it travel all the way to the edge.
                RefreshForSelection();
            });

            OnClick(_jumpUpButton, () => MutateSelected(JumpUp));
            OnClick(_jumpDownButton, () => MutateSelected(JumpDown));
            OnClick(_jumpLeftButton, () => MutateSelected(JumpLeft));
            OnClick(_jumpRightButton, () => MutateSelected(JumpRight));

            _nudgeUpButton.SetAction(() => MutateSelected(g => g.NudgeUp()), NudgeRepeatDelayMs, NudgeRepeatIntervalMs);
            _nudgeDownButton.SetAction(() => MutateSelected(g => g.NudgeDown()), NudgeRepeatDelayMs, NudgeRepeatIntervalMs);
            _nudgeLeftButton.SetAction(() => MutateSelected(g => g.NudgeLeft()), NudgeRepeatDelayMs, NudgeRepeatIntervalMs);
            _nudgeRightButton.SetAction(() => MutateSelected(g => g.NudgeRight()), NudgeRepeatDelayMs, NudgeRepeatIntervalMs);

            OnClick(_resetButton, () =>
            {
                var selected = Registry.SelectedGroup;
                if (selected == null)
                    return;

                var previousScale = selected.Scale;
                MutateSelected(g => g.ResetToDefault());

                // Only cascade when the navball itself was reset -- ResetToDefault() already put
                // `selected` back at its own correct absolute Position, so rescaling its offset
                // from the navball afterward (what this does for a directly-scaled, non-navball
                // group) would incorrectly nudge it away from that just-restored default.
                if (selected.Key == GroupCatalog.NavballKey)
                    Registry.RecalculateForScaleChange(selected, previousScale);

                ShowMessage($"Group {selected.DisplayName} reset.");
                RefreshForSelection();
            });

            OnClick(_fubarButton, () =>
            {
                Registry.ResetAllToDefault();
                MarkPendingChanges();
                ShowMessage("Layout reset to initial state.");
                RefreshForSelection();
            });

            OnClick(_saveButton, () =>
            {
                SaveLoadUtility.SaveData();
                ClearPendingChanges();
                ShowMessage("Layout saved.");
            });

            OnClick(_loadButton, () =>
            {
                SaveLoadUtility.LoadData();
                // The just-loaded layout is now exactly what's on disk, so there's nothing left
                // to save until the next change.
                ClearPendingChanges();
                ShowMessage("Layout loaded.");
                RefreshForSelection();
            });
        }

        /// <summary>
        /// The X/Y fields are `isDelayed`, which commits their text on blur as well as Enter.
        /// RefreshPositionFields deliberately skips a focused field so it doesn't clobber
        /// active typing -- but that also means a field left focused while cycling through
        /// groups (e.g. auto-focused when the window first opens) never gets its displayed text
        /// refreshed, and whenever it eventually loses focus it commits that stale text into
        /// whichever group happens to be selected *then*, not the group it was showing. Blurring
        /// before switching selection flushes any pending edit into the group that was actually
        /// being edited, and leaves the fields unfocused so the new group's display refreshes normally.
        /// </summary>
        private void BlurPositionFields()
        {
            _xField.Blur();
            _yField.Blur();
        }

        private void MutateSelected(Action<GroupHandle> mutation)
        {
            var selected = Registry.SelectedGroup;
            if (selected == null)
                return;

            var previous = selected.Position;
            mutation(selected);
            Registry.RecalculatePositionsOfGroupsAttachedToNavball(selected, previous);
            MarkPendingChanges();
        }

        private void MarkPendingChanges()
        {
            _hasPendingChanges = true;
            ApplyPendingChangesIndicator();
        }

        private void ClearPendingChanges()
        {
            _hasPendingChanges = false;
            ApplyPendingChangesIndicator();
        }

        /// <summary>
        /// Pushes <see cref="_hasPendingChanges"/> onto the Save button. Split out from
        /// Mark/ClearPendingChanges so a post-rebuild re-wire can restore the indicator on the new
        /// button without pretending a fresh edit (or a fresh save) just happened.
        /// </summary>
        private void ApplyPendingChangesIndicator()
        {
            if (_saveButton == null)
                return;

            if (_hasPendingChanges)
            {
                _saveButton.AddToClassList(PendingChangesClass);
                _saveButton.text = $"{_saveButtonBaseText} *";
            }
            else
            {
                _saveButton.RemoveFromClassList(PendingChangesClass);
                _saveButton.text = _saveButtonBaseText;
            }
        }

        // ---- Smart jump (ported from the legacy IMGUI D-pad buttons) ----

        private static void JumpUp(GroupHandle g)
        {
            if (g.Position.y < g.VerticalLowerMiddle) g.MoveToVerticalLowerMiddle();
            else if (g.Position.y < g.VerticalCenter) g.MoveToVerticalCenter();
            else if (g.Position.y < g.VerticalUpperMiddle) g.MoveToVerticalUpperMiddle();
            else g.MoveToVerticalUpperTop();
        }

        private static void JumpDown(GroupHandle g)
        {
            if (g.Position.y > g.VerticalUpperMiddle) g.MoveToVerticalUpperMiddle();
            else if (g.Position.y > g.VerticalCenter) g.MoveToVerticalCenter();
            else if (g.Position.y > g.VerticalLowerMiddle) g.MoveToVerticalLowerMiddle();
            else g.MoveToVerticalLowerBottom();
        }

        private static void JumpLeft(GroupHandle g)
        {
            if (g.Position.x > g.HorizontalRightMiddle) g.MoveToHorizontalRightMiddle();
            else if (g.Position.x > g.HorizontalCenter) g.MoveToHorizontalCenter();
            else if (g.Position.x > g.HorizontalLeftMiddle) g.MoveToHorizontalLeftMiddle();
            else g.MoveToHorizontalLeftFar();
        }

        private static void JumpRight(GroupHandle g)
        {
            if (g.Position.x < g.HorizontalLeftMiddle) g.MoveToHorizontalLeftMiddle();
            else if (g.Position.x < g.HorizontalCenter) g.MoveToHorizontalCenter();
            else if (g.Position.x < g.HorizontalRightMiddle) g.MoveToHorizontalRightMiddle();
            else g.MoveToHorizontalRightFar();
        }

        private static Vector3 WithX(Vector3 v, float x) => new(x, v.y, v.z);
        private static Vector3 WithY(Vector3 v, float y) => new(v.x, y, v.z);

        private static float ParseOrKeep(string text, float fallback) =>
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;

        private void RefreshForSelection()
        {
            var ready = Registry.IsInitialized && Registry.Groups.Count > 0;
            _notReady.style.display = ready ? DisplayStyle.None : DisplayStyle.Flex;
            _body.style.display = ready ? DisplayStyle.Flex : DisplayStyle.None;

            if (!ready)
                return;

            var selected = Registry.SelectedGroup;
            if (selected == null)
                return;

            _groupLabel.text = selected.DisplayName;

            _followNavballToggle.SetValueWithoutNotify(selected.AttachToNavball);
            _followNavballToggle.SetEnabled(selected.Key != GroupCatalog.NavballKey);
            _scaleWithNavballToggle.SetValueWithoutNotify(selected.ScaleWithNavball);
            _scaleWithNavballToggle.SetEnabled(selected.Key != GroupCatalog.NavballKey);
            _showToggle.SetValueWithoutNotify(selected.IsActive);

            var pos = selected.Position;

            // The natural on-screen bounds for a group are exactly the pivot positions where its
            // own edges touch the screen edges -- same geometry the "jump to edge" buttons use.
            SetSliderRangeAndValue(_xSlider, selected.HorizontalLeftFar, selected.HorizontalRightFar, pos.x);
            SetSliderRangeAndValue(_ySlider, selected.VerticalLowerBottom, selected.VerticalUpperTop, pos.y);

            RefreshPositionFields(selected);

            // Scale's range is fixed (GroupHandle.MinScale/MaxScale), set once in UXML, so unlike
            // the position sliders this never needs the widen/narrow dance -- just the value.
            _scaleSlider.SetValueWithoutNotify(selected.Scale);
            RefreshScaleLabel(selected);
        }

        private void RefreshScaleLabel(GroupHandle selected) =>
            _scaleValueLabel.text = $"{Mathf.Round(selected.Scale * 100f):F0}%";

        /// <summary>
        /// Changes a Slider's range and value together without ever letting `.value` sit outside
        /// the slider's *current* [lowValue, highValue] at any intermediate step.
        ///
        /// Confirmed via a [PosWrite] log + stack trace: when a range change forces Unity's
        /// Slider to re-clamp its value, it queues an internal ChangeEvent rather than
        /// dispatching it synchronously -- that event fires on a *later* frame, by which point
        /// our registered handler is live again and treats it as a real user edit, applying it to
        /// whatever group happens to be selected by then. Detaching the callback during the
        /// range-change call (what the last two attempts did) can't catch a deferred dispatch.
        /// The only reliable fix is to never let the out-of-range condition happen at all.
        /// </summary>
        private static void SetSliderRangeAndValue(Slider slider, float low, float high, float value)
        {
            // Temporarily widen (never narrow yet) so `value` fits within the range at every step.
            slider.lowValue = Mathf.Min(slider.lowValue, low, value);
            slider.highValue = Mathf.Max(slider.highValue, high, value);

            slider.SetValueWithoutNotify(value);

            // Now narrow to the real bounds -- `value` already sits within [low, high], so this
            // can't force a clamp either.
            slider.lowValue = low;
            slider.highValue = high;
        }

        private void RefreshPositionFields(GroupHandle selected)
        {
            var pos = selected.Position;

            if (!IsFocused(_xField))
                _xField.SetValueWithoutNotify(FormatPixel(pos.x));
            if (!IsFocused(_yField))
                _yField.SetValueWithoutNotify(FormatPixel(pos.y));
        }

        private static string FormatPixel(float value) => Mathf.Round(value).ToString(CultureInfo.InvariantCulture);

        private static bool IsFocused(VisualElement element) =>
            element.panel?.focusController?.focusedElement == element;

        private void ShowMessage(string text)
        {
            _messageLabel.text = text;
            _messageShownAt = Time.time;
        }

        public void Update()
        {
            // Everything past here dereferences the cached UXML elements, so it must not run against
            // a window that failed to build or is between a panel rebuild and its re-wire.
            if (!_isWired)
                return;

            if (Registry.IsInitialized && Registry.Groups.Count > 0 && Registry.SelectedGroup != null)
            {
                var selected = Registry.SelectedGroup;
                var pos = selected.Position;

                RefreshPositionFields(selected);
                // The range doesn't change here (only RefreshForSelection changes it), and the
                // live position always stays within its own slider bounds during normal use, so
                // a plain SetValueWithoutNotify is safe -- no widen/narrow dance needed.
                _xSlider.SetValueWithoutNotify(pos.x);
                _ySlider.SetValueWithoutNotify(pos.y);

                UpdateOverlay(selected);
            }
            else
            {
                _overlay.style.display = DisplayStyle.None;
            }

            var showMessage = Time.time - _messageShownAt < MessageDurationSeconds;
            
            // show or hide the notification panel
            if (showMessage != _messageBeingShown)
            {
                _messageBeingShown = showMessage;

                if (_messageBeingShown)
                {
                    _messageLabel.AddToClassList("notification--show");
                }
                else
                {
                    _messageLabel.RemoveFromClassList("notification--show");
                }
            }
        }

        private void UpdateOverlay(GroupHandle selected)
        {
            if (selected.RectTransform == null)
            {
                _overlay.style.display = DisplayStyle.None;
                return;
            }

            // The overlay is drawn at the widget's actual live screen bounds. World-space Y grows
            // upward; UI Toolkit's absolute-position Y grows downward from the top, so it's
            // flipped here.
            //
            // This window was created with UseStockScale (WindowOptions.Default), which renders
            // in UitkForKsp2's fixed 1920x1080 reference resolution -- Unity then scales that to
            // fit the real screen. Positions/sizes computed from the flight HUD's own Canvas are
            // in raw screen pixels, so they have to be converted into that reference space before
            // being assigned to a UI Toolkit style property, or they get scaled a second time and
            // drift out of sync with the actual widget on any non-1920x1080 display.
            var topLeftPx = new Vector2(selected.LeftEdge, Screen.height - selected.TopEdge);
            var topLeftRef = ReferenceResolution.ConvertFromScreenPixels(topLeftPx);
            var sizeRef = ReferenceResolution.ConvertFromScreenPixels(new Vector2(selected.WidthPx, selected.HeightPx));

            _overlay.style.display = DisplayStyle.Flex;
            _overlay.style.left = topLeftRef.x;
            _overlay.style.top = topLeftRef.y;
            _overlay.style.width = sizeRef.x;
            _overlay.style.height = sizeRef.y;
        }

        public void OnDisable()
        {
            _overlay?.RemoveFromHierarchy();

            // The window's elements can outlive this component being disabled, so leave nothing of
            // ours attached to them -- OnEnable wires them again from scratch.
            TeardownCallbacks();

            // The PanelRenderer outlives this component, so a reload callback left registered would
            // re-run the wiring against a disabled controller (and stack a second registration on the
            // next enable).
            if (_window != null)
                _window.UnregisterUIReloadCallback(OnPanelUiReloaded);

            // The cached elements belong to a tree we're no longer tracking; force a rebuild on
            // re-enable, and keep Update() off them until then.
            _isWired = false;
        }
    }
}
