using XRL;
using XRL.UI;
using XRL.World;

namespace UnnamedTweaksCollection.Scripts
{
    public class Helpers
    {
        /// <summary>
        /// Returns whether or not the given <see cref="Tweaks"/> entry is enabled.
        /// This only applies for checkbox tweaks.
        /// </summary>
        public static bool IsTweakEnabled(Tweaks tweakType)
        {
            return GetTweakSetting(tweakType).EqualsNoCase("Yes");
        }

        /// <summary>
        /// Fetches the current state of a given tweak's setting, if it has one.
        /// </summary>
        public static string GetTweakSetting(Tweaks tweakType)
        {
            return Options.GetOption($"UnnamedTweaksCollection_{tweakType}");
        }

        /// <summary>
        /// As <see cref="GetTweakSetting(Tweaks)"/>, but outputs the setting as a string as well.
        /// </summary>
        public static string GetTweakSetting(Tweaks tweakType, out string setting)
        {
            setting = Options.GetOption($"UnnamedTweaksCollection_{tweakType}");
            return setting;
        }

        public static bool ShouldBlockFromAutogetAndDisassemble(GameObject obj)
        {
            if (GetTweakSetting(Tweaks.DontTakeYurlsTreeItMakesThemSad, out string setting) == "Never")
                return false;
            if (obj.HasTag("UnnamedTweaksCollection_NoAutogetInTowns"))
            {
                if ((setting.EqualsNoCase("In Towns") && CheckpointingSystem.IsPlayerInCheckpoint()) || setting.EqualsNoCase("Always"))
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// To avoid reliance on repeating magic strings, we use enums to track each tweak that can be enabled or disabled.<br/>
    /// These each correspond to a key in Options.xml that is formatted like so: <code>UnnamedTweaksCollection_(tweak name)</code>
    /// Thus, a tweak named <c>ExampleTweak</c> would correspond to an option whose key is equal to <c>UnnamedTweaksCollection_ExampleTweak</c>.
    /// The naming must be exact for the system to pick up the setting!
    /// </summary>

    // The following tweaks can't be disabled right now due to there being no meaningful way to change XML loading depending on conditionals:
    // - Implement the Mindful skill, under the Customs and Folklore tree
    // Once the infrastructure is there, they'll be made toggleable!
    public enum Tweaks
    {
        None,
        NameCarbideChefRecipes,
        DontTakeAllJunk,
        DontTakeTownWater,
        DontTakeYurlsTreeItMakesThemSad,
        DifferentiateMaxCells,
        EnableItemHauling,
        RemoveCellByDefault,
        TreatModdedItemsAsScrap,
        TreatRobotLimbsAsScrap
    }
}
