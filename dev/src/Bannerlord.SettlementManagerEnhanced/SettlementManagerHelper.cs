using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace Bannerlord.SettlementManagerEnhanced
{
    /// <summary>
    /// Helper utilities for settlement management (player ownership checks, construction progress application).
    /// Kept small and focused like TroopManagerHelper.
    /// </summary>
    public static class SettlementManagerHelper
    {
        private const int MaxBuildingLevel = 3;

        /// <summary>
        /// True for player-owned towns and castles (the fiefs where fund transfer UI and our daily logic are relevant).
        /// </summary>
        public static bool IsPlayerFortification(Settlement settlement)
        {
            if (settlement == null || !settlement.IsFortification)
                return false;

            // PlayerClan is the reliable check for "my fiefs".
            var playerClan = Clan.PlayerClan;
            if (playerClan != null && settlement.OwnerClan == playerClan)
                return true;

            // Fallback for edge cases (e.g. during very early new game or special ownership).
            if (Hero.MainHero != null && settlement.Owner == Hero.MainHero)
                return true;

            return false;
        }

        /// <summary>
        /// Applies "build speed" (construction points) as progress to the settlement's current active building
        /// or the current default/daily project.
        ///
        /// Vanilla consumption of BoostBuildingProcess + awarding of points happens inside/around
        /// DefaultConstructionModel + settlement daily ticks / BuildingProgress updates.
        /// We mirror the effect here for our *additional* % based grant so the player sees immediate construction movement.
        /// </summary>
        public static void ApplyConstructionProgress(Town town, int points)
        {
            if (town == null || points <= 0)
                return;

            Building? building = town.CurrentBuilding;
            if (building == null)
                building = town.CurrentDefaultBuilding;

            if (building == null || building.CurrentLevel >= MaxBuildingLevel)
                return;

            int cost = building.GetConstructionCost();
            if (cost <= 0)
                cost = 1;

            // BuildingProgress is the 0..1 (per level) accumulator. Points / cost gives the fractional progress.
            float delta = points / (float)cost;
            building.BuildingProgress += delta;

            // Level up as many times as we crossed full levels (rare for one day but correct).
            while (building.BuildingProgress >= 1.0f && building.CurrentLevel < MaxBuildingLevel)
            {
                building.BuildingProgress -= 1.0f;
                building.CurrentLevel = Math.Min(MaxBuildingLevel, building.CurrentLevel + 1);
            }

            // Clamp in case of over-progress from very large % burns.
            if (building.BuildingProgress > 1.0f && building.CurrentLevel >= MaxBuildingLevel)
                building.BuildingProgress = 1.0f;
        }
    }
}
