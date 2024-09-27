using Ceres.Sundries.Scripts;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace Ceres.Sundries.Scripts
{
	[HasCallAfterGameLoaded]
	public class LoadGameHandler
	{
		[CallAfterGameLoaded]
		public static void AfterLoaded()
		{
			if (Helpers.GetTweakSetting(Tweaks.EnableItemHauling) != "Disabled")
			{
				Ceres_Sundries_HaulingHandler haulingHandler = The.Player?.RequirePart<Ceres_Sundries_HaulingHandler>();
				if (haulingHandler != null && haulingHandler.IsMyActivatedAbilityToggledOn(haulingHandler.ActivatedAbility))
					haulingHandler.ToggleMyActivatedAbility(haulingHandler.ActivatedAbility, Silent: true, SetState: false);
			}
			else if (The.Player != null && The.Player.HasPart<Ceres_Sundries_HaulingHandler>())
				The.Player.RemovePart<Ceres_Sundries_HaulingHandler>();
		}
	}

	[PlayerMutator]
	public class NewCharacterHandler : IPlayerMutator
	{
		public void mutate(GameObject player)
		{
			if (Helpers.GetTweakSetting(Tweaks.EnableItemHauling) != "Disabled")
				player.AddPart<Ceres_Sundries_HaulingHandler>();
		}
	}
}
