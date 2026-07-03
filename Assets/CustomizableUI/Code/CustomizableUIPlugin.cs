using System;
using System.IO;
using JetBrains.Annotations;
using KSP.Game;
using KSP.Messages;
using Redux.ExtraModTypes;
using SpaceWarp2.UI.API.Appbar;
using UnityEngine;
using CustomizableUI.Groups;
using CustomizableUI.UI;
using CustomizableUI.Utilities;

namespace CustomizableUI
{
    public class CustomizableUIPlugin : KerbalMod
    {
        [PublicAPI] public const string ModGuid = "CustomizableUI";
        [PublicAPI] public const string ModName = "Customizable UI";

        [PublicAPI] public static CustomizableUIPlugin Instance { get; private set; }

        public const string ToolbarFlightButtonID = "BTN-CustomizableUI";

        internal Texture2D AppIcon;

        public override void OnPreInitialized()
        {
            Instance = this;

            AppIcon = LoadIcon("icon.png");

            Settings.Initialize();
        }

        public override void OnInitialized()
        {
            Appbar.RegisterAppButton(
                ModName,
                ToolbarFlightButtonID,
                AppIcon,
                isOpen =>
                {
                    // Initialization via FlightViewEnteredMessage/Update() polling can lag behind
                    // the app bar becoming clickable -- make sure groups exist before the window
                    // that edits them opens.
                    if (isOpen && !GroupRegistry.Instance.IsInitialized)
                    {
                        GroupRegistry.Instance.Initialize();
                        if (GroupRegistry.Instance.IsInitialized)
                            SaveLoadUtility.LoadData();
                    }

                    SceneController.Instance.ToggleUI(isOpen);
                }
            );

            SubscribeToMessages();
        }

        private void SubscribeToMessages()
        {
            try
            {
                Game.Messages.PersistentSubscribe<FlightViewEnteredMessage>(OnFlightViewEntered);
                Game.Messages.PersistentSubscribe<FlightViewLeftMessage>(OnFlightViewLeft);
                SWLogger.LogInfo("Successfully subscribed to flight view messages.");
            }
            catch (Exception ex)
            {
                SWLogger.LogError($"Error subscribing to flight view messages.\n{ex}");
            }
        }

        private void OnFlightViewEntered(MessageCenterMessage message)
        {
            GroupRegistry.Instance.Initialize();
            if (GroupRegistry.Instance.IsInitialized)
                SaveLoadUtility.LoadData();
        }

        private void OnFlightViewLeft(MessageCenterMessage message)
        {
            SceneController.Instance.ToggleUI(false);
            GroupRegistry.Instance.Reset();
        }

        private void Update()
        {
            // Initialization via FlightViewEnteredMessage sometimes fires before the flight HUD's
            // instruments have finished spawning, so also poll here as a fallback (mirrors the
            // legacy mod's approach). When this fallback is the one that actually succeeds, the
            // message handler's own LoadData() call never ran, so the saved layout (including
            // hidden groups) would otherwise never get applied.
            var wasInitialized = GroupRegistry.Instance.IsInitialized;
            GroupRegistry.Instance.InitializeIfNeeded();
            if (!wasInitialized && GroupRegistry.Instance.IsInitialized)
                SaveLoadUtility.LoadData();

            // The game's own UIFlightHud force-reactivates these same instrument GameObjects on
            // vessel-changed/vessel-created events (see GroupHandle.EnforceVisibility), so keep
            // reasserting our hidden groups every frame rather than trusting a one-time apply.
            if (GroupRegistry.Instance.IsInitialized)
                GroupRegistry.Instance.EnforceVisibility();

            if ((Settings.EnableKeybinding?.Value ?? false) &&
                (Settings.Keybind1.Value == KeyCode.None || Input.GetKey(Settings.Keybind1.Value)) &&
                (Settings.Keybind2.Value == KeyCode.None || Input.GetKeyDown(Settings.Keybind2.Value)))
            {
                SceneController.Instance.ToggleUI(!SceneController.Instance.ShowMainGui);
            }
        }

        private Texture2D LoadIcon(string fileName)
        {
            try
            {
                var path = Path.Combine(SWMetadata.Folder.FullName, "assets", "images", fileName);
                var bytes = File.ReadAllBytes(path);
                var texture = new Texture2D(2, 2);
                texture.LoadImage(bytes);
                return texture;
            }
            catch (Exception ex)
            {
                SWLogger.LogError($"Error loading icon \"{fileName}\".\n{ex}");
                return new Texture2D(2, 2);
            }
        }
    }
}
