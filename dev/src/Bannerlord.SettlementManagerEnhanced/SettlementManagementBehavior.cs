using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Bannerlord.SettlementManagerEnhanced
{
    /// <summary>
    /// Campaign behavior for settlement management enhancements.
    ///
    /// - DailyTickSettlementEvent (natural rhythm, good perf).
    /// - Fund scaling / extra construction from current fund (BoostBuildingProcess) for player-owned forts.
    /// - Castle garrison auto-recruit from prison (prison first, skip IsHero NPCs).
    ///
    /// Matches the thin daily orchestration style of TroopManagementBehavior.
    /// </summary>
    public class SettlementManagementBehavior : CampaignBehaviorBase
    {
        private readonly FundManager _fundManager = new FundManager();
        private readonly GarrisonRecruitManager _garrisonRecruitManager = new GarrisonRecruitManager();

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, OnDailyTickSettlement);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Global settings only; no per-save data (matches reference).
        }

        private void OnDailyTickSettlement(Settlement settlement)
        {
            var settings = SettlementManagerSettings.Instance;
            if (settings == null || !settings.ModEnabled)
                return;

            if (settlement == null || !settlement.IsFortification)
                return;

            // Only enhance player-owned settlements (transfer UI + meaningful for player).
            if (!SettlementManagerHelper.IsPlayerFortification(settlement))
                return;

            try
            {
                // 1. Fund scaling + extra daily build speed from current fund (2%/4% burn).
                if (settings.FundScalingEnabled)
                {
                    _fundManager.ProcessDailyFundBoost(settlement, settings);
                }

                // 2. Castle-specific: auto recruit from prison (prison first).
                if (settlement.IsCastle && settings.CastlePrisonAutoRecruitEnabled)
                {
                    _garrisonRecruitManager.TryAutoRecruitForCastle(settlement, settings);
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"[SettlementManagerEnhanced] Exception during daily settlement tick for {settlement.Name}: {ex}");
            }
        }
    }
}
