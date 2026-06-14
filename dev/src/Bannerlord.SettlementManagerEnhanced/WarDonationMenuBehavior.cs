using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.SettlementManagerEnhanced
{
    /// <summary>
    /// Adds a town keep menu flow that converts clan influence into wartime fundraising denars.
    /// </summary>
    public sealed class WarDonationMenuBehavior : CampaignBehaviorBase
    {
        private const int SmallInfluenceCost = 100;
        private const int MediumInfluenceCost = 1000;
        private const int LargeInfluenceCost = 10000;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
        {
            AddGameMenus(campaignGameStarter);
        }

        private void AddGameMenus(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption(
                "town_keep",
                "sme_mno_war_donation",
                "{=SME_WAR_DONATION_START}Start a war donation drive",
                game_menu_sme_war_donation_on_condition,
                game_menu_sme_war_donation_on_consequence,
                isLeave: false,
                5);

            starter.AddGameMenu(
                "sme_town_keep_war_donation",
                "{=SME_WAR_DONATION_MENU_DESC}War needs coin as much as steel. Call on your political capital to raise denars for the campaign. Current rate: {SME_WAR_DONATION_RATIO} denars for each influence spent.",
                game_menu_sme_war_donation_menu_on_init,
                GameMenu.MenuOverlayType.SettlementWithBoth);

            starter.AddGameMenuOption(
                "sme_town_keep_war_donation",
                "sme_mno_war_donation_small",
                "{=SME_WAR_DONATION_SMALL}Small drive - spend {SME_WAR_DONATION_SMALL_COST} influence for {SME_WAR_DONATION_SMALL_GOLD} denars",
                game_menu_sme_war_donation_small_on_condition,
                game_menu_sme_war_donation_small_on_consequence,
                isLeave: false,
                1);

            starter.AddGameMenuOption(
                "sme_town_keep_war_donation",
                "sme_mno_war_donation_medium",
                "{=SME_WAR_DONATION_MEDIUM}Medium drive - spend {SME_WAR_DONATION_MEDIUM_COST} influence for {SME_WAR_DONATION_MEDIUM_GOLD} denars",
                game_menu_sme_war_donation_medium_on_condition,
                game_menu_sme_war_donation_medium_on_consequence,
                isLeave: false,
                2);

            starter.AddGameMenuOption(
                "sme_town_keep_war_donation",
                "sme_mno_war_donation_large",
                "{=SME_WAR_DONATION_LARGE}Large drive - spend {SME_WAR_DONATION_LARGE_COST} influence for {SME_WAR_DONATION_LARGE_GOLD} denars",
                game_menu_sme_war_donation_large_on_condition,
                game_menu_sme_war_donation_large_on_consequence,
                isLeave: false,
                3);

            starter.AddGameMenuOption(
                "sme_town_keep_war_donation",
                "sme_mno_war_donation_back",
                "{=SME_ARENA_BACK}Back",
                game_menu_sme_war_donation_back_on_condition,
                game_menu_sme_war_donation_back_on_consequence,
                isLeave: true,
                4);
        }

        private bool game_menu_sme_war_donation_on_condition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            return IsTownKeepAvailable();
        }

        private void game_menu_sme_war_donation_on_consequence(MenuCallbackArgs args)
        {
            GameMenu.SwitchToMenu("sme_town_keep_war_donation");
        }

        private void game_menu_sme_war_donation_menu_on_init(MenuCallbackArgs args)
        {
            MBTextManager.SetTextVariable("SME_WAR_DONATION_RATIO", GetDenarsPerInfluence());
        }

        private bool game_menu_sme_war_donation_small_on_condition(MenuCallbackArgs args)
        {
            return SetDonationOption(args, SmallInfluenceCost, "SME_WAR_DONATION_SMALL_COST", "SME_WAR_DONATION_SMALL_GOLD");
        }

        private void game_menu_sme_war_donation_small_on_consequence(MenuCallbackArgs args)
        {
            ApplyDonation(SmallInfluenceCost);
        }

        private bool game_menu_sme_war_donation_medium_on_condition(MenuCallbackArgs args)
        {
            return SetDonationOption(args, MediumInfluenceCost, "SME_WAR_DONATION_MEDIUM_COST", "SME_WAR_DONATION_MEDIUM_GOLD");
        }

        private void game_menu_sme_war_donation_medium_on_consequence(MenuCallbackArgs args)
        {
            ApplyDonation(MediumInfluenceCost);
        }

        private bool game_menu_sme_war_donation_large_on_condition(MenuCallbackArgs args)
        {
            return SetDonationOption(args, LargeInfluenceCost, "SME_WAR_DONATION_LARGE_COST", "SME_WAR_DONATION_LARGE_GOLD");
        }

        private void game_menu_sme_war_donation_large_on_consequence(MenuCallbackArgs args)
        {
            ApplyDonation(LargeInfluenceCost);
        }

        private bool game_menu_sme_war_donation_back_on_condition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Leave;
            return true;
        }

        private void game_menu_sme_war_donation_back_on_consequence(MenuCallbackArgs args)
        {
            GameMenu.SwitchToMenu("town_keep");
        }

        private static bool SetDonationOption(MenuCallbackArgs args, int influenceCost, string costVariable, string goldVariable)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Ransom;

            int goldGain = CalculateGoldGain(influenceCost);
            MBTextManager.SetTextVariable(costVariable, influenceCost);
            MBTextManager.SetTextVariable(goldVariable, goldGain);

            Clan? clan = Clan.PlayerClan;
            if (clan == null || clan.Influence < influenceCost)
            {
                var disabledText = new TextObject("{=SME_WAR_DONATION_DISABLED}Not enough influence. You need at least {INFLUENCE} influence.");
                disabledText.SetTextVariable("INFLUENCE", influenceCost);

                args.IsEnabled = false;
                args.Tooltip = disabledText;
            }

            return IsTownKeepAvailable();
        }

        private static void ApplyDonation(int influenceCost)
        {
            var settings = SettlementManagerSettings.Instance;
            if (settings != null && !settings.ModEnabled)
                return;

            Clan? clan = Clan.PlayerClan;
            Hero? mainHero = Hero.MainHero;
            if (clan == null || mainHero == null || clan.Influence < influenceCost || !IsTownKeepAvailable())
                return;

            int goldGain = CalculateGoldGain(influenceCost);
            if (goldGain <= 0)
                return;

            ChangeClanInfluenceAction.Apply(clan, -influenceCost);
            GiveGoldAction.ApplyBetweenCharacters(null, mainHero, goldGain, disableNotification: false);

            if (settings == null || settings.ShowNotifications)
            {
                var msg = new TextObject("{=SME_WAR_DONATION_DONE}War donation complete: spent {INFLUENCE} influence and raised {GOLD} denars.");
                msg.SetTextVariable("INFLUENCE", influenceCost);
                msg.SetTextVariable("GOLD", goldGain);
                InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Yellow));
            }
        }

        private static int CalculateGoldGain(int influenceCost)
        {
            int denarsPerInfluence = GetDenarsPerInfluence();
            long goldGain = (long)influenceCost * denarsPerInfluence;
            return goldGain > int.MaxValue ? int.MaxValue : (int)goldGain;
        }

        private static int GetDenarsPerInfluence()
        {
            int configured = SettlementManagerSettings.Instance?.WarDonationDenarsPerInfluence ?? 1000;
            return configured < 1 ? 1 : configured;
        }

        private static bool IsTownKeepAvailable()
        {
            var settings = SettlementManagerSettings.Instance;
            return (settings == null || settings.ModEnabled)
                && Settlement.CurrentSettlement?.IsTown == true;
        }
    }
}
