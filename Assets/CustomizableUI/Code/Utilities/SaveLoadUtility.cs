using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

        /// <summary>
        /// Bump when SaveFile's or GroupLayout's shape changes in a way that needs migration, and
        /// add that migration to ParseLayouts.
        /// </summary>
        private const int CurrentSaveVersion = 1;

        private static string SavePath => Path.Combine(CustomizableUIPlugin.Instance.SWMetadata.Folder.FullName, "SavedData.json");

        private class SaveFile
        {
            public int Version { get; set; }
            public List<GroupLayout> Groups { get; set; }
        }

        public static void SaveData()
        {
            try
            {
                var saveFile = new SaveFile
                {
                    Version = CurrentSaveVersion,
                    Groups = GroupRegistry.Instance.Groups.Select(g => g.ToLayout()).ToList(),
                };
                File.WriteAllText(SavePath, JsonConvert.SerializeObject(saveFile));
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
                var layouts = ParseLayouts(json);
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

        /// <summary>
        /// Pre-release saves were a bare JSON array of GroupLayout with no wrapper or version
        /// marker (version "0"); current saves wrap that array in a SaveFile with a Version so a
        /// future format change can detect and migrate old saves instead of failing to parse them
        /// or silently misreading their fields.
        /// </summary>
        private static List<GroupLayout> ParseLayouts(string json)
        {
            var token = JToken.Parse(json);
            if (token.Type == JTokenType.Array)
            {
                Logger.LogInfo("Loaded a pre-versioning save file (version 0); it will be upgraded to the current format on next save.");
                return token.ToObject<List<GroupLayout>>();
            }

            return token.ToObject<SaveFile>()?.Groups;
        }
    }
}
