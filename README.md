# Bannerlord.SettlementManagerEnhanced

A quality-of-life mod for Mount & Blade II: Bannerlord focused on **settlement construction funds** and **castle garrison management from prisons**.

**Target**: Bannerlord 1.2.12+ (e1.2 branch) and later.

## Features (all configurable via MCM)

### 1. Current Fund Transfer Range 0-1M (UI Patch)
- Vanilla hard-caps the gold transfer slider (into a town/castle's construction reserve / `BoostBuildingProcess`) at ~10,000 per transaction.
- This mod patches `SettlementConstructionVM` (via resilient reflection on Refresh) so the effective maximum becomes **1,000,000**.
- Required to make the daily fund scaling feature below actually useful in late game when you have millions to burn on rushing builds.

### 2. Daily Fund Scaling from Current Fund (the "500g = 50 speed" enhancement)
- Vanilla: the construction fund (see `settlement.Town.BoostBuildingProcess`) "costs" 500 denars/day to provide +50 build speed for towns (roughly 250/20 for castles). This is the flat behavior around `DefaultConstructionModel` + `BuildingHelper.BoostBuildingProcessWithGold`.
- **Our addition** (only on *player-owned* fortifications):
  - Every day, if current fund > 0:
    - **Additional cost**: 2% of current fund (4% if the settlement is a castle). This amount is **deducted directly from the fund reserve**.
    - **Additional build speed**: `additionalCost × 0.1` (town) or `additionalCost × 0.05` (castle). Progress is applied to the active construction project (or current default project) by converting points → `BuildingProgress += points / GetConstructionCost()`.
- This layers cleanly **on top of** the vanilla flat 500/50.
- Result: dumping a large reserve (now possible up to 1M thanks to the transfer patch) produces massive daily construction progress while slowly burning the reserve at a % rate. Perfect for late-game "just finish the damn castle already" moments.
- "Force Fund Scaling Pass Now" button in MCM for immediate testing.

### 3. Castle Garrison Auto Recruit from Prison (prison first, ignore NPCs)
- Every daily tick, for **player-owned castles**:
  - Scans prisoners in the **castle's prison first** (settlement-level jail / dungeon roster if discoverable via `Party` / reflection on `Town`; falls back to garrison prison), then the garrison party's own prison roster.
  - Uses vanilla `PrisonerRecruitmentModel.GetConformityNeededToRecruitPrisoner(...)` + the prison roster's XP field (conformity accumulator) — exactly the same pattern as TroopManagerEnhanced's prisoner recruitment.
  - Only recruits "stand-by" (conformity met) prisoners.
  - **Always ignores NPCs/heroes** (`troop.IsHero` skip).
  - Respects min tier, max-per-castle-per-day cap, and high-tier priority (all in MCM).
  - Moves directly via roster `AddToCounts` (prison → garrison member roster) and fires `OnTroopRecruited` for compatibility.
- "Force Castle Garrison Recruit Now" button available.
- This makes the "castle's auto recruit in game" actually pull from its own prison (and prison first).

All actions are optional, produce (optional) notifications, and run only on the daily settlement tick.

## Dependencies (load these **before** this mod)
- Bannerlord.Harmony
- Bannerlord.ButterLib (recommended)
- Bannerlord.UIExtenderEx (recommended)
- Bannerlord.MCM (Mod Configuration Menu) v5+

## Installation
1. Install the dependencies above.
2. Extract the `Bannerlord.SettlementManagerEnhanced` folder into `Modules/`.
3. Enable in Launcher (after the MCM/Harmony entries).
4. Start a campaign. Open **Mod Options** → "Settlement Manager Enhanced".

## Load Order (example)
1. Native
2. SandBoxCore
3. Sandbox
4. StoryMode (optional)
5. Bannerlord.Harmony
6. Bannerlord.ButterLib
7. Bannerlord.UIExtenderEx
8. Bannerlord.MBOptionScreen (MCM)
9. **Bannerlord.SettlementManagerEnhanced**
10. Everything else

## MCM Settings
Everything lives under **Settlement Manager Enhanced** in Mod Options (global JSON settings).

- Master enable + notifications
- Fund group: scaling toggle (the 2%/4% + efficiency), raise transfer limit toggle, force button
- Castle Garrison Recruit group: enable, min tier, max per castle/day, high tier priority, force button

## Building from Source (Unified Layout)
Same structure as Bannerlord.TroopManagerEnhanced:

```
dev/
├── build.ps1
├── module/
│   ├── SubModule.xml
│   └── ModuleData/Languages/...
└── src/
    └── Bannerlord.SettlementManagerEnhanced/
        ├── Bannerlord.SettlementManagerEnhanced.csproj
        └── *.cs
```

```powershell
pwsh ./dev/build.ps1 -Version v1.0.0
# or on Windows: .\dev\build.ps1 -Version v1.0.0
```

Outputs to `out/Bannerlord.SettlementManagerEnhanced/` and a zip.

Configure `GameFolder` (or `GAMEFOLDER` env var) in the csproj or on the command line for your Bannerlord install. The project references `SandBox.ViewModelCollection.dll` for the transfer UI patch.

## Development Notes
- Core logic split into managers (`FundManager`, `GarrisonRecruitManager`) + thin `SettlementManagementBehavior` (DailyTickSettlementEvent only).
- UI range patch lives in `SettlementConstructionPatches.cs` (reflection-resilient on the VM Refresh to support multiple game versions).
- Vanilla "500/50" logic identified via community docs + `Town.BoostBuildingProcess` + `BuildingHelper.BoostBuildingProcessWithGold` + `DefaultConstructionModel`.
- Prisoner logic deliberately mirrors TroopManagerEnhanced's `RecruitmentManager` (conformity, XP field, IsHero skip, roster ops).
- Player-owned only for the enhancements (fund scaling + castle auto-recruit) so AI behavior is untouched.
- Full multi-language scaffolding (EN primary + SC, with CN/CNs fallbacks).

## Credits
- TaleWorlds
- BUTR team (Harmony, MCM, etc.)
- Bannerlord modding community & https://docs.bannerlordmodding.com/ (and the fandom construction wiki page)
- The "Gold Rush Construction" idea space and vanilla numbers from the community

## License
Free to use, modify, and redistribute.

Happy building (literally)!
