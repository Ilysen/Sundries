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
            if (Helpers.TweakEnabled(Tweaks.EnableItemHauling))
                The.Player?.RequirePart<UnnamedTweaksCollection_HaulingHandler>();
            else if (The.Player != null && The.Player.HasPart<UnnamedTweaksCollection_HaulingHandler>())
                The.Player.RemovePart<UnnamedTweaksCollection_HaulingHandler>();
        }
    }

    [PlayerMutator]
    public class NewCharacterHandler : IPlayerMutator
    {
        public void mutate(GameObject player)
        {
            if (Helpers.TweakEnabled(Tweaks.EnableItemHauling))
                player.AddPart<UnnamedTweaksCollection_HaulingHandler>();
        }
    }
}
