using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using XRL;
using XRL.Language;
using XRL.Messages;
using XRL.Rules;
using XRL.UI;
using XRL.World;
using XRL.World.Capabilities;
using XRL.World.Effects;
using XRL.World.Parts;
using XRL.World.Parts.Mutation;
using XRL.World.Parts.Skill;
using XRL.World.Skills.Cooking;
using XRL.World.Tinkering;
using XRL.World.ZoneParts;

namespace Ceres.Sundries.Scripts.Patches
{
	/// <summary>
	/// <para>This is the master class containing all of the requisite Harmony patches to enable the various tweaks in Sundries.
	/// Everything's organized into regions for convenience of easy reference, and has been compartmentalized as much as possible.</para>
	/// 
	/// <para>Item hauling logic can be found in <c>ItemHauling.cs</c>.</para>
	/// <para>Logic for the 'Show math on failed Proselytize atempts' feature is contained in its own class -- see <c><see cref="PersuasionProselytizePatch"/></c> for that.</para>
	/// </summary>
	[HarmonyPatch]
	class HarmonyPatches
	{
		#region Block auto-collection of decorations
		/// <summary>
		/// As <c><see cref="ShouldTakeAllPatch(GameObject, ref bool)"/></c>, but for autoget instead.
		/// </summary>
		[HarmonyPostfix]
		[HarmonyPatch(typeof(GameObject), nameof(GameObject.CanAutoget))]
		static void GameObject_CanAutogetPatch(GameObject __instance, ref bool __result)
		{
			if (!__result || Helpers.GetTweakSetting(Tweaks.DontTakeYurlsTreeItMakesThemSad) == "Never")
				return;
			__result = !Helpers.ShouldBlockFromAutogetAndDisassemble(__instance);
		}

		/// <summary>
		/// Prevents decorations (metal folding chair, plastic tree) from being considered scrap based on the setting of <c><see cref="Tweaks.DontTakeYurlsTreeItMakesThemSad"/></c>.
		/// </summary>
		[HarmonyPostfix]
		[HarmonyPatch(typeof(Tinkering_Disassemble), nameof(Tinkering_Disassemble.ConsiderScrap))]
		static void Tinkering_Disassemble_ConsiderScrapPatch(GameObject obj, ref bool __result)
		{
			if (__result)
				__result = !Helpers.ShouldBlockFromAutogetAndDisassemble(obj);
		}
		#endregion

		#region Block auto-digging in towns
		// This is a WIP -- the code for autoexploring is very convoluted and patching it is a heck of a headache
		/*[HarmonyPrefix]
		[HarmonyPatch(typeof(AutoAct), nameof(AutoAct.TryToMove))]
		[HarmonyPatch(new Type[] { typeof(GameObject), typeof(Cell), typeof(GameObject), typeof(Cell), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(bool) })]
		static void Autoact_TryToMovePatch(GameObject Actor, ref bool AllowDigging)
		{
			if (Actor.IsPlayer() && Helpers.IsTweakEnabled(Tweaks.DontAutodigInTowns) && CheckpointingSystem.IsPlayerInCheckpoint())
				AllowDigging = false;
		}*/
		#endregion

		#region Block auto-collection of fresh water in towns
		/// <summary>
		/// Prevents the auto-collection of fresh water in towns, even if the vanilla option to do so is enabled. It's just the considerate thing to do!~
		/// We check for towns by seeing if the player is in a screen that has a checkpoint. This isn't perfect, but it covers most of our bases.
		/// </summary>
		[HarmonyPostfix]
		[HarmonyPatch(typeof(LiquidVolume), nameof(LiquidVolume.GetActiveAutogetLiquid))]
		static void LiquidVolume_GetActiveAutogetLiquidPatch(LiquidVolume __instance, ref string __result)
		{
			if (!Helpers.IsTweakEnabled(Tweaks.DontTakeTownWater) || __instance.AutoCollectLiquidType != null || __result != "water")
				return;
			if (CheckpointingSystem.IsPlayerInCheckpoint())
				__result = null;
		}
		#endregion

		#region Block junk from Take All
		/// <summary>
		/// Adds logic preventing items with the proper tag from being picked up by Take All.
		/// </summary>
		[HarmonyPostfix]
		[HarmonyPatch(typeof(GameObject), nameof(GameObject.ShouldTakeAll))]
		static void GameObject_ShouldTakeAllPatch(GameObject __instance, ref bool __result)
		{
			if (!__result || !Helpers.IsTweakEnabled(Tweaks.DontTakeAllJunk))
				return;
			if (__instance.HasTag("Ceres_Sundries_NoTakeAll"))
				__result = false;
		}
		#endregion

		#region Disable item naming
		[HarmonyPrefix]
		[HarmonyPatch(typeof(ItemNaming), nameof(ItemNaming.Opportunity))]
		static bool ItemNaming_OpportunityPatch()
		{
			if (Helpers.IsTweakEnabled(Tweaks.DisableItemNaming))
				return false;
			return true;
		}
		#endregion

		#region Fully-charged energy cells appear as Max
		/// <summary>
		/// Designates completely charged energy cells as Max.
		/// Vanilla logic uses the word "Full" for any value above 75%, even if it isn't actually fully charged.
		/// </summary>
		[HarmonyPostfix]
		[HarmonyPatch(typeof(EnergyStorage), nameof(EnergyStorage.GetChargeStatus))]
		static void EnergyStorage_GetChargeStatusPatch(int Charge, int MaxCharge, string Style, ref string __result)
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
		#endregion

		#region Treat all robot limbs as scrap + 'Treat as scrap' ignores tinkering mods
		/// <summary>
		/// Overrides some vanilla logic to make tweaks treat certain item types as the same for determining scrap.
		/// Treating objects as scrap references a unique key that's built from its tinkering blueprint plus the names of its mods
		/// "ScrapToggle_Step Sowers" would be used for unmodded step sowers, and 
		/// "ScrapToggle_Carbine+MasterworkScoped" would appear for the Sparbine (I think lmao).
		/// 
		/// This method works by adjusting the returned key depending on enabled tweaks:
		/// * All robot limbs will use the same unified key, "ScrapToggle_RobotLimb", with an optional exclusion for faces;
		/// * Item mods will be truncated from the end of the key, meaning that the Sparbine, for instance, would still be lumped under "ScrapToggle_Carbine".
		/// </summary>
		[HarmonyPostfix]
		[HarmonyPatch(typeof(Tinkering_Disassemble), "ToggleKey")]
		static void Tinkering_Disassemble_ToggleKeyPatch(GameObject obj, Tinkering_Disassemble __instance, ref string __result)
		{
			if (obj.GetBlueprint().InheritsFrom("RobotLimb") && Helpers.GetTweakSetting(Tweaks.TreatRobotLimbsAsScrap, out string setting) != "None")
			{
				bool shouldExempt = setting != "All" && obj.GetPart<Armor>()?.WornOn == "Face";
				if (!shouldExempt)
					__result = "ScrapToggle_RobotLimb";
			}
			else if (Helpers.IsTweakEnabled(Tweaks.TreatModdedItemsAsScrap))
			{
				var modProfile = Tinkering_Disassemble.ModProfile(obj);
				if (!modProfile.IsNullOrEmpty())
					__result = __result.Replace(modProfile, "");
			}
		}
		#endregion

		#region Feature: Allow naming of Carbide Chef recipes
		/// <summary>
		/// Postfixes logic allowing the player to choose names for their own Carbide Chef recipes.
		/// </summary>
		[HarmonyPostfix]
		[HarmonyPatch(typeof(CookingRecipe), nameof(CookingRecipe.FromIngredients))]
		[HarmonyPatch(new Type[] { typeof(List<GameObject>), typeof(ProceduralCookingEffect), typeof(string), typeof(string) })]
		static void CookingRecipe_GenerateRecipeNamePatch(ref CookingRecipe __result)
		{
			// Player might be null here if the game is still setting up, which can cause cookbooks on the starting map to fail to generate if we don't check for it
			if (The.Player == null || __result.ChefName != The.Player.BaseDisplayName || !Helpers.IsTweakEnabled(Tweaks.NameCarbideChefRecipes))
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
			UnityEngine.Debug.Log(__result.DisplayName);
			__result.ChefName = null;
			__result.DisplayName = newName;
			// The cached display name is private, so we need to use reflection to clear it and ensure that the new name shows correctly
			FieldInfo pi = __result.GetType().GetField("CachedDisplayName", BindingFlags.NonPublic | BindingFlags.Instance);
			pi.SetValue(__result, null);
		}
		#endregion

		#region Feature: Barathrumites shelter when needed
		/// <summary>
		/// Force-moves any characters with the <c><see cref="Ceres_Sundries_BarathrumiteShelter"/></c> part to their safe location if they're not there when the Templar arrive.
		/// This is a failsafe to prevent pathfinding issues from potentially killing anyone who gets stuck upstairs.
		/// </summary>
		[HarmonyPostfix]
		[HarmonyPatch(typeof(ScriptCallToArms), nameof(ScriptCallToArms.spawnParties))]
		static void ScriptCallToArms_SpawnPartiesPatch(ScriptCallToArms __instance)
		{
			foreach (GameObject go in __instance.ParentZone.FindObjectsWithPart(nameof(Ceres_Sundries_BarathrumiteShelter)))
			{
				Ceres_Sundries_BarathrumiteShelter bs = go.GetPart<Ceres_Sundries_BarathrumiteShelter>();
				if (go.CurrentCell != bs.SafePlace)
					bs.TeleportToSafeSpot();
			}
		}
		#endregion

		#region Feature: Mass psychometry
		/// <summary>
		/// Overrides the base activated ability for Psychometry to attempt to analyze every valid artifact in the user's inventory.
		/// This patch is slightly destructive, but the base ability does nothing except throw a popup, so at the very least it isn't throwing out too much of importance.
		/// </summary>
		[HarmonyPrefix]
		[HarmonyPatch(typeof(Psychometry), nameof(Psychometry.HandleEvent), new Type[] { typeof(CommandEvent) })]
		static void Psychomentry_HandleEventPatch(CommandEvent E, Psychometry __instance)
		{
			if (!Helpers.IsTweakEnabled(Tweaks.EnableMassPsychometry))
				return;
			if (E.Command == Psychometry.MENU_COMMAND_NAME)
			{
				GameObject ply = __instance.ParentObject;
				if (!ply.HasPart<Tinkering>())
					Popup.Show("You can't perform mass psychometry without the Tinkering skill.");
				else
				{
					Dictionary<string, GameObject> validObjs = new();
					List<GameObject> learned = new();
					List<GameObject> tooComplex = new();
					foreach (GameObject go in ply.Inventory.GetObjects().Where(x => x.HasInventoryActionWithCommand(Psychometry.COMMAND_NAME) && x.GetEpistemicStatus() == 2))
					{
						if (!validObjs.ContainsKey(go.Blueprint))
							validObjs.Add(go.Blueprint, go);
					}
					if (validObjs.Count == 0)
						Popup.Show("You don't have any artifacts to perform mass psychometry on.");
					else if (Popup.ShowYesNo($"Perform mass psychometry on {validObjs.Count} artifact{(validObjs.Count == 1 ? "" : "s")}?") == DialogResult.Yes)
					{
						E.Actor.PlayWorldSound("Sounds/Abilities/sfx_ability_mutation_psychometry_activate", 0.5f);
						E.Actor.PlayWorldOrUISound("sfx_characterMod_tinkerSchematic_learn");
						foreach (GameObject go in validObjs.Values)
						{
							Examiner e = go.GetPart<Examiner>();
							TinkerItem t = go.GetPart<TinkerItem>();
							if (e?.Complexity > __instance.GetLearnableComplexity())
								tooComplex.Add(go);
							else
							{
								string text = t?.ActiveBlueprint ?? go.Blueprint;
								__instance.LearnedBlueprints.Add(text);
								foreach (TinkerData tinkerData in TinkerData.TinkerRecipes)
								{
									if (tinkerData.Blueprint == text)
									{
										GameObject gameObject = GameObject.CreateSample(tinkerData.Blueprint);
										gameObject.MakeUnderstood(false);
										try
										{
											tinkerData.DisplayName = gameObject.DisplayNameOnlyDirect;
											TinkerData.KnownRecipes.Add(tinkerData);
											learned.Add(go);
										}
										finally
										{
											gameObject.Obliterate(null, false, null);
										}
									}
								}
							}
						}

						string learnedText = string.Empty;
						foreach (GameObject go in learned)
							learnedText += go.BaseDisplayName + "\n";

						string tooComplexText = string.Empty;
						foreach (GameObject go in tooComplex)
							tooComplexText += go.BaseDisplayName + "\n";

						// gross hack to include an extra line of whitespace if needed
						if (learnedText != string.Empty && tooComplexText != string.Empty)
							learnedText = $"{learnedText}\n";

						Popup.Show($"Performed mass psychometry. {(learned.Count > 0 ? $"Learned {learned.Count} new recipe{(learned.Count > 1 ? "s" : "")}:\n\n" : string.Empty)}{learnedText}" +
							$"{(tooComplex.Count > 0 ? $"{tooComplex.Count} recipe{(tooComplex.Count > 1 ? "s were" : " was")} too complex:\n\n{tooComplexText}" : string.Empty)}");
					}
				}
				E.Command = "Ceres_Sundries_PsychometryMenuResolved";
			}
		}
		#endregion
	}

	// There is a dilapidated sign here. Ancient runes scrawl across the dilapidated surface
	// joined by the faded remains of threatening pictograms and ominous warning symbols:
	//
	// TRANSPILERS BEYOND THIS POINT
	#region Show math on failed recruitment attempts
	/// <summary>
	/// This method, <c><see cref="BeguilingPatch"/></c>, and <c><see cref="RebukeRobotPatch"/></c>, and <c><see cref="LoveTonicApplicatorPatch"/></c>
	/// are all largely copies of one another, with some varying logic differences for their specialized handling.
	/// </summary>
	#region Proselytize patch
	[HarmonyPatch(typeof(Persuasion_Proselytize))]
	class ProselytizePatch
	{
		/// <summary>
		/// This value should always be exactly equal to the text shown at the end of a failed proselytize.
		/// To make sure this is correct, use a decompiler and check <c><see cref="Persuasion_Proselytize.Proselytize(MentalAttackEvent)"/></c>.
		/// </summary>
		public static readonly string UNCONVINCED_TEXT = " unconvinced by your pleas.";

		/// <summary>
		/// Causes failed proselytization attempts to break down and display the math that went into it.
		/// We have to recalculate the numbers here, so future updates might make this inaccurate.
		/// </summary>
		[HarmonyTranspiler]
		[HarmonyPatch(nameof(Persuasion_Proselytize.Proselytize))]
		public static IEnumerable<CodeInstruction> Persuasion_ProselytizeTranspiler(IEnumerable<CodeInstruction> instructions)
		{
			var codes = new List<CodeInstruction>(instructions);
			for (int i = 0; i < codes.Count; i++)
			{
				/*
					This transpiler effectively replaces this code:

						return (E.Penetrations > 0 && defender.ApplyEffect(new Proselytized(E.Attacker))) || E.Attacker.Fail(defender.Does("are") + " unconvinced by your pleas.");

					With this code:

						return (E.Penetrations > 0 && defender.ApplyEffect(new Proselytized(E.Attacker))) || E.Attacker.Fail(defender.Does("are") + AssembleText(E));
				  
					That isn't what the exact method looks like in the decompiled code, but it's close to it -- default arguments have been trimmed for readability here.
					The transpiler always runs, but the relevant method returns a value equivalent to the old one if the tweak is disabled.
				 */

				if (codes[i].opcode == OpCodes.Ldstr && codes[i].operand is string s && s.Contains(UNCONVINCED_TEXT))
				{
					codes[i] = CodeInstruction.Call(typeof(ProselytizePatch), nameof(AssembleText));
					codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_0));
					break;
				}
			}
			return codes.AsEnumerable();
		}

		private static string AssembleText(MentalAttackEvent E)
		{
			string toReturn = UNCONVINCED_TEXT;
			if (Helpers.IsTweakEnabled(Tweaks.ShowProselytizeMath))
			{
				string formulaText = $" ({Helpers.GetMentalAttackFormulaVisualization(E, -6)})";

				int atkModifier = E.Attacker.StatMod("Ego", 0);
				Beguiled b = E.Defender.GetEffect<Beguiled>();
				int defModifier = (E.Defender.HasEffect<Proselytized>() ? 1 : 0) + (E.Defender.HasEffect<Rebuked>() ? 1 : 0) + (b != null ? b.LevelApplied : 0);

				if (2 + atkModifier < E.Difficulty + defModifier)
					formulaText += "\n\n{{R|Impossible by " + ((E.Difficulty + defModifier) - (2 + atkModifier)) + "}}";
				else
				{
					int requiredRoll = 8 - ((2 + atkModifier) - (E.Difficulty + defModifier));
					formulaText += "\n\n{{G|Succeeds on a d8 roll of " + requiredRoll + (requiredRoll == 8 ? "" : " or higher") + "}}";
				}
				toReturn += formulaText;
			}
			return toReturn;
		}
	}
	#endregion

	/// <summary>
	/// <para>As <c><see cref="ProselytizePatch"/></c>, but for Rebuke Robot. See there for further documentation; this method is largely a copy.</para>
	/// <para>The primary difference is that Rebuke Robot requires a specific number of penetrations, so we need to do a gross hack to calculate an average
	/// to display it for mechanical clarity.</para>
	/// </summary>
	#region Rebuke Robot patch
	[HarmonyPatch(typeof(Persuasion_RebukeRobot))]
	class RebukeRobotPatch
	{
		public static readonly string ZERO_PEN_TEXT = "Your argument does not compute.";

		public static readonly string INSUFFICIENT_PEN_TEXT = "away disinterestedly";

		[HarmonyTranspiler]
		[HarmonyPatch(nameof(Persuasion_RebukeRobot.Rebuke))]
		[HarmonyPatch(new Type[] { typeof(MentalAttackEvent) })]
		public static IEnumerable<CodeInstruction> Persuasion_RebukeRobotTranspiler(IEnumerable<CodeInstruction> instructions)
		{
			var codes = new List<CodeInstruction>(instructions);
			for (int i = 0; i < codes.Count; i++)
			{

				if (codes[i].opcode == OpCodes.Ldstr && codes[i].operand is string s && (s.Contains(ZERO_PEN_TEXT) || s.Contains(INSUFFICIENT_PEN_TEXT)))
				{
					codes[i] = CodeInstruction.Call(typeof(RebukeRobotPatch), nameof(AssembleText));
					codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_1));
				}
			}
			return codes.AsEnumerable();
		}

		private static string AssembleText(MentalAttackEvent E)
		{
			string toReturn = E.Penetrations == 0 ? ZERO_PEN_TEXT : INSUFFICIENT_PEN_TEXT;
			if (Helpers.IsTweakEnabled(Tweaks.ShowProselytizeMath))
			{
				// """Calculate""" an average by quickly simulating 100 mental attacks and computing the mean
				// This sucks. Nobody should allow me near a keyboard.
				List<int> simulatedPens = new();
				for (int i = 0; i < 100; i++)
					simulatedPens.Add(Stat.RollDamagePenetrations(E.Difficulty, E.Modifier, E.Modifier));

				MessageQueue.AddPlayerMessage($"Average pens: {Math.Round(simulatedPens.Average(), 3)}");
				string formulaText = Helpers.GetMentalAttackFormulaVisualization(E, LevelRatio: 0.8f);
				formulaText += ". Succeeds on {{rules|3 penetrations}}; current average: ~{{rules|" + Math.Round(simulatedPens.Average(), 3) + "}}";
				toReturn += $" ({formulaText})";
			}
			return toReturn;
		}
	}
	#endregion

	/// <summary>
	/// As <c><see cref="ProselytizePatch"/></c>, but for Beguiling. See there for further documentation; this method is largely a copy.
	/// </summary>
	#region Beguile patch
	[HarmonyPatch(typeof(Beguiling))]
	class BeguilingPatch
	{
		public static readonly string STRING_END_TEXT = ".";

		[HarmonyTranspiler]
		[HarmonyPatch("Beguile")] // Beguiling.Beguile is private, so we need to define the string manually here
		public static IEnumerable<CodeInstruction> Beguiling_BeguileTranspiler(IEnumerable<CodeInstruction> instructions)
		{
			var codes = new List<CodeInstruction>(instructions);
			for (int i = 0; i < codes.Count; i++)
			{
				if (codes[i].opcode == OpCodes.Ldstr && codes[i].operand is string s && s.Contains(STRING_END_TEXT))
				{
					codes[i] = CodeInstruction.Call(typeof(BeguilingPatch), nameof(AssembleText));
					codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_1));
				}
			}
			return codes.AsEnumerable();
		}

		private static string AssembleText(MentalAttackEvent E)
		{
			string toReturn = STRING_END_TEXT;
			if (Helpers.IsTweakEnabled(Tweaks.ShowProselytizeMath))
				toReturn += $" ({Helpers.GetMentalAttackFormulaVisualization(E, E.Defender.HasEffect<Proselytized>() || E.Defender.HasEffect<Rebuked>() ? -1 : 0)})";
			return toReturn;
		}
	}
	#endregion

	/// <summary>
	/// As <c><see cref="ProselytizePatch"/></c>, but for love injectors. Unlike the other ones, this one has its own logic, mostly!
	/// </summary>
	#region Love injector patch
	[HarmonyPatch(typeof(LoveTonicApplicator))]
	class LoveTonicApplicatorPatch
	{
		public static readonly string STRING_END_TEXT = " the love tonic with no effect.";

		[HarmonyTranspiler]
		[HarmonyPatch(nameof(LoveTonicApplicator.FireEvent))]
		public static IEnumerable<CodeInstruction> LoveTonicApplicator_FireEventPatch(IEnumerable<CodeInstruction> instructions)
		{
			var codes = new List<CodeInstruction>(instructions);
			for (int i = 0; i < codes.Count; i++)
			{
				if (codes[i].opcode == OpCodes.Ldstr && codes[i].operand is string s && s.Contains(STRING_END_TEXT))
				{
					codes[i] = CodeInstruction.Call(typeof(LoveTonicApplicatorPatch), nameof(AssembleText));
					codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_1));
				}
			}
			return codes.AsEnumerable();
		}

		private static string AssembleText(Event E)
		{
			GameObject Attacker = E.GetGameObjectParameter("Attacker");
			GameObject Defender = E.GetGameObjectParameter("Subject");
			MessageQueue.AddPlayerMessage($"assemble text (attacker {Attacker.DisplayName}, defender {Defender.DisplayName}");
			string toReturn = STRING_END_TEXT;
			if (Helpers.IsTweakEnabled(Tweaks.ShowProselytizeMath))
				toReturn += " ({{rules|" + (95 + Attacker.Stat("Level") - Defender.Stat("Level") - Defender.GetIntProperty("LoveTonicResistance")) + "%}} success chance)";
			return toReturn;
		}
	}
	#endregion
	#endregion
}
