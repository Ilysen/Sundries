using XRL;
using XRL.Wish;
using XRL.World;
using XRL.World.Parts;

namespace UnnamedTweaksCollection.Scripts
{
    [HasWishCommand]
    public class Wishes
    {
        [WishCommand(Command = "disguisetest")]
        public static bool DisguiseTestHandler(string rest)
        {
            WishResult wr = WishSearcher.SearchForBlueprint(rest);
            GameObject obj = GameObjectFactory.create(wr.Result);
            obj.AddPart<ModDisguise>();
            The.Player.ReceiveObject(obj);
            return true;
        }

        [WishCommand(Command = "slimetime")]
        public static bool SlimetimeHandler()
        {
            The.Player.AddPart<Engulfing>();
            EngulfingDamage ed = The.Player.AddPart<EngulfingDamage>();
            ed.Amount = "2-6";
            return true;
        }
    }
}
