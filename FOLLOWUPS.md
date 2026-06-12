# SettlementManagerEnhanced - Follow-ups & Roadmap

Created as the "settlement side" companion to Bannerlord.TroopManagerEnhanced. Kept deliberately focused on the three explicit user requests:
1. Fund transfer slider 0-10k → 0-1M.
2. Hook the vanilla 500/50 (and 250/20) fund cost/build logic and layer % of current fund additional cost + efficiency-based extra build speed.
3. Castle auto-recruit from its prison (prison first), ignoring NPC heroes in prison.

## Still Relevant Polish / Small Improvements
- Test the VM patch across game versions (the reflection fallback on Refresh is intentionally broad; add more candidate field names if a new patch of the game renames the internal `_transferableGoldMax` etc.).
- Consider exposing the burn rates / efficiencies as MCM sliders in a future "Advanced" group (currently per-spec hardcoded 2%/4% and 0.1/0.05).
- Improve prison roster discovery: if in your game version the settlement jail is exposed via a new public API (e.g. `SettlementPrisonComponent` or `town.PrisonRoster`), prefer the direct property over reflection and update the "prison first" ordering comment.
- Add a small per-settlement summary in the daily notif (e.g. aggregate "X settlements got extra build from funds today") instead of one message per settlement when many fiefs are owned.
- Optional: a lightweight "Construction Power" readout in notifs or a debug command.
- Keep the four language files in sync on string changes (EN authoritative).
- Build convenience: a small `build.sh` wrapper for users without pwsh, or document `dotnet build -p:GameFolder=...`.

## Out of Scope (by design for v1)
- Town (non-castle) auto garrison recruit from prison (user specifically asked for *castle's* auto recruit).
- Any change to AI-owned settlements (our enhancements are deliberately player-owned only).
- Overwriting / suppressing the vanilla flat 500/50 consumption (we only *add*).
- Per-save or per-settlement configuration (global MCM like the reference).
- UI buttons inside the settlement screen itself (MCM force buttons + daily tick are sufficient and keep scope small).
- Accelerated anything or hotkeys (kept consistent with the slimmed TroopManagerEnhanced philosophy).

## General Notes
- The source is intentionally lean (same 7-8 .cs files pattern as the reference after the scope reduction).
- When editing strings, update **all four** language folders.
- For builds on macOS/Linux: `pwsh ./dev/build.ps1 -Version x.y.z` after ensuring the GameFolder path points at a real Bannerlord install that has the required DLLs (including SandBox.ViewModelCollection.dll for the transfer patch).
- Run `dotnet build` (with GameFolder) from the project dir for fast iteration; the post-build target copies into a local `_Module` for quick launcher testing if you maintain one at mod root.

## Potential Future (only if users ask)
- Configurable % rates and efficiencies.
- "Also apply fund scaling to AI" as an explicit opt-in (with warnings).
- Integration with other settlement QoL (e.g. auto-assign projects, auto-collect tax, etc.) — would be a larger follow-up mod or expansion.

This mod was built to be a clean, standardized, drop-in style companion to TroopManagerEnhanced.
