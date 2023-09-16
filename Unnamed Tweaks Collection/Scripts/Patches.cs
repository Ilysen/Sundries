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
using XRL.World.Parts;
using XRL.World.Parts.Skill;
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
            if (!Options.GetOption("UnnamedTweaksCollection_EnableNoTakeTownWater").EqualsNoCase("Yes"))
                return;
            if (__instance.AutoCollectLiquidType != null || __result != "water")
                return;
            if (CheckpointingSystem.IsPlayerInCheckpoint())
                __result = null;
        }
    }

    [HarmonyPatch(typeof(EnergyCellSocket))]
    [HarmonyPatch("AttemptReplaceCell")]
    public static class UnnamedTweaksCollection_EnergyCellSocket
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            UnityEngine.Debug.Log("Running transpiler");
            var codes = new List<CodeInstruction>(instructions);
            var found = false;
            UnityEngine.Debug.Log($"Codes count: {codes.Count}");
            for (int i = 0; i < codes.Count; i++)
            {
                UnityEngine.Debug.Log($"{i}: {(codes[i].opcode != null ? codes[i].opcode.ToString() : "null")} - {(codes[i].operand != null ? codes[i].operand.ToString() : "null")} (found: {found})");
                if (codes[i].Calls(AccessTools.Method(typeof(Popup), nameof(Popup.ShowOptionList))))
                {
                    codes[i] = CodeInstruction.Call(typeof(UnnamedTweaksCollection_EnergyCellSocket), nameof(NewShowOptionList));
                    break;
                }
            }
            return codes.AsEnumerable();
        }

        // This place is a message...and part of a system of messages... pay attention to it!
        // Sending this message was important to us.We considered ourselves to be a powerful culture.
        // This place is not a place of honor... no highly esteemed deed is commemorated here... nothing valued is here.
        // What is here was dangerous and repulsive to us. This message is a warning about danger.
        // The danger is in a particular location...it increases towards a center... the center of danger is here... of a particular size and shape, and below us.
        // The danger is still present, in your time, as it was in ours.
        // The danger is to the body, and it can kill.
        // The form of the danger is an emanation of energy.
        // The danger is unleashed only if you substantially disturb this place physically. This place is best shunned and left uninhabited.
        private static int NewShowOptionList(string Title, IList<string> Options, IList<char> Hotkeys, int Spacing, string Intro, int MaxWidth, bool RespectOptionNewlines, bool AllowEscape, int DefaultSelected, string SpacingText, Action<int> onResult, GameObject context, IList<IRenderable> Icons, IRenderable IntroIcon, IList<QudMenuItem> Buttons, bool centerIntro, bool centerIntroIcon, int iconPosition, bool forceNewPopup)
        {
            // I hate this.
            return Popup.ShowOptionList(Title, Options, Hotkeys, Spacing, Intro, MaxWidth, RespectOptionNewlines, AllowEscape, XRL.UI.Options.GetOption("UnnamedTweaksCollection_EnableDefaultRemoveCell").EqualsNoCase("Yes") ? 0 : DefaultSelected, SpacingText, onResult, context, Icons, IntroIcon, Buttons, centerIntro, centerIntroIcon, iconPosition, forceNewPopup);
        }
    }

    [HarmonyPatch(typeof(Tinkering_Disassemble))]
    class UnnamedTweaksCollection_TinkeringDisassemble
    {
        static readonly Dictionary<Type, string> removalMessages = new Dictionary<Type, string>()
        {
            { typeof(ModJewelEncrusted), "pry off the jewels" },
            { typeof(ModEngraved), "buff out the engravings" },
            { typeof(ModPainted), "wash away the paint" },
            { typeof(ModSnailEncrusted), "pluck off the snails" },
            { typeof(ModScaled), "grind away the scales" },
            { typeof(ModFeathered), "pluck out the feathers" }
        };

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Tinkering_Disassemble.HandleEvent), new Type[] { typeof(OwnerGetInventoryActionsEvent) })]
        static void HandleEventGetInventoryActionsPatch(OwnerGetInventoryActionsEvent E, ref bool __result)
        {
            if (!Options.GetOption("UnnamedTweaksCollection_EnableItemModReset").EqualsNoCase("Yes"))
                return;
            if (!__result)
                return;
            if (E.Object.GetModificationCount() >= 1)
                E.AddAction("UnnamedTweaksCollection_RemoveAllMods", "remove all mods", "UnnamedTweaksCollection_RemoveAllMods", Key: 'O', FireOnActor: true);
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Tinkering_Disassemble.HandleEvent), new Type[] { typeof(InventoryActionEvent) })]
        static void HandleEventHandleInventoryActionsPatch(InventoryActionEvent E, ref bool __result)
        {
            // We don't need to include the Options.GetOption() call in here since the item context action already won't appear if the option is disabled
            if (!__result)
                return;
            if (E.Command == "UnnamedTweaksCollection_RemoveAllMods")
            {
                GameObject Target = E.Item.SplitFromStack();
                int ItemMods = Target.GetModificationCount();
                if (Popup.ShowYesNo($"Remove {ItemMods} mod{(ItemMods == 1 ? "" : "s")} from {Target.the} {Target.ShortDisplayName}? You will not get any materials back, and this cannot be undone.") != DialogResult.Yes)
                    return;
                List<string> removalDescriptors = new List<string>();
                foreach (IModification mod in Target.GetPartsDescendedFrom<IModification>())
                {
                    if (removalMessages.TryGetValue(mod.GetType(), out string value))
                        removalDescriptors.Add(value);
                    Helpers.RemoveModification(Target, mod);
                }
                string workMessage = "astutely undo the changes";
                if (removalDescriptors.Count > 0)
                {
                    if (removalDescriptors.Count < ItemMods)
                        removalDescriptors.Add(workMessage.Replace("the changes", "the remaining changes"));
                    workMessage = Grammar.MakeAndList(removalDescriptors.ToArray());
                }
                Popup.Show($"You {workMessage}, rendering {Target.the + Target.ShortDisplayName} {(Target.GetModificationCount() > 0 ? "mostly" : "perfectly")} unremarkable.");
            }
        }
    }
}
