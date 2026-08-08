using KSP.UI.Binding;
using UitkForKsp2.API;
using UnityEngine;
using UnityEngine.UIElements;

namespace CustomizableUI.UI
{
    /// <summary>Owns the editor window's open/closed state. Replaces the legacy static UI singleton.</summary>
    public class SceneController
    {
        public static SceneController Instance { get; } = new();

        /// <summary>
        /// The window's renderer. PanelRenderer, not UIDocument: as of KSP2 0.2.9.0 / UitkForKsp2
        /// 26w32b, Window.Create builds windows on Unity 6.5's PanelRenderer and never adds a
        /// UIDocument to the window GameObject.
        /// </summary>
        public PanelRenderer MainGui { get; private set; }

        private bool _showMainGui;
        public bool ShowMainGui
        {
            get => _showMainGui;
            private set
            {
                _showMainGui = value;
                MainGui = RebuildUi(MainGui, value);
            }
        }

        private SceneController() { }

        private PanelRenderer RebuildUi(PanelRenderer window, bool show)
        {
            if (window != null && window.gameObject != null)
                Object.Destroy(window.gameObject);

            return show ? BuildUi() : null;
        }

        private PanelRenderer BuildUi()
        {
            var visualTree = Uxmls.Instance.MainGui;
            if (visualTree == null)
                return null;

            var options = WindowOptions.Default;
            options.WindowId = "CustomizableUIMainGui";
            options.IsHidingEnabled = true;
            options.MoveOptions = new MoveOptions
            {
                IsMovingEnabled = true,
                CheckScreenBounds = true,
            };
            options.DisableGameInputForTextFields = true;

            // Spell the type out rather than using `var`: Window.Create's return type changed
            // (UIDocument -> PanelRenderer) in UitkForKsp2 26w32b, and because `var` absorbed that
            // silently, the only symptom was MainGuiController.OnEnable NREing on a null
            // GetComponent<UIDocument>(). An explicit type turns the next such change into a
            // compile error instead.
            PanelRenderer window = Window.Create(options, visualTree);

            // PanelRenderer.rootVisualElement is internal, so the window element comes from
            // UitkForKsp2's GetWindowRoot() extension -- it already descends through the
            // TemplateContainer wrappers, so it replaces the old `rootVisualElement[0]` rather
            // than being indexed again. Window.Create ran WindowComponent.ResolveNow() before
            // returning, so the root is resolved by now; guard anyway, since a UXML that failed to
            // resolve should not take the window down with an NRE.
            window.GetWindowRoot()?.CenterByDefault();

            window.gameObject.AddComponent<MainGuiController>();
            return window;
        }

        public void ToggleUI(bool state)
        {
            ShowMainGui = state;

            GameObject.Find(CustomizableUIPlugin.ToolbarFlightButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(state);
        }
    }
}
