# Changelog

All notable changes to SettlementManagerEnhanced will be documented in this file.
Format based on Keep a Changelog + SemVer.

## [Unreleased / 1.0.0] - Initial Release

### Added
- **Current fund transfer range raised to 0-1,000,000**: Resilient Harmony postfix on `SettlementConstructionVM.Refresh` (via reflection on common internal max fields/properties) so the settlement construction fund transfer slider accepts up to 1M in one go (vanilla ~10k).
- **Daily current fund scaling / extra build speed**:
  - Vanilla baseline (documented): 500 denars/day → +50 construction for towns; ~250 → +20 for castles (via `BoostBuildingProcess` consumption + `DefaultConstructionModel` / `BuildingHelper.BoostBuildingProcessWithGold`).
  - New (player-owned forts only): *additional* daily cost = current fund × 2% (town) / 4% (castle). This cost is deducted from `Town.BoostBuildingProcess`.
  - New: additional build speed granted = additionalCost × 0.1 (town) / 0.05 (castle). Progress applied directly to the active building's `BuildingProgress` (or `CurrentDefault`) with level-up handling.
  - Layers cleanly on top of vanilla flat behavior. "Force Fund Scaling Pass Now" MCM button.
- **Castle garrison auto-recruit from prison (prison first)**:
  - DailyTickSettlement for player-owned castles.
  - Scans castle's prison / jail roster first (via `Settlement.Party` / reflection on `Town.PrisonRoster`), then garrison prison roster.
  - Conformity check using `PrisonerRecruitmentModel` + prison roster XP (stand-by only) — mirrors TroopManagerEnhanced.
  - **Ignores all NPC heroes** (`IsHero`).
  - Respects MCM filters (min tier, max per castle/day, high-tier priority).
  - Roster move prison → garrison members + `OnTroopRecruited` event.
  - "Force Castle Garrison Recruit Now" button.
- Full MCM v5 global settings (json) with groups, hints, force toggles (modeled exactly on TroopManagerEnhanced).
- Complete localization scaffolding (EN primary, SC full, CN + CNs fallback) using `{=SME_...}` keys.
- Standardized project layout (dev/build.ps1 + module/ + src/ + unified copy targets), csproj with SandBox.ViewModelCollection reference, SubModule lifecycle + Harmony.PatchAll.
- `SettlementManagementBehavior` (thin daily settlement tick only), `FundManager`, `GarrisonRecruitManager`, `SettlementManagerHelper`, `SettlementConstructionPatches`.
- README, CHANGELOG, FOLLOWUPS matching the reference style.

### Notes on Vanilla Hooks Found
- `settlement.Town.BoostBuildingProcess` = the "current fund" / project reserve.
- Transfer UI + `BuildingHelper.BoostBuildingProcessWithGold(int, Town)`.
- Daily construction power and reserve consumption around `DefaultConstructionModel.CalculateDailyConstructionPower`.
- Prisoner conformity + roster XP exactly as used by `PrisonerRecruitmentModel` and party prison recruitment.

No per-save data, no heavy model replacement, maximum vanilla compatibility for roster ops and events.

[1.0.0]: Initial creation following TroopManagerEnhanced standardization.
