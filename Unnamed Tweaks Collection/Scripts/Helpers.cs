﻿using XRL.UI;

namespace UnnamedTweaksCollection.Scripts
{
    public class Helpers
    {
        /// <summary>
        /// Returns whether or not the given <see cref="Tweaks"/> entry is enabled.
        /// This only applies for checkbox tweaks.
        /// </summary>
        public static bool TweakEnabled(Tweaks tweakType)
        {
            return TweakSetting(tweakType).EqualsNoCase("Yes");
        }

        /// <summary>
        /// Fetches the current state of a given tweak's setting, if it has one.
        /// </summary>
        public static string TweakSetting(Tweaks tweakType)
        {
            return Options.GetOption($"UnnamedTweaksCollection_{tweakType}");
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
        RemoveCellByDefault
    }
}