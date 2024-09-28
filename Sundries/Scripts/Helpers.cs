using XRL;
using XRL.UI;
using XRL.World;
using XRL.Wish;
using System.Linq;
using XRL.World.Parts;
using XRL.Messages;
using XRL.World.ZoneParts;

namespace Ceres.Sundries.Scripts
{
	[HasWishCommand]
	public class Helpers
	{
		/// <summary>
		/// Returns whether or not the given <c><see cref="Tweaks"/></c> entry is enabled.
		/// This only applies for checkbox tweaks.
		/// </summary>
		public static bool IsTweakEnabled(Tweaks tweakType) => GetTweakSetting(tweakType).EqualsNoCase("Yes");

		/// <summary>
		/// Fetches the current state of a given tweak's setting, if it has one.
		/// </summary>
		public static string GetTweakSetting(Tweaks tweakType) => Options.GetOption($"Ceres_Sundries_{tweakType}");

		/// <summary>
		/// As <c><see cref="GetTweakSetting(Tweaks)"/></c>, but outputs the setting as a string as well.
		/// </summary>
		public static string GetTweakSetting(Tweaks tweakType, out string setting)
		{
			setting = Options.GetOption($"Ceres_Sundries_{tweakType}");
			return setting;
		}

		public static bool ShouldBlockFromAutogetAndDisassemble(GameObject obj)
		{
			if (GetTweakSetting(Tweaks.DontTakeYurlsTreeItMakesThemSad, out string setting) == "Never")
				return false;
			if (obj.HasTag("Ceres_Sundries_NoAutogetInTowns"))
			{
				if ((setting.EqualsNoCase("In Towns") && CheckpointingSystem.IsPlayerInCheckpoint()) || setting.EqualsNoCase("Always"))
					return true;
			}
			return false;
		}

		[WishCommand(Command = "zonetime")]
		public static void Zones()
		{
			foreach (var zone in The.ZoneManager.CachedZones.Values)
			{
				MessageQueue.AddPlayerMessage($"Cached zone: {zone.DisplayName} ({zone.ResolvedLocation}), {zone.GetObjectsWithPart(nameof(Ceres_Sundries_BarathrumiteShelter)).Count} object(s) with shelter part");
			}
		}

		[WishCommand(Command = "droptemplar")]
		public static void DropTemplar()
		{
			ScriptCallToArms scta = The.ActiveZone.GetPart<ScriptCallToArms>();
			scta.n = 249;
			The.Player.PassTurn();
			MessageQueue.AddPlayerMessage("Templar have dropped.");
		}

		[WishCommand(Command = "templarbgone")]
		public static void TemplarBGone()
		{
			MessageQueue.AddPlayerMessage("All Templar in this zone have been killed.");
			The.Player.PlayWorldOrUISound("Sounds/Interact/sfx_interact_timeCube_activate");
			The.Player.DilationSplat();
			foreach (GameObject go in The.ActiveZone.GetObjects().Where(x => x.Brain?.GetPrimaryFaction() == "Templar"))
				go.Die();
		}
	}

	/// <summary>
	/// To avoid reliance on repeating magic strings, we use enums to track each tweak that can be enabled or disabled.<br/>
	/// These each correspond to a key in Options.xml that is formatted like so: <code>Sundries_(tweak name)</code>
	/// Thus, a tweak named <c>ExampleTweak</c> would correspond to an option whose key is equal to <c>Sundries_ExampleTweak</c>.
	/// The naming must be exact for the system to pick up the setting!
	/// </summary>

	// The following tweaks can't be disabled right now due to there being no meaningful way to change XML loading depending on conditionals:
	// - Implements the Mindful skill, under the Customs and Folklore tree
	// - Allows the Barathrumites to take shelter in the study during A Call to Arms
	// Once the infrastructure is there, they'll be made toggleable!
	public enum Tweaks
	{
		None,
		NameCarbideChefRecipes,
		DontTakeAllJunk,
		DontTakeTownWater,
		DontTakeYurlsTreeItMakesThemSad,
		DifferentiateMaxCells,
		DisableGeomagneticDiscAnimation,
		DisableItemNaming,
		EnableMassPsychometry,
		EnableItemHauling,
		RemoveCellByDefault,
		ShowProselytizeMath,
		TreatModdedItemsAsScrap,
		TreatRobotLimbsAsScrap
	}
}
