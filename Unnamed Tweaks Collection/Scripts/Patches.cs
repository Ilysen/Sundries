using ConsoleLib.Console;
using HarmonyLib;
using Qud.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using UnnamedTweaksCollection.Scripts;
using XRL;
using XRL.Language;
using XRL.UI;
using XRL.World;
using XRL.World.Capabilities;
using XRL.World.Parts;
using XRL.World.Skills.Cooking;

namespace UnnamedTweaksCollection.HarmonyPatches
{
    [HarmonyPatch(typeof(EnergyStorage))]
    class UnnamedTweaksCollection_EnergyStorage
    {
        /// <summary>
        /// Designates completely charged energy cells as Max.
        /// Vanilla logic uses the word "Full" for any value above 75%, even if it isn't actually fully charged.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(nameof(EnergyStorage.GetChargeStatus))]
        static void GetChargeStatusPatch(int Charge, int MaxCharge, string Style, ref string __result)
        {
            if (!Helpers.TweakEnabled(Tweaks.DifferentiateMaxCells))
                return;
            if (Charge == MaxCharge)
            {
                switch (Style)
                {
                    case "electrical":
                        __result = __result.Replace("Full", "Max");
                        break;
                }
            }
        }
    }

    [HarmonyPatch(typeof(CookingRecipe))]
    class UnnamedTweaksCollection_CookingRecipe
    {
        /// <summary>
        /// Postfixes logic allowing the player to choose names for their own Carbide Chef recipes.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(nameof(CookingRecipe.GenerateRecipeName))]
        static void GenerateRecipeNamePatch(string chef, ref string __result)
        {
            // Player might be null here if the game is still setting up, which can cause cookbooks on the starting map to fail to generate if we don't check for it
            if (The.Player == null || Helpers.TweakEnabled(Tweaks.NameCarbideChefRecipes) || chef != The.Player.BaseDisplayName)
                return;
            Start:
            string newName = Popup.AskString("Name your recipe? Enter nothing to choose randomly instead.");
            if (newName == string.Empty)
            {
                if (Popup.ShowYesNo("Name this recipe randomly?") == DialogResult.Yes)
                    return;
                goto Start;
            }
            if (newName.Length > 50)
                newName = newName.Substring(0, 50); // Enforce a reasonable character limit to prevent unforeseen shenanigans
            newName = "{{W|" + Grammar.MakeTitleCase(newName) + "}}";
            DialogResult confirmation = Popup.ShowYesNo($"This dish will be named {newName}. Is this what you want?");
            if (confirmation == DialogResult.No || confirmation == DialogResult.Cancel)
                goto Start;
            __result = newName;
        }
    }

    [HarmonyPatch(typeof(GameObject))]
    class UnnamedTweaksCollection_GameObject
    {
        /// <summary>
        /// Adds logic preventing items with the proper tag from being picked up by Take All.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(nameof(GameObject.ShouldTakeAll))]
        static void ShouldTakeAllPatch(GameObject __instance, ref bool __result)
        {
            if (!Helpers.TweakEnabled(Tweaks.DontTakeAllJunk))
                return;
            if (__result && __instance.HasTag("UnnamedTweaksCollection_NoAutoPickup"))
                __result = false;
        }
    }

    [HarmonyPatch(typeof(LiquidVolume))]
    class UnnamedTweaksCollection_LiquidVolume
    {
        /// <summary>
        /// Prevents the auto-collection of fresh water in towns, even if the option is enabled. It's just the considerate thing to do!~
        /// We check for towns by seeing if the player is in a screen that has a checkpoint. This isn't perfect, but it covers most of our bases.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(nameof(LiquidVolume.GetActiveAutogetLiquid))]
        static void GetActiveAutogetLiquidPatch(LiquidVolume __instance, ref string __result)
        {
            if (!Helpers.TweakEnabled(Tweaks.DontTakeTownWater))
                return;
            if (__instance.AutoCollectLiquidType != null || __result != "water")
                return;
            if (CheckpointingSystem.IsPlayerInCheckpoint())
                __result = null;
        }
    }

    [HarmonyPatch(typeof(EnergyCellSocket), nameof(EnergyCellSocket.AttemptReplaceCell))]
    public static class UnnamedTweaksCollection_EnergyCellSocket
    {
        /// <summary>
        /// Causes the "replace cell" menu to default to the option to remove a cell, like it was before version 204.98.
        /// </summary>
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> AttemptReplaceCellTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(AccessTools.Method(typeof(Popup), nameof(Popup.ShowOptionList))))
                {
                    codes[i] = CodeInstruction.Call(typeof(UnnamedTweaksCollection_EnergyCellSocket), nameof(NewShowOptionList));
                    break;
                }
            }
            return codes.AsEnumerable();
        }

        // This place is a message... and part of a system of messages... pay attention to it!
        // Sending this message was important to us.We considered ourselves to be a powerful culture.
        // This place is not a place of honor... no highly esteemed deed is commemorated here... nothing valued is here.
        // What is here was dangerous and repulsive to us. This message is a warning about danger.
        // The danger is in a particular location... it increases towards a center... the center of danger is here... of a particular size and shape, and below us.
        // The danger is still present, in your time, as it was in ours.
        // The danger is to the body, and it can kill.
        // The form of the danger is an emanation of energy.
        // The danger is unleashed only if you substantially disturb this place physically. This place is best shunned and left uninhabited.
        private static int NewShowOptionList(string Title, IList<string> Options, IList<char> Hotkeys, int Spacing, string Intro, int MaxWidth, bool RespectOptionNewlines, bool AllowEscape, int DefaultSelected, string SpacingText, Action<int> onResult, GameObject context, IList<IRenderable> Icons, IRenderable IntroIcon, IList<QudMenuItem> Buttons, bool centerIntro, bool centerIntroIcon, int iconPosition, bool forceNewPopup)
        {
            return Popup.ShowOptionList(Title, Options, Hotkeys, Spacing, Intro, MaxWidth, RespectOptionNewlines, AllowEscape, Helpers.TweakEnabled(Tweaks.RemoveCellByDefault) ? 0 : DefaultSelected, SpacingText, onResult, context, Icons, IntroIcon, Buttons, centerIntro, centerIntroIcon, iconPosition, forceNewPopup);
        }
    }
}
