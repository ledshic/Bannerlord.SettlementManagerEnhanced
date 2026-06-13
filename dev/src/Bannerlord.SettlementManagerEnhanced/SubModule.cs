using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace Bannerlord.SettlementManagerEnhanced
{
    /// <summary>
    /// Main entry point for the SettlementManagerEnhanced mod.
    /// Follows standard Bannerlord MBSubModuleBase lifecycle, matching TroopManagerEnhanced standardization.
    /// </summary>
    public class SubModule : MBSubModuleBase
    {
        private const string HarmonyId = "Bannerlord.SettlementManagerEnhanced";

        private Harmony? _harmony;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            try
            {
                _harmony = new Harmony(HarmonyId);
                _harmony.PatchAll(Assembly.GetExecutingAssembly());

                Debug.Print($"[Bannerlord.SettlementManagerEnhanced] SubModule loaded. Harmony patches applied. v{typeof(SubModule).Assembly.GetName().Version}");
            }
            catch (Exception ex)
            {
                Debug.Print($"[SettlementManagerEnhanced] ERROR in OnSubModuleLoad: {ex}");
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=SME_INIT_FAIL}SettlementManagerEnhanced failed to initialize Harmony. Check logs.").ToString(),
                    Colors.Red));
            }
        }

        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();

            try
            {
                _harmony?.UnpatchAll(HarmonyId);
                _harmony = null;
            }
            catch (Exception ex)
            {
                Debug.Print($"[SettlementManagerEnhanced] ERROR in OnSubModuleUnloaded: {ex}");
            }
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarter)
        {
            base.OnGameStart(game, gameStarter);

            if (game.GameType is Campaign)
            {
                var campaignStarter = (CampaignGameStarter)gameStarter;

                campaignStarter.AddBehavior(new SettlementManagementBehavior());
                campaignStarter.AddBehavior(new ArenaTournamentMenuBehavior());

                Debug.Print("[Bannerlord.SettlementManagerEnhanced] SettlementManagementBehavior + ArenaTournamentMenuBehavior registered for campaign.");
            }
        }
    }
}
