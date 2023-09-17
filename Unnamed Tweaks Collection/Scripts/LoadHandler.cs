using XRL;
using XRL.World.Parts;

namespace UnnamedTweaksCollection.Scripts
{
    [HasCallAfterGameLoaded]
    public class UnnamedTweaksPack_LoadGameHandler
    {
        [CallAfterGameLoaded]
        public static void MyLoadGameCallback()
        {
            The.Player?.RequirePart<UnnamedTweaksCollection_HaulingHandler>();
        }
    }
}
