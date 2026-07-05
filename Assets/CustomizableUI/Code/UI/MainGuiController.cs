using System;
using System.Globalization;
using UitkForKsp2.API;
using UnityEngine;
using UnityEngine.UIElements;
using CustomizableUI.Groups;
using CustomizableUI.Utilities;

namespace CustomizableUI.UI
{
    /// <summary>
    /// Wires the UXML controls to the GroupRegistry. Replaces the legacy IMGUI UI.FillWindow.
    /// </summary>
    public class MainGuiController : MonoBehaviour
    {
        private UIDocument _document;
        private VisualElement _root;

        private Label _groupLabel;
        private Button _prevGroupButton;
        private Button _nextGroupButton;
        private Button _closeButton;

        private VisualElement _notReady;
        private VisualElement _body;

        private Toggle _followNavballToggle;
        private Toggle _showToggle;

        private TextField _xField;
        private TextField _yField;

        private Slider _xSlider;
        private Slider _ySlider;

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
        private const float MessageDurationSeconds = 2f;
        private bool _messageBeingShown;

        private VisualElement _overlay;

        private GroupRegistry Registry => GroupRegistry.Instance;

        public void OnEnable()
        {
            _document = GetComponent<UIDocument>();
            _root = _document.rootVisualElement;

            _groupLabel = _root.Q<Label>("group-label");
            _prevGroupButton = _root.Q<Button>("prev-group");
            _nextGroupButton = _root.Q<Button>("next-group");
            _closeButton = _root.Q<Button>("close-button");

            _notReady = _root.Q<VisualElement>("not-ready");
            _body = _root.Q<VisualElement>("body");

            _followNavballToggle = _root.Q<Toggle>("follow-navball-toggle");
            _showToggle = _root.Q<Toggle>("show-toggle");

            _xField = _root.Q<TextField>("x-field");
            _yField = _root.Q<TextField>("y-field");
            _xField.isDelayed = true;
            _yField.isDelayed = true;

            _xSlider = _root.Q<Slider>("x-slider");
            _ySlider = _root.Q<Slider>("y-slider");

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
            _loadButton = _root.Q<Button>("load-button");

            _messageLabel = _root.Q<Label>("message-label");

            BuildOverlay();
            RegisterCallbacks();

            RefreshForSelection();
        }

        private void BuildOverlay()
        {
            _overlay = new VisualElement { pickingMode = PickingMode.Ignore };
            _overlay.AddToClassList("overlay");
            _overlay.style.position = Position.Absolute;
            _root.Add(_overlay);
        }

        private void RegisterCallbacks()
        {
            _prevGroupButton.clicked += () => { BlurPositionFields(); Registry.SelectPrevious(); RefreshForSelection(); };
            _nextGroupButton.clicked += () => { BlurPositionFields(); Registry.SelectNext(); RefreshForSelection(); };
            _closeButton.clicked += () => SceneController.Instance.ToggleUI(false);

            _followNavballToggle.RegisterValueChangedCallback(evt =>
            {
                if (Registry.SelectedGroup != null)
                    Registry.SelectedGroup.AttachToNavball = evt.newValue;
            });

            _showToggle.RegisterValueChangedCallback(evt =>
            {
                if (Registry.SelectedGroup != null)
                    Registry.SelectedGroup.IsActive = evt.newValue;
            });

            _xField.RegisterValueChangedCallback(evt => MutateSelected(g => g.Position = WithX(g.Position, ParseOrKeep(evt.newValue, g.Position.x))));
            _yField.RegisterValueChangedCallback(evt => MutateSelected(g => g.Position = WithY(g.Position, ParseOrKeep(evt.newValue, g.Position.y))));

            _xSlider.RegisterValueChangedCallback(evt => MutateSelected(g => g.Position = WithX(g.Position, evt.newValue)));
            _ySlider.RegisterValueChangedCallback(evt => MutateSelected(g => g.Position = WithY(g.Position, evt.newValue)));

            _jumpUpButton.clicked += () => MutateSelected(JumpUp);
            _jumpDownButton.clicked += () => MutateSelected(JumpDown);
            _jumpLeftButton.clicked += () => MutateSelected(JumpLeft);
            _jumpRightButton.clicked += () => MutateSelected(JumpRight);

            _nudgeUpButton.SetAction(() => MutateSelected(g => g.NudgeUp()), NudgeRepeatDelayMs, NudgeRepeatIntervalMs);
            _nudgeDownButton.SetAction(() => MutateSelected(g => g.NudgeDown()), NudgeRepeatDelayMs, NudgeRepeatIntervalMs);
            _nudgeLeftButton.SetAction(() => MutateSelected(g => g.NudgeLeft()), NudgeRepeatDelayMs, NudgeRepeatIntervalMs);
            _nudgeRightButton.SetAction(() => MutateSelected(g => g.NudgeRight()), NudgeRepeatDelayMs, NudgeRepeatIntervalMs);

            _resetButton.clicked += () =>
            {
                var selected = Registry.SelectedGroup;
                if (selected == null)
                    return;

                selected.ResetToDefault();
                ShowMessage($"Group {selected.DisplayName} reset.");
                RefreshForSelection();
            };

            _fubarButton.clicked += () =>
            {
                Registry.ResetAllToDefault();
                ShowMessage("Layout reset to initial state.");
                RefreshForSelection();
            };

            _saveButton.clicked += () =>
            {
                SaveLoadUtility.SaveData();
                ShowMessage("Layout saved.");
            };

            _loadButton.clicked += () =>
            {
                SaveLoadUtility.LoadData();
                ShowMessage("Layout loaded.");
                RefreshForSelection();
            };
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
            _showToggle.SetValueWithoutNotify(selected.IsActive);

            var pos = selected.Position;

            // The natural on-screen bounds for a group are exactly the pivot positions where its
            // own edges touch the screen edges -- same geometry the "jump to edge" buttons use.
            SetSliderRangeAndValue(_xSlider, selected.HorizontalLeftFar, selected.HorizontalRightFar, pos.x);
            SetSliderRangeAndValue(_ySlider, selected.VerticalLowerBottom, selected.VerticalUpperTop, pos.y);

            RefreshPositionFields(selected);
        }

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
            
            // _messageLabel.style.display = showMessage ? DisplayStyle.Flex : DisplayStyle.None;
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
        }
    }
}
