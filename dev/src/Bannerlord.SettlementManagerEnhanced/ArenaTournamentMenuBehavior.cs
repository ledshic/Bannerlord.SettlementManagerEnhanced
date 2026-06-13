using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.SettlementManagerEnhanced
{
    /// <summary>
    /// Adds extra arena menu options:
    /// 1) Ask upcoming tournament locations (with a submenu and map tracking action).
    /// 2) Sponsor a tournament in current town for 10,000 denars.
    /// </summary>
    public sealed class ArenaTournamentMenuBehavior : CampaignBehaviorBase
    {
        private const int SponsorCost = 10000;

        private readonly List<Settlement> _lastQueriedTournamentSettlements = new List<Settlement>();
        private string _lastQueryReplyText = string.Empty;

        // If a town already has an active tournament, queue one sponsored tournament for later.
        private Dictionary<Town, int> _queuedSponsoredTournaments = new Dictionary<Town, int>();

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, OnDailyTickSettlement);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // NOTE: _queuedSponsoredTournaments is NOT persisted across saves.
            // It's transient in-memory data that queues upcoming tournaments if the current town already has an active tournament.
            // We don't serialize it because Dictionary<Town, int> cannot be serialized (Town is a non-serializable game object).
            // This doesn't matter because queued tournaments will eventually be scheduled on the next daily tick after the current tournament ends.
            _queuedSponsoredTournaments ??= new Dictionary<Town, int>();
        }

        private void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
        {
            AddGameMenus(campaignGameStarter);
        }

        private void AddGameMenus(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption(
                "town_arena",
                "sme_mno_tournament_query",
                "{=SME_ARENA_ASK_NEXT}Ask where the next tournaments will be held",
                game_menu_sme_tournament_query_on_condition,
                game_menu_sme_tournament_query_on_consequence,
                isLeave: false,
                4);

            starter.AddGameMenuOption(
                "town_arena",
                "sme_mno_tournament_sponsor",
                "{=SME_ARENA_HOST_HERE}Sponsor a tournament here (10,000)",
                game_menu_sme_tournament_sponsor_on_condition,
                game_menu_sme_tournament_sponsor_on_consequence,
                isLeave: false,
                5);

            starter.AddGameMenu(
                "sme_town_arena_tournament_query",
                "{=SME_ARENA_QUERY_MENU}{SME_TOURNAMENT_QUERY_REPLY}",
                game_menu_sme_tournament_query_menu_on_init,
                GameMenu.MenuOverlayType.SettlementWithBoth);

            starter.AddGameMenuOption(
                "sme_town_arena_tournament_query",
                "sme_mno_tournament_locate",
                "{=SME_ARENA_LOCATE}Locate these towns",
                game_menu_sme_tournament_locate_on_condition,
                game_menu_sme_tournament_locate_on_consequence,
                isLeave: false,
                1);

            starter.AddGameMenuOption(
                "sme_town_arena_tournament_query",
                "sme_mno_tournament_query_back",
                "{=SME_ARENA_BACK}Back",
                game_menu_sme_tournament_query_back_on_condition,
                game_menu_sme_tournament_query_back_on_consequence,
                isLeave: true,
                2);
        }

        private bool game_menu_sme_tournament_query_on_condition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            return Settlement.CurrentSettlement?.Town != null;
        }

        private void game_menu_sme_tournament_query_on_consequence(MenuCallbackArgs args)
        {
            BuildTournamentQueryResult(Settlement.CurrentSettlement?.Town, out var replyText, out var targets);

            _lastQueryReplyText = replyText?.ToString() ?? string.Empty;
            _lastQueriedTournamentSettlements.Clear();
            _lastQueriedTournamentSettlements.AddRange(targets);

            GameMenu.SwitchToMenu("sme_town_arena_tournament_query");
        }

        private bool game_menu_sme_tournament_sponsor_on_condition(MenuCallbackArgs args)
        {
            int currentGold = Hero.MainHero?.Gold ?? 0;
            bool disableOption = currentGold < SponsorCost;

            args.optionLeaveType = GameMenuOption.LeaveType.Ransom;

            var disabledText = new TextObject("{=SME_ARENA_HOST_HERE_DISABLED}You need at least {GOLD} denars.");
            disabledText.SetTextVariable("GOLD", SponsorCost);

            return MenuHelper.SetOptionProperties(args, canPlayerDo: true, disableOption, disabledText);
        }

        private void game_menu_sme_tournament_sponsor_on_consequence(MenuCallbackArgs args)
        {
            var settings = SettlementManagerSettings.Instance;
            if (settings != null && !settings.ModEnabled)
                return;

            Settlement? currentSettlement = Settlement.CurrentSettlement;
            Town? currentTown = currentSettlement?.Town;
            Hero? mainHero = Hero.MainHero;

            if (currentTown == null || currentSettlement == null || mainHero == null || mainHero.Gold < SponsorCost)
                return;

            GiveGoldAction.ApplyBetweenCharacters(mainHero, null, SponsorCost, disableNotification: false);

            bool startedNow = TryStartSponsoredTournament(currentTown);
            if (!startedNow)
            {
                if (!_queuedSponsoredTournaments.ContainsKey(currentTown))
                    _queuedSponsoredTournaments[currentTown] = 0;

                _queuedSponsoredTournaments[currentTown]++;
            }

            if (settings == null || settings.ShowNotifications)
            {
                TextObject msg = startedNow
                    ? new TextObject("{=SME_ARENA_HOSTED_NOW}A sponsored tournament has been added in {TOWN}.")
                    : new TextObject("{=SME_ARENA_QUEUED}{TOWN} already has an active tournament. Your sponsored tournament has been queued for the next available round.");

                msg.SetTextVariable("TOWN", currentSettlement.Name?.ToString() ?? "town");
                InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Yellow));
            }
        }

        private void game_menu_sme_tournament_query_menu_on_init(MenuCallbackArgs args)
        {
            MBTextManager.SetTextVariable("SME_TOURNAMENT_QUERY_REPLY", _lastQueryReplyText ?? string.Empty);
        }

        private bool game_menu_sme_tournament_locate_on_condition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Manage;

            bool disableOption = _lastQueriedTournamentSettlements.Count <= 0;
            var disabledText = new TextObject("{=SME_ARENA_LOCATE_DISABLED}No known active tournaments to locate.");
            return MenuHelper.SetOptionProperties(args, canPlayerDo: true, disableOption, disabledText);
        }

        private void game_menu_sme_tournament_locate_on_consequence(MenuCallbackArgs args)
        {
            int tracked = 0;
            var tracker = Campaign.Current?.VisualTrackerManager;
            if (tracker != null)
            {
                foreach (var settlement in _lastQueriedTournamentSettlements)
                {
                    if (settlement == null)
                        continue;

                    if (!tracker.CheckTracked(settlement))
                    {
                        tracker.RegisterObject(settlement);
                        tracked++;
                    }
                }
            }

            var msg = new TextObject("{=SME_ARENA_LOCATED_MSG}Marked {COUNT} tournament towns on map.");
            msg.SetTextVariable("COUNT", tracked);
            InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Cyan));
        }

        private bool game_menu_sme_tournament_query_back_on_condition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Leave;
            return true;
        }

        private void game_menu_sme_tournament_query_back_on_consequence(MenuCallbackArgs args)
        {
            GameMenu.SwitchToMenu("town_arena");
        }

        private void OnDailyTickSettlement(Settlement settlement)
        {
            if (settlement?.Town == null || !settlement.IsTown)
                return;

            var settings = SettlementManagerSettings.Instance;
            if (settings != null && !settings.ModEnabled)
                return;

            Town town = settlement.Town;
            if (!_queuedSponsoredTournaments.TryGetValue(town, out int queued) || queued <= 0)
                return;

            if (!TryStartSponsoredTournament(town))
                return;

            _queuedSponsoredTournaments[town] = queued - 1;
            if (_queuedSponsoredTournaments[town] <= 0)
                _queuedSponsoredTournaments.Remove(town);

            if (settings == null || settings.ShowNotifications)
            {
                var msg = new TextObject("{=SME_ARENA_QUEUED_STARTED}Your queued sponsored tournament has now started in {TOWN}.");
                msg.SetTextVariable("TOWN", settlement.Name?.ToString() ?? "town");
                InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Yellow));
            }
        }

        private static bool TryStartSponsoredTournament(Town town)
        {
            if (town == null)
                return false;

            Campaign? campaign = Campaign.Current;
            if (campaign == null)
                return false;

            ITournamentManager manager = campaign.TournamentManager;
            if (manager == null)
                return false;

            if (manager.GetTournamentGame(town) != null)
                return false;

            TournamentGame game = campaign.Models.TournamentModel.CreateTournament(town);
            if (game == null)
                return false;

            manager.AddTournament(game);
            return true;
        }

        private static void BuildTournamentQueryResult(Town? currentTown, out TextObject replyText, out List<Settlement> targets)
        {
            targets = new List<Settlement>();

            Campaign? campaign = Campaign.Current;

            if (campaign == null || currentTown == null || Settlement.CurrentSettlement == null)
            {
                replyText = new TextObject("{=tGI135jv}Ah - I don't know of any right now. That's a bit unusual though. Must be the wars.[ib:closed]");
                return;
            }

            List<Town> source = Town.AllTowns
                .Where(x => campaign.TournamentManager.GetTournamentGame(x) != null && x != currentTown)
                .OrderBy(x => DistanceHelper.FindClosestDistanceFromSettlementToSettlement(Settlement.CurrentSettlement, x.Settlement, MobileParty.NavigationType.All))
                .ToList();

            if (source.Count > 1)
            {
                MBTextManager.SetTextVariable("CLOSEST_TOURNAMENT", source[0].Settlement.EncyclopediaLinkWithName);
                MBTextManager.SetTextVariable("NEXT_CLOSEST_TOURNAMENT", source[1].Settlement.EncyclopediaLinkWithName);
                replyText = new TextObject("{=pinSMuMe}Well, there's one starting up at {CLOSEST_TOURNAMENT}, then another at {NEXT_CLOSEST_TOURNAMENT}. You should probably be able to get to either of those, if you move quickly.[ib:hip]");

                targets.Add(source[0].Settlement);
                targets.Add(source[1].Settlement);
                return;
            }

            if (source.Count == 1)
            {
                MBTextManager.SetTextVariable("CLOSEST_TOURNAMENT", source[0].Settlement.EncyclopediaLinkWithName);
                replyText = new TextObject("{=2WnruiBw}I know of one starting up at {CLOSEST_TOURNAMENT}. You should be able to get there if you move quickly enough.");

                targets.Add(source[0].Settlement);
                return;
            }

            replyText = new TextObject("{=tGI135jv}Ah - I don't know of any right now. That's a bit unusual though. Must be the wars.[ib:closed]");
        }
    }
}
