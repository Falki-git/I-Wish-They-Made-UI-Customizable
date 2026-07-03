using System;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;
using ILogger = ReduxLib.Logging.ILogger;

namespace CustomizableUI.UI
{
    /// <summary>
    /// Loads the mod's UXML through Addressables (the current template's asset pipeline --
    /// ThunderKit's "Build for Editor"/"Build for Player" pipelines stage addressables groups
    /// rather than raw AssetBundles). Lazily initialized on first access, which in practice is
    /// well after OnInitialized() has run and the mod's addressables catalog is loaded.
    /// </summary>
    public class Uxmls
    {
        public static Uxmls Instance { get; } = new();

        public VisualTreeAsset MainGui { get; }

        private const string MainGuiAddress = "Assets/CustomizableUI/UI/CustomizableUI.uxml";

        private static readonly ILogger Logger = ReduxLib.ReduxLib.GetLogger($"CustomizableUI|{nameof(Uxmls)}");

        private Uxmls()
        {
            MainGui = Load(MainGuiAddress);
        }

        private static VisualTreeAsset Load(string address)
        {
            try
            {
                var handle = Addressables.LoadAssetAsync<VisualTreeAsset>(address);
                return handle.WaitForCompletion();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load VisualTreeAsset at address \"{address}\".\n{ex}");
                return null;
            }
        }
    }
}
