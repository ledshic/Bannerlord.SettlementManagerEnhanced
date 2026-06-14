using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.SettlementManagerEnhanced
{
    /// <summary>
    /// Manager for castle garrison auto-recruit from the castle dungeon.
    ///
    /// Per requirement:
    /// - Only for castles (settlement.IsCastle).
    /// - Only uses the settlement-level dungeon/prison roster.
    /// - Ignores vanilla obedience/conformity checks and recruits up to the configured daily amount.
    /// - Prisoners may include heroes; those are skipped so only troop stacks are moved.
    /// - Recruits into the castle's garrison MemberRoster.
    /// - Only affects player-owned castles.
    /// </summary>
    public class GarrisonRecruitManager
    {
        public int TryAutoRecruitForCastle(Settlement settlement, SettlementManagerSettings settings)
        {
            if (settlement == null || !settlement.IsCastle || settlement.Town == null)
                return 0;

            if (settings == null || !settings.ModEnabled || !settings.CastlePrisonAutoRecruitEnabled)
                return 0;

            if (!SettlementManagerHelper.IsPlayerFortification(settlement))
                return 0;

            var town = settlement.Town;
            var garrison = town.GarrisonParty;
            if (garrison == null || !garrison.IsActive || garrison.MemberRoster == null)
                return 0;

            try
            {
                int recruited = PerformRecruitmentInternal(settlement, garrison, settings);

                if (recruited > 0 && settings.ShowNotifications)
                {
                    var text = new TextObject("{=SME_GARR_001}Recruited {COUNT} prisoners from prison into {SETTLEMENT} garrison.", null);
                    text.SetTextVariable("COUNT", recruited);
                    text.SetTextVariable("SETTLEMENT", settlement.Name?.ToString() ?? "castle");
                    InformationManager.DisplayMessage(new InformationMessage(text.ToString(), Colors.Cyan));
                }

                return recruited;
            }
            catch (Exception ex)
            {
                Debug.Print($"[SettlementManagerEnhanced][GarrisonRecruit] Exception for {settlement.Name}: {ex}");
                return 0;
            }
        }

        private int PerformRecruitmentInternal(Settlement settlement, MobileParty garrison, SettlementManagerSettings settings)
        {
            var garrisonRoster = garrison.MemberRoster;
            var prisonRoster = GetCastleDungeonRoster(settlement);

            if (prisonRoster == null || prisonRoster.TotalManCount <= 0)
                return 0;

            int freeSlots = Math.Max(0, GetPartySizeLimit(garrison) - garrisonRoster.TotalManCount);
            if (freeSlots <= 0)
                return 0;

            int maxThisCheck = Math.Max(1, settings.MaxGarrisonRecruitsPerCastle);

            var candidates = new List<TroopRosterElement>();
            for (int i = 0; i < prisonRoster.Count; i++)
            {
                var element = prisonRoster.GetElementCopyAtIndex(i);
                var troop = element.Character as CharacterObject;

                if (troop == null || troop.IsHero || element.Number <= 0)
                    continue;

                candidates.Add(element);
            }

            if (candidates.Count == 0)
                return 0;

            int totalRecruited = 0;
            int remainingSlots = freeSlots;
            int remainingMax = maxThisCheck;

            foreach (var candidate in candidates)
            {
                if (remainingSlots <= 0 || remainingMax <= 0)
                    break;

                var troop = candidate.Character as CharacterObject;
                if (troop == null)
                    continue;

                int canRecruit = Math.Min(candidate.Number, Math.Min(remainingSlots, remainingMax));
                if (canRecruit <= 0)
                    continue;

                // Direct roster transfer from the castle dungeon to the garrison.
                prisonRoster.AddToCounts(troop, -canRecruit);
                garrisonRoster.AddToCounts(troop, canRecruit);

                try
                {
                    // Use the same event as party recruit for maximum compatibility (some perks / achievements listen to it)
                    CampaignEventDispatcher.Instance.OnTroopRecruited(
                        Hero.MainHero,
                        null,
                        null,
                        troop,
                        canRecruit);
                }
                catch { /* non-fatal */ }

                totalRecruited += canRecruit;
                remainingSlots -= canRecruit;
                remainingMax -= canRecruit;
            }

            return totalRecruited;
        }

        /// <summary>
        /// Returns the settlement-level dungeon/prison roster when discoverable.
        /// </summary>
        private TroopRoster? GetCastleDungeonRoster(Settlement settlement)
        {
            TroopRoster? dungeon = null;

            try
            {
                var partyProp = settlement.GetType().GetProperty("Party", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (partyProp != null)
                {
                    var partyBase = partyProp.GetValue(settlement) as PartyBase;
                    if (partyBase != null && partyBase.PrisonRoster != null)
                        dungeon = partyBase.PrisonRoster;
                }
            }
            catch { /* reflection safe */ }

            if (dungeon == null)
            {
                try
                {
                    var townPrisonProp = typeof(Town).GetProperty("PrisonRoster", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (townPrisonProp != null)
                        dungeon = townPrisonProp.GetValue(settlement.Town) as TroopRoster;
                }
                catch { }
            }

            return dungeon;
        }

        private int GetPartySizeLimit(MobileParty garrison)
        {
            string[] candidateNames = { "PartySizeLimit", "LimitedPartySize", "LimitPartySize" };

            foreach (var name in candidateNames)
            {
                var prop = garrison.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop != null && prop.PropertyType == typeof(int))
                    return (int)prop.GetValue(garrison);
            }

            var party = garrison.Party;
            if (party != null)
            {
                foreach (var name in candidateNames)
                {
                    var prop = party.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (prop != null && prop.PropertyType == typeof(int))
                        return (int)prop.GetValue(party);
                }
            }

            return int.MaxValue;
        }

    }
}
