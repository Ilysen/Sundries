using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XRL.World;
using XRL.World.Parts;

namespace UnnamedTweaksCollection.Scripts
{
    public static class Helpers
    {
        public static void RemoveModification(GameObject Object, IModification ModPart)
        {
            Commerce commerce = Object.GetPart<Commerce>();
            if (commerce != null)
                commerce.Value /= ModificationFactory.ModsByPart[ModPart.Name].Value;
            Object.RemovePart(ModPart);
        }
    }
}
