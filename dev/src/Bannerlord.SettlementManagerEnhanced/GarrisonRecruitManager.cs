using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Manager for castle garrison auto-recruit from prison.
    ///
    /// Per requirement:
    /// - Only for castles (settlement.IsCastle).
    /// - Affects prisoners in "castle's prison" and "prison first".
    /// - Prisoners in prison may include NPCs (heroes); ignore them (IsHero skip, same as TroopManagerEnhanced RecruitmentManager).
    /// - Uses vanilla PrisonerRecruitmentModel for conformity + the XP field on prison roster (conformity accumulator).
    /// - Recruits into the castle's garrison MemberRoster (vanilla roster move).
    /// - Respects settings: min tier, max per day per castle, high-tier priority, only player owned.
    ///
    /// "Prison first": we probe for a settlement-level jail roster first (via Party or reflection on Town),
    /// then fall back to / also process the garrison party's own prison roster.
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
            var prisonSources = GetPrisonRostersInPriorityOrder(settlement, garrison);

            if (prisonSources.Count == 0)
                return 0;

            int freeSlots = Math.Max(0, GetPartySizeLimit(garrison) - garrisonRoster.TotalManCount);
            if (freeSlots <= 0)
                return 0;

            int minTier = Math.Max(0, settings.MinimumGarrisonPrisonerTier);
            int maxThisCheck = Math.Max(1, settings.MaxGarrisonRecruitsPerCastle);
            bool prioritizeHighTier = settings.PrioritizeHighTierGarrisonPrisoners;

            var recruitmentModel = Campaign.Current?.Models?.PrisonerRecruitmentCalculationModel;

            var candidates = new List<GarrisonPrisonerCandidate>();

            foreach (var prisonRoster in prisonSources)
            {
                if (prisonRoster == null || prisonRoster.TotalManCount <= 0)
                    continue;

                for (int i = 0; i < prisonRoster.Count; i++)
                {
                    TroopRosterElement element = prisonRoster.GetElementCopyAtIndex(i);
                    var troop = element.Character as CharacterObject;

                    if (troop == null || troop.IsHero)
                        continue;   // "prisoner in prison may have npcs, ignore them"

                    if (troop.Tier < minTier)
                        continue;

                    // Conformity check (stand-by) - same pattern as TroopManagerEnhanced
                    int currentConformity = prisonRoster.GetElementXp(i);
                    int neededPerOne = 100;
                    if (recruitmentModel != null)
                    {
                        neededPerOne = GetConformityNeededToRecruitPrisoner(recruitmentModel, garrison, troop);
                        if (neededPerOne <= 0) neededPerOne = 1;
                    }

                    int ready = currentConformity / neededPerOne;
                    if (ready <= 0)
                        continue;

                    candidates.Add(new GarrisonPrisonerCandidate
                    {
                        Troop = troop,
                        Count = element.Number,
                        Ready = ready,
                        Tier = troop.Tier,
                        PrisonRoster = prisonRoster,
                        OriginalIndex = i
                    });
                }
            }

            if (candidates.Count == 0)
                return 0;

            if (prioritizeHighTier)
                candidates = candidates.OrderByDescending(c => c.Tier).ToList();

            int totalRecruited = 0;
            int remainingSlots = freeSlots;
            int remainingMax = maxThisCheck;

            foreach (var candidate in candidates)
            {
                if (remainingSlots <= 0 || remainingMax <= 0)
                    break;

                int canRecruit = Math.Min(candidate.Count, Math.Min(candidate.Ready, remainingSlots));
                canRecruit = Math.Min(canRecruit, remainingMax);
                if (canRecruit <= 0)
                    continue;

                var troop = candidate.Troop;
                var sourcePrison = candidate.PrisonRoster;

                // Vanilla-style roster transfer (prison -> garrison members)
                sourcePrison.AddToCounts(troop, -canRecruit);
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
        /// Returns prison rosters in "prison first" order:
        /// 1. Settlement-level jail / dungeon roster (if discoverable via Party or Town reflection).
        /// 2. The garrison party's own prison roster.
        /// Duplicates are deduped.
        /// </summary>
        private List<TroopRoster> GetPrisonRostersInPriorityOrder(Settlement settlement, MobileParty garrison)
        {
            var result = new List<TroopRoster>();
            var seen = new HashSet<TroopRoster>();

            // Castle's dedicated prison (jail) first
            TroopRoster? jail = null;

            // Try Settlement.Party (PartyBase) if present in the version
            try
            {
                var partyProp = settlement.GetType().GetProperty("Party", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (partyProp != null)
                {
                    var partyBase = partyProp.GetValue(settlement) as PartyBase;
                    if (partyBase != null && partyBase.PrisonRoster != null)
                        jail = partyBase.PrisonRoster;
                }
            }
            catch { /* reflection safe */ }

            // Alternative: Town may expose a PrisonRoster in some builds
            if (jail == null)
            {
                try
                {
                    var townPrisonProp = typeof(Town).GetProperty("PrisonRoster", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (townPrisonProp != null)
                        jail = townPrisonProp.GetValue(settlement.Town) as TroopRoster;
                }
                catch { }
            }

            if (jail != null && seen.Add(jail))
                result.Add(jail);

            // Garrison prison (always relevant, processed after dedicated prison if both exist)
            if (garrison?.PrisonRoster != null && seen.Add(garrison.PrisonRoster))
                result.Add(garrison.PrisonRoster);

            return result;
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

        private int GetConformityNeededToRecruitPrisoner(object recruitmentModel, MobileParty garrison, CharacterObject troop)
        {
            var methods = recruitmentModel.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.Name == "GetConformityNeededToRecruitPrisoner");

            foreach (var method in methods)
            {
                var parameters = method.GetParameters();
                object[]? args = null;

                if (parameters.Length == 1 && parameters[0].ParameterType.IsInstanceOfType(troop))
                    args = new object[] { troop };
                else if (parameters.Length == 2 && parameters[0].ParameterType.IsInstanceOfType(garrison.Party) && parameters[1].ParameterType.IsInstanceOfType(troop))
                    args = new object[] { garrison.Party, troop };

                if (args == null)
                    continue;

                var value = method.Invoke(recruitmentModel, args);
                if (value is int needed)
                    return needed;
            }

            return 100;
        }

        private struct GarrisonPrisonerCandidate
        {
            public CharacterObject Troop;
            public int Count;
            public int Ready;
            public int Tier;
            public TroopRoster PrisonRoster;
            public int OriginalIndex;
        }
    }
}
