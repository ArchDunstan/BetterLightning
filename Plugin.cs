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
        private const string modVersion = "1.0.2";

        public  static BetterLightningMod Instance = null;

        private readonly Harmony harmony = new Harmony(modGUID);

        void Awake()
        {
            if (Instance == null)
                Instance = this;

            Logger.LogInfo($"Plugin {modGUID} is loaded!");

            harmony.PatchAll();
        }
    }
}
