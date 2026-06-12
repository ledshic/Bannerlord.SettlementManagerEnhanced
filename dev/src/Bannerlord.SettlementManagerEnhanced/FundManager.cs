using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.SettlementManagerEnhanced
{
    /// <summary>
    /// Manager for construction fund (BoostBuildingProcess) enhancements.
    ///
    /// Vanilla reference (found via wiki + modding docs + common models):
    /// - "Current fund" = settlement.Town.BoostBuildingProcess (the reserve gold sunk for construction).
    /// - Vanilla flat boost (the "fund costs 500 every day and provides 50 build speed"):
    ///     Towns: +50 construction at ~500 denars daily cost.
    ///     Castles: +20 construction at ~250 denars daily cost.
    ///   This is implemented around DefaultConstructionModel (CalculateDailyConstructionPower) +
    ///   BuildingHelper.BoostBuildingProcessWithGold (the API to add to the reserve when you transfer gold in the UI).
    ///   The reserve is slowly consumed to deliver the construction points to the active project (or default project).
    ///
    /// Our addition (per spec):
    /// - On top of vanilla flat behavior, when fund > 0 on a *player-owned* fortification:
    ///   additional daily cost = current_fund * 0.02 (town) or * 0.04 (castle)
    ///   additional build speed granted = additional_cost * 0.1 (town) or * 0.05 (castle)
    /// - We deduct the % directly from BoostBuildingProcess (the "cost").
    /// - We apply the extra build points as progress to the current building (or current default project).
    /// - This makes large reserves (now transferable up to 1M) actually useful for rushing construction.
    /// </summary>
    public class FundManager
    {
        /// <summary>
        /// Called from daily settlement tick (and force button).
        /// Returns the extra build points granted this pass (for optional notif aggregation).
        /// </summary>
        public int ProcessDailyFundBoost(Settlement settlement, SettlementManagerSettings settings)
        {
            if (settlement == null || settlement.Town == null)
                return 0;

            if (settings == null || !settings.ModEnabled || !settings.FundScalingEnabled)
                return 0;

            if (!SettlementManagerHelper.IsPlayerFortification(settlement))
                return 0;

            Town town = settlement.Town;
            int currentFund = Math.Max(0, town.BoostBuildingProcess);
            if (currentFund <= 0)
                return 0;

            bool isCastle = settlement.IsCastle;
            float burnRate = isCastle ? 0.04f : 0.02f;
            float efficiency = isCastle ? 0.05f : 0.1f;

            int additionalCost = (int)Math.Round(currentFund * burnRate);
            if (additionalCost > currentFund)
                additionalCost = currentFund;
            if (additionalCost <= 0)
                return 0;

            int additionalBuild = (int)Math.Round(additionalCost * efficiency);
            if (additionalBuild <= 0)
                return 0;

            // Cost: burn from the current fund reserve
            town.BoostBuildingProcess = currentFund - additionalCost;

            // Provide the build speed: apply progress to active (or default) project
            SettlementManagerHelper.ApplyConstructionProgress(town, additionalBuild);

            // Optional notification (per settlement)
            if (settings.ShowNotifications)
            {
                try
                {
                    var text = new TextObject("{=SME_FUND_001}Construction fund at {SETTLEMENT}: burned {COST} for +{BUILD} build speed.", null);
                    text.SetTextVariable("SETTLEMENT", settlement.Name?.ToString() ?? "settlement");
                    text.SetTextVariable("COST", additionalCost);
                    text.SetTextVariable("BUILD", additionalBuild);
                    InformationManager.DisplayMessage(new InformationMessage(text.ToString(), Colors.Yellow));
                }
                catch { /* non-fatal */ }
            }

            Debug.Print($"[SettlementManagerEnhanced][Fund] {settlement.Name} (castle={isCastle}): burned {additionalCost} from fund (was {currentFund}), granted +{additionalBuild} build points. Remaining fund: {town.BoostBuildingProcess}");

            return additionalBuild;
        }
    }
}
