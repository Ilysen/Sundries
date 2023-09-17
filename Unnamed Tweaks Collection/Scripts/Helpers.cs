using XRL.UI;

namespace UnnamedTweaksCollection.Scripts
{
    public class Helpers
    {
        /// <summary>
        /// Returns whether or not the given <see cref="Tweaks"/> entry is enabled.
        /// As of right now every tweakable setting uses a checkbox, but this could be expanded to other option types in the future.
        /// </summary>
        public static bool TweakEnabled(Tweaks tweakType)
        {
            return Options.GetOption($"UnnamedTweaksCollection_{tweakType}").EqualsNoCase("Yes");
        }
    }

    /// <summary>
    /// To avoid reliance on repeating magic strings, we use enums to track each tweak that can be enabled or disabled.<br/>
    /// These each correspond to a key in Options.xml that is formatted like so: <code>UnnamedTweaksCollection_(tweak name)</code>
    /// Thus, a tweak named <c>ExampleTweak</c> would correspond to an option whose key is equal to <c>UnnamedTweaksCollection_ExampleTweak</c>.
    /// The naming must be exact for the system to pick up the setting!
    /// </summary>
    public enum Tweaks
    {
        None,
        NameCarbideChefRecipes,
        DontTakeTownWater,
        DontTakeAllJunk,
        DifferentiateMaxCells,
        EnableItemHauling,
        RemoveCellByDefault
    }
}
