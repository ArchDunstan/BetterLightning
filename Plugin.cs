using BepInEx;
using HarmonyLib;
using BepInEx.Logging;
using BetterLightning.Patches;

namespace BetterLightning
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class BetterLightningMod : BaseUnityPlugin
    {
        private const string modGUID = "Archmage.BetterLightning";
        private const string modName = "BetterLightning";
        private const string modVersion = "1.0.0.0";

        private readonly Harmony harmony = new Harmony(modGUID);

        private static BetterLightningMod Instance = null;

        internal ManualLogSource log;

        void Awake()
        {
            if (Instance == null)
                Instance = this;

            log = BepInEx.Logging.Logger.CreateLogSource(modGUID);

            Logger.LogInfo($"Plugin {modGUID} is loaded!");
            log.LogInfo("Testmod Loaded...");

            harmony.PatchAll();
        }
    }
}
