using XRL;
using XRL.World;
using XRL.World.Parts;

namespace Ava.UnnamedTweaksCollection.Scripts
{
	[HasCallAfterGameLoaded]
	public class LoadGameHandler
	{
		[CallAfterGameLoaded]
		public static void AfterLoaded()
		{
			if (Helpers.GetTweakSetting(Tweaks.EnableItemHauling) != "Disabled")
			{
				Ava_UnnamedTweaksCollection_HaulingHandler haulingHandler = The.Player?.RequirePart<Ava_UnnamedTweaksCollection_HaulingHandler>();
				if (haulingHandler != null && haulingHandler.IsMyActivatedAbilityToggledOn(haulingHandler.ActivatedAbility))
					haulingHandler.ToggleMyActivatedAbility(haulingHandler.ActivatedAbility, Silent: true, SetState: false);
			}
			else if (The.Player != null && The.Player.HasPart<Ava_UnnamedTweaksCollection_HaulingHandler>())
				The.Player.RemovePart<Ava_UnnamedTweaksCollection_HaulingHandler>();
		}
	}

	[PlayerMutator]
	public class NewCharacterHandler : IPlayerMutator
	{
		public void mutate(GameObject player)
		{
			if (Helpers.GetTweakSetting(Tweaks.EnableItemHauling) != "Disabled")
				player.AddPart<Ava_UnnamedTweaksCollection_HaulingHandler>();
		}
	}
}
