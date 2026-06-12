using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Bannerlord.SettlementManagerEnhanced
{
    /// <summary>
    /// Harmony patches for SettlementManagerEnhanced.
    ///
    /// 1. Raise current fund transfer range from vanilla ~10k to 1,000,000.
    ///    The vanilla limit lives in the settlement/town management UI (SettlementConstructionVM in SandBox.ViewModelCollection).
    ///    We use a resilient Refresh postfix + reflection to bump any internal max/transfer cap we can find.
    ///    This is the "adjust current fund transfer range from 0-10k to 0-1m" requirement.
    ///
    /// 2. Placeholder area for future patches around construction / DefaultConstructionModel if we need to
    ///    observe or lightly influence the vanilla 500/50 flat fund logic (see FundManager.cs header for the
    ///    identified vanilla hook points: DefaultConstructionModel.CalculateDailyConstructionPower and
    ///    BuildingHelper.BoostBuildingProcessWithGold).
    ///
    /// Patches are auto-applied by SubModule.PatchAll.
    /// </summary>
    [HarmonyPatch]
    public static class SettlementConstructionPatches
    {
        // The VM lives in SandBox.ViewModelCollection.dll (TownManagement sub-namespace in most versions).
        private static readonly string VmTypeName = "SandBox.ViewModelCollection.TownManagement.SettlementConstructionVM";

        /// <summary>
        /// Postfix on the VM's Refresh (or equivalent update) method.
        /// We attempt to locate and raise internal fields/properties that control the max gold the player
        /// can transfer into the settlement's construction fund (BoostBuildingProcess) in one go.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(SandBox.ViewModelCollection.TownManagement.SettlementConstructionVM), "Refresh")]
        public static void Postfix_RaiseTransferLimit(object __instance)
        {
            try
            {
                var settings = SettlementManagerSettings.Instance;
                if (settings == null || !settings.ModEnabled || !settings.RaiseTransferLimit)
                    return;

                var t = __instance.GetType();
                const int TARGET_MAX = 1000000;

                // Common internal field names seen across similar UIs / versions
                string[] candidateFields = new[]
                {
                    "_transferableGoldMax",
                    "_maxGold",
                    "TransferableGoldMax",
                    "_currentFundMax",
                    "MaxGold",
                    "TransferMax",
                    "_maxTransferAmount"
                };

                bool bumped = false;

                foreach (var name in candidateFields)
                {
                    var field = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (field != null && field.FieldType == typeof(int))
                    {
                        int current = (int)field.GetValue(__instance);
                        if (current > 0 && current < TARGET_MAX)
                        {
                            field.SetValue(__instance, TARGET_MAX);
                            bumped = true;
                        }
                    }

                    var prop = t.GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (prop != null && prop.CanWrite && prop.PropertyType == typeof(int))
                    {
                        int current = (int)prop.GetValue(__instance);
                        if (current > 0 && current < TARGET_MAX)
                        {
                            prop.SetValue(__instance, TARGET_MAX);
                            bumped = true;
                        }
                    }
                }

                // Also try a couple of common "gold input" related properties that may expose the max to the view
                string[] valueProps = new[] { "TransferableGoldMax", "MaxGoldToTransfer", "CurrentFundMax", "GoldMax" };
                foreach (var vp in valueProps)
                {
                    var p = t.GetProperty(vp, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (p != null && p.CanWrite && p.PropertyType == typeof(int))
                    {
                        int cur = (int)p.GetValue(__instance);
                        if (cur > 0 && cur < TARGET_MAX)
                        {
                            p.SetValue(__instance, TARGET_MAX);
                            bumped = true;
                        }
                    }
                }

                if (bumped)
                {
                    // Optional: also ensure the displayed current/max strings don't hard-clamp in some label (best effort)
                    Debug.Print("[SettlementManagerEnhanced] Raised settlement fund transfer limit toward 1,000,000 via VM patch.");
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"[SettlementManagerEnhanced][Patches] Transfer limit patch error (non-fatal): {ex.Message}");
            }
        }

        // Example of an optional model-level observation patch (commented; kept for future if vanilla flat 500/50 needs suppression or scaling awareness).
        /*
        [HarmonyPostfix]
        [HarmonyPatch(typeof(DefaultConstructionModel), "CalculateDailyConstructionPower")]
        public static void Postfix_ObserveVanillaConstruction(Town town, ref ExplainedNumber __result)
        {
            // Here one could read town.BoostBuildingProcess and understand the vanilla contribution.
            // Our mod prefers to layer on top via daily settlement tick + direct Boost + BuildingProgress for predictability.
        }
        */
    }
}
