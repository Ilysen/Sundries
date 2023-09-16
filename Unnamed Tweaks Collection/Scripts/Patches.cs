using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnnamedTweaksCollection.Scripts;
using XRL;
using XRL.Language;
using XRL.Messages;
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
            /*var found = false;
            UnityEngine.Debug.Log($"Codes count: {codes.Count}");
            for (int i = 0; i < codes.Count; i++)
            {
                UnityEngine.Debug.Log($"{i}: {(codes[i].opcode != null ? codes[i].opcode.ToString() : "null")} - {(codes[i].operand != null ? codes[i].operand.ToString() : "null")} (found: {found})");
                if (codes[i].opcode == OpCodes.Stloc_S && codes[i].operand is LocalBuilder lb)
                {
                    if (lb.LocalIndex == 7)
                    {
                        if (!found)
                        {
                            UnityEngine.Debug.Log($"Found the first occurrence at index {i}. Marking.");
                            found = true;
                        }
                        else
                        {
                            UnityEngine.Debug.Log($"Found the second occurrence at index {i}. Adding new code.");
                            codes.Insert(i + 1, new CodeInstruction(OpCodes.Ldloc_S, 7));
                            codes.Insert(i + 1, CodeInstruction.Call(typeof(UnnamedTweaksCollection_EnergyCellSocket), nameof(GetCellIndex)));
                            codes.Insert(i + 1, new CodeInstruction(OpCodes.Stloc_S, 7));
                            UnityEngine.Debug.Log("New calls successfully added. Backing out.");
                            break;
                        }
                    }
                }
            }*/

            return codes.AsEnumerable();
        }

        private static int GetCellIndex(int def)
        {
            Popup.Show("Getting cell index. Default is " + def);
            if (Options.GetOption("UnnamedTweaksCollection_EnableDefaultRemoveCell").EqualsNoCase("Yes"))
                return 0;
            return def;
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
