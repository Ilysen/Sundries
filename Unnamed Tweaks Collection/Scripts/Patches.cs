using HarmonyLib;
using XRL;
using XRL.Language;
using XRL.UI;
using XRL.World;
using XRL.World.Skills.Cooking;

namespace UnnamedTweaksCollection.HarmonyPatches
{
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
            if (The.Player == null)
                return;
            if (!Options.GetOption("UnnamedTweaksCollection_EnableRenameCarbideChefRecipes").EqualsNoCase("Yes") || chef != The.Player.BaseDisplayName)
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
            if (!Options.GetOption("UnnamedTweaksCollection_EnableNoBonesTrashTakeAll").EqualsNoCase("Yes"))
                return;
            if (__result && __instance.HasTag("UnnamedTweaksCollection_NoAutoPickup"))
                __result = false;
        }
    }
}
