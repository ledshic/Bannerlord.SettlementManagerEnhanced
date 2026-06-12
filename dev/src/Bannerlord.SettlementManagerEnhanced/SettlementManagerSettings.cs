using System.Collections.Generic;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace Bannerlord.SettlementManagerEnhanced
{
    /// <summary>
    /// MCMv5 Global settings for SettlementManagerEnhanced.
    /// Mirrors TroopManagerEnhanced structure and conventions (global json, {=SME_} keys, groups, force buttons where sensible).
    /// </summary>
    public sealed class SettlementManagerSettings : AttributeGlobalSettings<SettlementManagerSettings>
    {
        public override string Id => "Bannerlord.SettlementManagerEnhanced_v1";
        public override string DisplayName
        {
            get
            {
                var ver = typeof(SettlementManagerSettings).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
                return new TextObject("{=SME_MainDisplay}Settlement Manager Enhanced {VERSION}", new Dictionary<string, object>
                {
                    { "VERSION", ver }
                }).ToString();
            }
        }
        public override string FolderName => "Bannerlord.SettlementManagerEnhanced";
        public override string FormatType => "json";

        #region General / Master Toggles

        [SettingPropertyBool(
            "{=SME_EnableMod}Enable Mod",
            RequireRestart = false,
            HintText = "{=SME_EnableModHint}Master toggle. When off, no settlement enhancements or auto actions will occur.")]
        [SettingPropertyGroup("{=SME_General}General")]
        public bool ModEnabled { get; set; } = true;

        [SettingPropertyBool(
            "{=SME_ShowNotifs}Show Notifications",
            RequireRestart = false,
            HintText = "{=SME_ShowNotifsHint}Display information messages for fund scaling, construction boosts, and garrison auto-recruits.")]
        [SettingPropertyGroup("{=SME_General}General")]
        public bool ShowNotifications { get; set; } = true;

        #endregion

        #region Fund Transfer & Daily Boost (current fund scaling)

        [SettingPropertyBool(
            "{=SME_FundEnhance}Enable Fund Scaling (2%/4% daily)",
            RequireRestart = false,
            HintText = "{=SME_FundEnhanceHint}When current construction fund (BoostBuildingProcess) > 0 on player-owned towns/castles: burn additional 2% (town) or 4% (castle) of the fund each day and grant extra build speed at vanilla-like efficiency (0.1 / 0.05 build-per-gold). This is *on top of* vanilla flat 500g->50 (town) / 250g->20 (castle) behavior. Requires large transfers (patched to 1M max).")]
        [SettingPropertyGroup("{=SME_Fund}Fund Transfer & Boost")]
        public bool FundScalingEnabled { get; set; } = true;

        [SettingPropertyBool(
            "{=SME_RaiseTransfer}Raise Transfer Limit to 1,000,000 (UI)",
            RequireRestart = false,
            HintText = "{=SME_RaiseTransferHint}Patches the settlement construction fund transfer slider / dialog so you can move up to 1,000,000 denars in one transaction (vanilla caps at 10,000). The patch runs when the settlement screen refreshes.")]
        [SettingPropertyGroup("{=SME_Fund}Fund Transfer & Boost")]
        public bool RaiseTransferLimit { get; set; } = true;

        // Optional manual trigger for fund processing across all owned fiefs (resets automatically).
        private bool _forceFund;
        private bool _isForcingFund;

        [SettingPropertyBool(
            "{=SME_ForceFund}Force Fund Scaling Pass Now",
            RequireRestart = false,
            HintText = "{=SME_ForceFundHint}Immediately run a fund scaling + build progress pass on all your owned towns and castles (respects the Enable toggle above). Resets after trigger.")]
        [SettingPropertyGroup("{=SME_Fund}Fund Transfer & Boost")]
        public bool ForceFundPassNow
        {
            get => _forceFund;
            set
            {
                if (value && !_isForcingFund)
                {
                    _isForcingFund = true;
                    _forceFund = false;

                    try
                    {
                        if (ModEnabled && FundScalingEnabled)
                        {
                            // The behavior owns the managers; we trigger via a public helper on a temp instance for force.
                            // For simplicity we create a light manager and enumerate settlements here.
                            var manager = new FundManager();
                            var settlements = Campaign.Current?.Settlements;
                            if (settlements != null)
                            {
                                int affected = 0;
                                foreach (var s in settlements)
                                {
                                    if (s != null && SettlementManagerHelper.IsPlayerFortification(s) && manager.ProcessDailyFundBoost(s, this) > 0)
                                        affected++;
                                }
                                if (affected > 0 && ShowNotifications)
                                {
                                    var text = new TextObject("{=SME_FUND_FORCE}Forced fund pass affected {N} settlements.", null);
                                    text.SetTextVariable("N", affected);
                                    InformationManager.DisplayMessage(new InformationMessage(text.ToString(), Colors.Gold));
                                }
                            }
                        }
                    }
                    finally
                    {
                        _isForcingFund = false;
                    }
                }
                else if (!value)
                {
                    _forceFund = false;
                }
            }
        }

        #endregion

        #region Castle Garrison Auto Recruit from Prison (prison first, ignore NPC heroes)

        [SettingPropertyBool(
            "{=SME_CastleRecruit}Castle Auto Recruit from Prison",
            RequireRestart = false,
            HintText = "{=SME_CastleRecruitHint}Every day, for your owned castles, automatically recruit conformity-ready prisoners from the castle's prison (and garrison prison) into the garrison. Prison processed first. NPC heroes (IsHero) are always ignored.")]
        [SettingPropertyGroup("{=SME_Garrison}Castle Garrison Recruit")]
        public bool CastlePrisonAutoRecruitEnabled { get; set; } = true;

        [SettingPropertyInteger(
            "{=SME_GarrTier}Minimum Prisoner Tier (Garrison)",
            0, 6, "0",
            RequireRestart = false,
            HintText = "{=SME_GarrTierHint}Only auto-recruit prisoners of this tier or higher into castle garrisons.")]
        [SettingPropertyGroup("{=SME_Garrison}Castle Garrison Recruit")]
        public int MinimumGarrisonPrisonerTier { get; set; } = 0;

        [SettingPropertyInteger(
            "{=SME_MaxGarrPer}Max Garrison Recruits Per Castle Per Day",
            1, 100, "0",
            RequireRestart = false,
            HintText = "{=SME_MaxGarrPerHint}Hard cap per castle on how many prisoners can be auto-recruited into its garrison per daily tick.")]
        [SettingPropertyGroup("{=SME_Garrison}Castle Garrison Recruit")]
        public int MaxGarrisonRecruitsPerCastle { get; set; } = 10;

        [SettingPropertyBool(
            "{=SME_GarrHighTier}Prioritize High Tier Prisoners (Garrison)",
            RequireRestart = false,
            HintText = "{=SME_GarrHighTierHint}When recruiting for garrisons, take higher tier prisoners first.")]
        [SettingPropertyGroup("{=SME_Garrison}Castle Garrison Recruit")]
        public bool PrioritizeHighTierGarrisonPrisoners { get; set; } = true;

        // Force all castles now (one-shot)
        private bool _forceGarr;
        private bool _isForcingGarr;

        [SettingPropertyBool(
            "{=SME_ForceGarr}Force Castle Garrison Recruit Now",
            RequireRestart = false,
            HintText = "{=SME_ForceGarrHint}Immediately attempt prisoner recruitment from prisons into garrisons for all your owned castles (respects tier, caps, and enable). Resets automatically.")]
        [SettingPropertyGroup("{=SME_Garrison}Castle Garrison Recruit")]
        public bool ForceGarrisonRecruitNow
        {
            get => _forceGarr;
            set
            {
                if (value && !_isForcingGarr)
                {
                    _isForcingGarr = true;
                    _forceGarr = false;
                    try
                    {
                        if (ModEnabled && CastlePrisonAutoRecruitEnabled)
                        {
                            var manager = new GarrisonRecruitManager();
                            var settlements = Campaign.Current?.Settlements;
                            int total = 0;
                            if (settlements != null)
                            {
                                foreach (var s in settlements)
                                {
                                    if (s != null && s.IsCastle && SettlementManagerHelper.IsPlayerFortification(s))
                                        total += manager.TryAutoRecruitForCastle(s, this);
                                }
                            }
                            if (total > 0 && ShowNotifications)
                            {
                                var text = new TextObject("{=SME_GARR_FORCE}Forced garrison recruit: {COUNT} prisoners.", null);
                                text.SetTextVariable("COUNT", total);
                                InformationManager.DisplayMessage(new InformationMessage(text.ToString(), Colors.Cyan));
                            }
                        }
                    }
                    finally { _isForcingGarr = false; }
                }
                else if (!value) _forceGarr = false;
            }
        }

        #endregion

        /// <summary>
        /// Feature enablement helper (matches TroopManagerEnhanced pattern).
        /// </summary>
        public static bool IsFeatureEnabled(string featureKey)
        {
            var global = Instance;
            if (global == null || !global.ModEnabled) return false;

            return featureKey switch
            {
                "fund_scaling" => global.FundScalingEnabled,
                "raise_transfer" => global.RaiseTransferLimit,
                "castle_prison_recruit" => global.CastlePrisonAutoRecruitEnabled,
                _ => false
            };
        }
    }
}
