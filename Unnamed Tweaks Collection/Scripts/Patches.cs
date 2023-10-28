using Ava.UnnamedTweaksCollection.Scripts;
using ConsoleLib.Console;
using HarmonyLib;
using Qud.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using XRL;
using XRL.Language;
using XRL.UI;
using XRL.World;
using XRL.World.Capabilities;
using XRL.World.Effects;
using XRL.World.Parts;
using XRL.World.Parts.Mutation;
using XRL.World.Parts.Skill;
using XRL.World.Skills.Cooking;

namespace UnnamedTweaksCollection.HarmonyPatches
{
	[HarmonyPatch(typeof(EnergyStorage))]
	class Ava_UnnamedTweaksCollection_EnergyStorage
	{
		/// <summary>
		/// Designates completely charged energy cells as Max.
		/// Vanilla logic uses the word "Full" for any value above 75%, even if it isn't actually fully charged.
		/// </summary>
		[HarmonyPostfix]
		[HarmonyPatch(nameof(EnergyStorage.GetChargeStatus))]
		static void GetChargeStatusPatch(int Charge, int MaxCharge, string Style, ref string __result)
		{
			if (!Helpers.IsTweakEnabled(Tweaks.DifferentiateMaxCells))
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

	[HarmonyPatch(typeof(ScriptCallToArmsPart))]
	class Ava_UnnamedTweaksCollection_ScriptCallToArmsPart
	{
		/// <summary>
		/// Force-moves any characters with the <see cref="Ava_UnnamedTweaksCollection_BarathrumiteShelter"/> part to their safe location if they're not there when the Templar arrive.
		/// This is a failsafe to prevent pathfinding issues from potentially killing anyone who gets stuck upstairs.
		/// </summary>
		[HarmonyPostfix]
		[HarmonyPatch(nameof(ScriptCallToArmsPart.spawnParties))]
		static void SpawnPartiesPatch(ScriptCallToArmsPart __instance)
		{
			foreach (GameObject go in __instance.ParentObject.CurrentZone.FindObjectsWithPart(nameof(Ava_UnnamedTweaksCollection_BarathrumiteShelter)))
			{
				Ava_UnnamedTweaksCollection_BarathrumiteShelter bs = go.GetPart<Ava_UnnamedTweaksCollection_BarathrumiteShelter>();
				if (go.CurrentCell != bs.SafePlace)
					bs.TeleportToSafeSpot();
			}
		}
	}

	[HarmonyPatch(typeof(CookingRecipe))]
	class Ava_UnnamedTweaksCollection_CookingRecipe
	{
		/// <summary>
		/// Postfixes logic allowing the player to choose names for their own Carbide Chef recipes.
		/// </summary>
		[HarmonyPostfix]
		[HarmonyPatch(nameof(CookingRecipe.GenerateRecipeName))]
		static void GenerateRecipeNamePatch(string chef, ref string __result)
		{
			// Player might be null here if the game is still setting up, which can cause cookbooks on the starting map to fail to generate if we don't check for it
			if (The.Player == null || chef != The.Player.BaseDisplayName || !Helpers.IsTweakEnabled(Tweaks.NameCarbideChefRecipes))
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
	class Ava_UnnamedTweaksCollection_GameObject
	{
		/// <summary>
		/// Adds logic preventing items with the proper tag from being picked up by Take All.
		/// </summary>
		[HarmonyPostfix]
		[HarmonyPatch(nameof(GameObject.ShouldTakeAll))]
		static void ShouldTakeAllPatch(GameObject __instance, ref bool __result)
		{
			if (!__result || !Helpers.IsTweakEnabled(Tweaks.DontTakeAllJunk))
				return;
			if (__instance.HasTag("Ava_UnnamedTweaksCollection_NoTakeAll"))
				__result = false;
		}

		/// <summary>
		/// As <see cref="ShouldTakeAllPatch(GameObject, ref bool)"/>, but for autoget instead.
		/// </summary>
		[HarmonyPostfix]
		[HarmonyPatch(nameof(GameObject.CanAutoget))]
		static void CanAutogetPatch(GameObject __instance, ref bool __result)
		{
			if (!__result || Helpers.GetTweakSetting(Tweaks.DontTakeYurlsTreeItMakesThemSad) == "Never")
				return;
			__result = !Helpers.ShouldBlockFromAutogetAndDisassemble(__instance);
		}
	}

	[HarmonyPatch(typeof(LiquidVolume))]
	class Ava_UnnamedTweaksCollection_LiquidVolume
	{
		/// <summary>
		/// Prevents the auto-collection of fresh water in towns, even if the vanilla option to do so is enabled. It's just the considerate thing to do!~
		/// We check for towns by seeing if the player is in a screen that has a checkpoint. This isn't perfect, but it covers most of our bases.
		/// </summary>
		[HarmonyPostfix]
		[HarmonyPatch(nameof(LiquidVolume.GetActiveAutogetLiquid))]
		static void GetActiveAutogetLiquidPatch(LiquidVolume __instance, ref string __result)
		{
			if (__instance.AutoCollectLiquidType != null || __result != "water")
				return;
			if (!Helpers.IsTweakEnabled(Tweaks.DontTakeTownWater))
				return;
			if (CheckpointingSystem.IsPlayerInCheckpoint())
				__result = null;
		}
	}

	[HarmonyPatch(typeof(EnergyCellSocket))]
	public static class Ava_UnnamedTweaksCollection_EnergyCellSocket
	{
		/// <summary>
		/// Causes the "replace cell" menu to default to the option to remove a cell, like it was before version 204.98.
		/// </summary>
		[HarmonyTranspiler]
		[HarmonyPatch(nameof(EnergyCellSocket.AttemptReplaceCell))]
		public static IEnumerable<CodeInstruction> AttemptReplaceCellTranspiler(IEnumerable<CodeInstruction> instructions)
		{
			var codes = new List<CodeInstruction>(instructions);
			for (int i = 0; i < codes.Count; i++)
			{
				if (codes[i].Calls(AccessTools.Method(typeof(Popup), nameof(Popup.ShowOptionList))))
				{
					codes[i] = CodeInstruction.Call(typeof(Ava_UnnamedTweaksCollection_EnergyCellSocket), nameof(NewShowOptionList));
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
			return Popup.ShowOptionList(Title, Options, Hotkeys, Spacing, Intro, MaxWidth, RespectOptionNewlines, AllowEscape, Helpers.IsTweakEnabled(Tweaks.RemoveCellByDefault) ? 0 : DefaultSelected, SpacingText, onResult, context, Icons, IntroIcon, Buttons, centerIntro, centerIntroIcon, iconPosition, forceNewPopup);
		}
	}

	[HarmonyPatch(typeof(Psychometry))]
	public static class Ava_UnnamedTweaksCollection_Psychometry
	{
		/// <summary>
		/// Overrides the base activated ability for Psychometry to attempt to analyze every valid artifact in the user's inventory.
		/// This patch is slightly destructive, but the base ability does nothing except throw a popup, so at the very least it isn't throwing out too much of importance.
		/// </summary>
		[HarmonyPrefix]
		[HarmonyPatch(nameof(Psychometry.HandleEvent), new Type[] { typeof(CommandEvent) })]
		static void HandleEventPatch(CommandEvent E, Psychometry __instance)
		{
			if (!Helpers.IsTweakEnabled(Tweaks.EnableMassPsychometry))
				return;
			if (E.Command == "CommandPsychometryMenu")
			{
				GameObject ply = __instance.ParentObject;
				if (!ply.HasSkill("Tinkering"))
					Popup.Show("You can't perform mass psychometry without the Tinkering skill.");
				else
				{
					Dictionary<string, GameObject> validObjs = new Dictionary<string, GameObject>();
					foreach (GameObject go in ply.Inventory.GetObjects().Where(x => x.HasInventoryActionWithCommand("Psychometry") && x.GetEpistemicStatus() == 2))
					{
						if (!validObjs.ContainsKey(go.Blueprint))
							validObjs.Add(go.Blueprint, go);
					}
					if (validObjs.Count == 0)
						Popup.Show("You don't have any artifacts to perform mass psychometry on.");
					else if (Popup.ShowYesNo($"Perform mass psychometry on {validObjs.Count} artifact{(validObjs.Count == 1 ? "" : "s")}?") == DialogResult.Yes)
					{
						foreach (GameObject go in validObjs.Values)
							InventoryActionEvent.Check(ply, ply, go, "Psychometry");
					}
				}
				E.Command = "Ava_UnnamedTweaksCollection_PsychometryMenuResolved";
			}
		}
	}

	[HarmonyPatch(typeof(Tinkering_Disassemble))]
	public static class Ava_UnnamedTweaksCollection_TinkeringDisassemble
	{
		/// <summary>
		/// Overrides some vanilla logic to make tweaks treat certain item types as the same for determining scrap.
		/// </summary>
		[HarmonyPostfix]
		[HarmonyPatch("ToggleKey")]
		static void ToggleKeyPatch(GameObject obj, Tinkering_Disassemble __instance, ref string __result)
		{
			if (obj.GetBlueprint().InheritsFrom("RobotLimb") && Helpers.GetTweakSetting(Tweaks.TreatRobotLimbsAsScrap, out string setting) != "None")
			{
				bool shouldExempt = setting != "All" && obj.GetPart<Armor>()?.WornOn == "Face";
				if (!shouldExempt)
					__result = "RobotLimb";
			}
			else if (Helpers.IsTweakEnabled(Tweaks.TreatModdedItemsAsScrap))
			{
				MethodInfo dynMethod = typeof(Tinkering_Disassemble).GetMethod("ModProfile", BindingFlags.NonPublic | BindingFlags.Instance);
				if (dynMethod.Invoke(__instance, new object[] { obj }) is string s && !s.IsNullOrEmpty())
					__result = __result.Replace(s, "");
			}
		}

		/// <summary>
		/// Prevents decorations (metal folding chair, plastic tree) from being considered scrap based on the setting of <see cref="Tweaks.DontTakeYurlsTreeItMakesThemSad"/>.
		/// </summary>
		[HarmonyPostfix]
		[HarmonyPatch(nameof(Tinkering_Disassemble.ConsiderScrap))]
		static void ConsiderScrapPatch(GameObject obj, ref bool __result)
		{
			if (__result)
				__result = !Helpers.ShouldBlockFromAutogetAndDisassemble(obj);
		}
	}

	[HarmonyPatch(typeof(GeomagneticDisc))]
	public static class Ava_UnnamedTweaksCollection_GeomagneticDisc
	{
		/// <summary>
		/// Fully disables the animation for throwing a geomagnetic disc, which can become very slow on crowded screens.
		/// </summary>
		[HarmonyTranspiler]
		[HarmonyPatch("DoThrow")]
		public static IEnumerable<CodeInstruction> DoThrowTranspiler(IEnumerable<CodeInstruction> instructions)
		{
			// This transpiler effectively replaces this code:
			//   flag = Actor.CurrentZone.IsActive()
			// With this code:
			//   flag = Ava_UnnamedTweaksCollection_GeomagneticDisc.ShouldAllowAnimation(Actor)
			// The transpiler always overrides the new method, but that method returns a value equivalent to the old one if the tweak is disabled.

			var codes = new List<CodeInstruction>(instructions);
			for (int i = 0; i < codes.Count; i++)
			{
				if (codes[i].opcode == OpCodes.Ldarg_1 &&
					codes[i + 2].Calls(AccessTools.Method(typeof(Zone), nameof(Zone.IsActive))))
				{
					codes.RemoveRange(i, 3);
					codes.Insert(i, CodeInstruction.Call(typeof(Ava_UnnamedTweaksCollection_GeomagneticDisc), nameof(ShouldAllowAnimation)));
					codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_1));
					break;
				}
			}
			return codes.AsEnumerable();
		}

		private static bool ShouldAllowAnimation(GameObject Actor) => !Helpers.IsTweakEnabled(Tweaks.DisableGeomagneticDiscAnimation) && Actor.CurrentZone.IsActive();
	}

	[HarmonyPatch(typeof(Persuasion_Proselytize))]
	public static class Ava_UnnamedTweaksCollection_PersuasionProselytize
	{
		/// <summary>
		/// Causes failed proselytization attempts to break down and display the math that went into it.
		/// We have to recalculate the numbers here, so future updates might make this inaccurate.
		/// </summary>
		[HarmonyTranspiler]
		[HarmonyPatch(nameof(Persuasion_Proselytize.Proselytize))]
		public static IEnumerable<CodeInstruction> DoThrowTranspiler(IEnumerable<CodeInstruction> instructions)
		{
			var codes = new List<CodeInstruction>(instructions);
			for (int i = 0; i < codes.Count; i++)
			{
				if (codes[i].opcode == OpCodes.Ldstr && codes[i].operand is string s && s.Contains(" unconvinced by your pleas"))
				{
					codes[i] = CodeInstruction.Call(typeof(Ava_UnnamedTweaksCollection_PersuasionProselytize), nameof(AssembleText));
					codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_0));
					break;
				}
			}
			return codes.AsEnumerable();
		}

		/// <summary>
		/// Assembles 
		/// </summary>
		private static string AssembleText(MentalAttackEvent E)
		{
			string toReturn = " unconvinced by your pleas.";
			if (Helpers.IsTweakEnabled(Tweaks.ShowProselytizeMath))
			{
				var toAdd = new List<string>();
				Beguiled b = E.Defender.GetEffect<Beguiled>();
				int atkModifier = E.Attacker.StatMod("Ego", 0);
				int defModifier = (E.Defender.HasEffect<Proselytized>() ? 1 : 0) + (E.Defender.HasEffect<Rebuked>() ? 1 : 0) + (b != null ? b.LevelApplied : 0);
				int levelDifference = Math.Max(E.Defender.Stat("Level", 0) - E.Attacker.Stat("Level", 0), 0);

				var difficultyFactors = new List<string>();
				if (levelDifference != 0)
					difficultyFactors.Add($"{levelDifference} levels in difference");
				if (defModifier != 0)
					difficultyFactors.Add($"{defModifier} from existing deffects");
				if (difficultyFactors.Count > 0)
					difficultyFactors.Insert(0, $"{E.Difficulty - levelDifference - defModifier} base difficulty");

				toReturn += $" ({E.Dice}{(atkModifier != 0 ? $" + {atkModifier}" : null)} vs. {E.Difficulty}{(difficultyFactors.Count > 0 ? $"; {string.Join(" + ", difficultyFactors)}" : "")})";
				if (2 + atkModifier < E.Difficulty + defModifier)
					toReturn += "\n\n{{R|Impossible by " + ((E.Difficulty + defModifier) - (2 + atkModifier)) + "}}";
				else
				{
					int requiredRoll = 8 - ((2 + atkModifier) - (E.Difficulty + defModifier));
					toReturn += "\n\n{{G|Succeeds on a d8 roll of " + requiredRoll + (requiredRoll == 8 ? "" : " or higher") + "}}";
				}
			}
			return toReturn;
		}

	}
}
