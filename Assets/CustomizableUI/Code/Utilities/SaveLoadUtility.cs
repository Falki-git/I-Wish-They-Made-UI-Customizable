using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using CustomizableUI.Groups;
using ILogger = ReduxLib.Logging.ILogger;

namespace CustomizableUI.Utilities
{
    /// <summary>
    /// Saves/loads the current layout as a single JSON file in the mod's own folder. This is a
    /// global layout (not per KSP2 save file), matching the legacy mod's scope -- just relocated
    /// from "next to the DLL" to the mod's proper data folder.
    /// </summary>
    public static class SaveLoadUtility
    {
        private static readonly ILogger Logger = ReduxLib.ReduxLib.GetLogger($"CustomizableUI|{nameof(SaveLoadUtility)}");

        private static string SavePath => Path.Combine(CustomizableUIPlugin.Instance.SWMetadata.Folder.FullName, "SavedData.json");

        public static void SaveData()
        {
            try
            {
                var layouts = GroupRegistry.Instance.Groups.Select(g => g.ToLayout()).ToList();
                File.WriteAllText(SavePath, JsonConvert.SerializeObject(layouts));
                Logger.LogInfo("Save data successful.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error saving data. Full error:\n{ex}");
            }
        }

        public static void LoadData()
        {
            try
            {
                var json = File.ReadAllText(SavePath);
                var layouts = JsonConvert.DeserializeObject<List<GroupLayout>>(json);
                if (layouts == null)
                    return;

                foreach (var group in GroupRegistry.Instance.Groups)
                {
                    var layout = layouts.Find(l => l.Key == group.Key);
                    if (layout != null)
                        group.ApplyLayout(layout);
                }

                Logger.LogInfo("Load data successful.");
            }
            catch (FileNotFoundException)
            {
                Logger.LogInfo("No saved layout found yet -- this is normal on first use.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error loading data. Full error:\n{ex}");
            }
        }
    }
}
