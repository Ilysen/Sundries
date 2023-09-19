using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace UnnamedTweaksCollection.Scripts
{
    [HasCallAfterGameLoaded]
    public class LoadGameHandler
    {
        [CallAfterGameLoaded]
        public static void AfterLoaded()
        {
            if (Helpers.TweakSetting(Tweaks.EnableItemHauling) != "Disabled")
            {
                UnnamedTweaksCollection_HaulingHandler haulingHandler = The.Player?.RequirePart<UnnamedTweaksCollection_HaulingHandler>();
                if (haulingHandler != null && haulingHandler.IsMyActivatedAbilityToggledOn(haulingHandler.ActivatedAbility))
                    haulingHandler.ToggleMyActivatedAbility(haulingHandler.ActivatedAbility, Silent: true, SetState: false);
            }
            else if (The.Player != null && The.Player.HasPart<UnnamedTweaksCollection_HaulingHandler>())
                The.Player.RemovePart<UnnamedTweaksCollection_HaulingHandler>();
        }
    }

    [PlayerMutator]
    public class NewCharacterHandler : IPlayerMutator
    {
        public void mutate(GameObject player)
        {
            if (Helpers.TweakSetting(Tweaks.EnableItemHauling) != "Disabled")
                player.AddPart<UnnamedTweaksCollection_HaulingHandler>();
        }
    }
}
