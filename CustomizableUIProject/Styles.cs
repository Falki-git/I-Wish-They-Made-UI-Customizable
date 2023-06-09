﻿using BepInEx.Logging;
using SpaceWarp.API.Assets;
using SpaceWarp.API.UI;
using UnityEngine;

namespace CustomizableUI
{
    public static class Styles
    {
        private static readonly ManualLogSource _logger = BepInEx.Logging.Logger.CreateLogSource("CustomizableUI.Styles");

        public static int WindowWidth = 660;
        public static string PixelFormat = "{0:F0}";

        public static GUISkin SpaceWarpUISkin;

        public static GUIStyle MainWindow;
        public static GUIStyle SelectButton;
        public static GUIStyle NormalButton;
        public static GUIStyle CloseButton;
        public static GUIStyle SelectTextField;
        public static GUIStyle GroupLabel;
        public static GUIStyle AttachToggle;
        public static GUIStyle BaseButton;
        public static GUIStyle MoveButton;
        public static GUIStyle JumpLeftRightButton;
        public static GUIStyle JumpUpDownButton;
        public static GUIStyle MessageLabel;

        public static Texture2D CloseButtonTexture;

        public static void Initialize()
        {
            InitializeTextures();

            SpaceWarpUISkin = Skins.ConsoleSkin;

            MainWindow = new GUIStyle(SpaceWarpUISkin.window)
            { };

            SelectButton = new GUIStyle(SpaceWarpUISkin.button)
            {
                fixedWidth = 70f,
                fixedHeight = 60,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 40,
                padding = new RectOffset()                
            };
            
            NormalButton = new GUIStyle(SpaceWarpUISkin.button)
            {
                alignment = TextAnchor.MiddleCenter
            };

            CloseButton = new GUIStyle(SpaceWarpUISkin.button)
            {
                fixedWidth = 20,
                fixedHeight = 20,
                padding = new RectOffset()
            };

            SelectTextField = new GUIStyle(SpaceWarpUISkin.textField)
            {
                fixedWidth = 80f,
                alignment = TextAnchor.MiddleCenter
            };

            GroupLabel = new GUIStyle(SpaceWarpUISkin.label)
            {
                fixedWidth = 450f,
                fontSize = 40,
                alignment = TextAnchor.MiddleCenter
            };

            AttachToggle = new GUIStyle(SpaceWarpUISkin.toggle)
            { };

            BaseButton = new GUIStyle(SpaceWarpUISkin.button)
            {
                margin = new RectOffset(),
                padding = new RectOffset(),
            };
            
            MoveButton = new GUIStyle(BaseButton)
            {
                fixedWidth = 40,
                fixedHeight = 40,
                fontSize = 30
            };

            JumpLeftRightButton = new GUIStyle(BaseButton)
            {
                fixedWidth = 40,
                fixedHeight = 120,
                margin = new RectOffset(5, 5, 0, 0),
                fontSize = 40
            };

            JumpUpDownButton = new GUIStyle(BaseButton)
            {
                fixedWidth = 120,
                fixedHeight = 40,
                margin = new RectOffset(0, 0, 5, 5),
                fontSize = 40
            };

            MessageLabel = new GUIStyle(SpaceWarpUISkin.label);
            MessageLabel.normal.textColor = Color.green;
        }

        private static void InitializeTextures()
        {
            // Icons from https://icons8.com
            CloseButtonTexture = LoadTexture($"{CustomizableUIPlugin.Instance.Info.Metadata.GUID}/images/close-15.png");
        }

        private static Texture2D LoadTexture(string path)
        {
            try
            {
                return AssetManager.GetAsset<Texture2D>(path);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading texture with path: {path}. Full error: \n{ex}");
                return new Texture2D(20, 20);
            }
        }
    }
}
