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

        public UIDocument MainGui { get; private set; }

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

        private UIDocument RebuildUi(UIDocument uiDocument, bool show)
        {
            if (uiDocument != null && uiDocument.gameObject != null)
                Object.Destroy(uiDocument.gameObject);

            return show ? BuildUi() : null;
        }

        private UIDocument BuildUi()
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

            var uiDocument = Window.Create(options, visualTree);
            uiDocument.rootVisualElement[0].CenterByDefault();
            uiDocument.gameObject.AddComponent<MainGuiController>();
            return uiDocument;
        }

        public void ToggleUI(bool state)
        {
            ShowMainGui = state;

            GameObject.Find(CustomizableUIPlugin.ToolbarFlightButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(state);
        }
    }
}
