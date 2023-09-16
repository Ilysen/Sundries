using HistoryKit;
using Qud.API;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace UnnamedTweaksCollection.Scripts
{
    public static class Helpers
    {
        public static void RemoveModification(GameObject Object, IModification ModPart)
        {
            // TODO: Complexity and difficulty resets
            // AntiGravity
            // CoProcessor
            // DrumLoaded
            // Gesticulating
            
            // Unremovable: Colossal, Extradimensional, Psionic, relic effects
            // Handles removal on its own: Featherweight

            AddsRep rep = Object.GetPart<AddsRep>();
            int difficultyAdjustment = 0;
            int complexityAdjustment = 0;
            bool cellAddedByMod = false;
            if (rep == null)
                goto PostReputationUpdates;

            string relevantSultan = null;
            if (ModPart is ModAirfoil)
            {
                Object.ModIntProperty("ThrowRangeBonus", -4, true);
                difficultyAdjustment = 1;
                complexityAdjustment = 1;
            }
            else if (ModPart is ModAntiGravity)
            {
                difficultyAdjustment = 1;
                complexityAdjustment = 2;
                cellAddedByMod = true;
            }
            else if (ModPart is ModBeamsplitter)
            {
                MissileWeapon mw = Object.GetPart<MissileWeapon>();
                mw.ShotsPerAction /= 3;
                mw.ShotsPerAnimation /= 3;
                difficultyAdjustment = 1;
                complexityAdjustment = 2;
            }
            else if (ModPart is ModCleated Cleats)
            {
                SaveModifier sm = Object.GetPart<SaveModifier>();
                sm.Amount -= ModCleated.GetSaveModifierAmount(Cleats.Tier);
                if (sm.Amount <= 0)
                    Object.RemovePart(sm);
                difficultyAdjustment = 1;
            }
            else if (ModPart is ModCoProcessor)
            {
                difficultyAdjustment = 1;
                complexityAdjustment = 2;
                cellAddedByMod = true;
            }
            else if (ModPart is ModCounterweighted Counterweight)
            {
                Object.GetPart<MeleeWeapon>().HitBonus -= Counterweight.GetModificationLevel();
                difficultyAdjustment = 1;
                complexityAdjustment = 1;
            }
            else if (ModPart is ModDisplacer)
            {
                difficultyAdjustment = 2;
                complexityAdjustment = 1;
                cellAddedByMod = true;
            }
            else if (ModPart is ModDrumLoaded)
            {
                difficultyAdjustment = 1;
                complexityAdjustment = 1;
            }
            else if (ModPart is ModElectrified)
            {
                difficultyAdjustment = 1;
                complexityAdjustment = 1;
                cellAddedByMod = true;
            }
            else if (ModPart is ModFilters)
            {
                difficultyAdjustment = 1;
                complexityAdjustment = 1;
            }
            else if (ModPart is ModFlaming)
            {
                difficultyAdjustment = 1;
                complexityAdjustment = 1;
                cellAddedByMod = true;
            }
            else if (ModPart is ModFlexiweaved Flexiweave)
            {
                Object.GetPart<Armor>().DV -= Flexiweave.GetModificationLevel();
                complexityAdjustment = 1;
            }
            else if (ModPart is ModFreezing)
            {
                difficultyAdjustment = 1;
                complexityAdjustment = 1;
                cellAddedByMod = true;
            }

            if (ModPart is ModEngraved Engraved)
                relevantSultan = Engraved.Sultan;
            else if (ModPart is ModPainted Painted)
                relevantSultan = Painted.Sultan;
            else if (ModPart is ModScaled)
                RemoveRepBoostForFaction(rep, "Unshelled Reptiles");
            else if (ModPart is ModFeathered)
                RemoveRepBoostForFaction(rep, "Birds");
            else if (ModPart is ModJewelEncrusted)
                RemoveRepBoostForFaction(rep, "Water"); // WHY IS THE WATER BARON FACTION JUST NAMED "WATER"? WHY???
            else if (ModPart is ModSnailEncrusted)
                RemoveRepBoostForFaction(rep, "Mollusks");
            else if (ModPart is ModDisguise Disguise)
            {
                string partParameter = GameObjectFactory.Factory.Blueprints[Disguise.DisguiseBlueprint]?.GetPartParameter<string>("Brain", "Factions", null);
                difficultyAdjustment = 2;
                if (partParameter == null)
                    goto PostDisguise;
                foreach (string text in partParameter.CachedCommaExpansion())
                {
                    string cachedText = text;
                    if (Brain.ExtractFactionMembership(ref cachedText) > 0)
                    {
                        Faction faction = Factions.get(cachedText);
                        if (faction != null)
                            RemoveRepBoostForFaction(rep, cachedText);
                    }
                }
                if (Disguise.ParentObject.Equipped is GameObject)
                {
                    MethodInfo unapplyDisguise = Disguise.GetType().GetMethod("UnapplyDisguise", BindingFlags.NonPublic | BindingFlags.Instance);
                    unapplyDisguise.Invoke(Disguise, new object[] { Disguise.ParentObject.Equipped });
                }
            }
            PostDisguise:

            if (relevantSultan != null)
                RemoveRepBoostForFaction(rep, FindFactionForSultan(relevantSultan));

            PostReputationUpdates:

            Commerce commerce = Object.GetPart<Commerce>();
            if (commerce != null)
                commerce.Value /= ModificationFactory.ModsByPart[ModPart.Name].Value;

            Object.RemovePart(ModPart);
        }

        private static void RemoveRepBoostForFaction(AddsRep Rep, string Faction)
        {
            // So, a bit of primer on why this code has to be existential horror:
            // In the game's code, AddsRep is partitioned as a formatted string of faction names associated to their rep bonus.
            // The Scaled mod has it formatted like 'Unshelled Reptiles:250:hidden', for instance;
            // If we have a scaled and engraved fork-horned helm, it'll look like 'Antelopes,Goatfolk,Unshelled Reptiles:250:hidden,SultanCult4:60'
            // The sultan cult rep in that last line there is random but generally looks like that. The antelopes and goatfolk stuff is handled in the item def itself.
            // Usually, the part's code handles everything as needed through unequipping, wielding, and the like
            // The problem here is that AddsRep is built around being permanent. It is NOT meant to have things removed from it, only added.
            // As a result, this entire section is bespoke code to break down the string and parse what it means, and remove piecemeal parts of it as needed.
            // We need to do this carefully so as to not malform the string and mess it up, and doing that takes a lot of work on our part.
            // It is awful and terrible and why don't you just use a dictionary or a list of structs, AddsRep, whyyyy.
            // But it's what we have available, so it's what we're dealing with. Paradoxically, this ensures maximum compatibility, so it's worth the pain.
            if (Faction.IsNullOrEmpty())
                return;
            string[] factionEntries = Rep.Faction.Split(',');
            foreach (string factionEntry in factionEntries)
            {
                string[] splitEntry = factionEntry.Split(':');
                if (splitEntry[0] == Faction && int.TryParse(splitEntry[1], out int toRemove))
                {
                    if (Rep.AppliedBonus)
                        The.Game.PlayerReputation.modify(Faction, -toRemove, "AddsRepApply", silent: true, transient: true);
                    // There's probably a cleaner way to do this but I've been awake for 20 hours. :3
                    Rep.Faction = Rep.Faction.Replace($",{factionEntry}", string.Empty);
                    Rep.Faction = Rep.Faction.Replace($"{factionEntry},", string.Empty);
                    Rep.Faction = Rep.Faction.Replace(factionEntry, string.Empty);
                    break;
                }
            }
            if (Rep.Faction == string.Empty)
                Rep.ParentObject.RemovePart(Rep);
        }

        private static string FindFactionForSultan(string SultanName)
        {
            IEnumerable<HistoricEntity> Sultans = HistoryAPI.GetSultans().Where(c => c.GetCurrentSnapshot().GetProperty("name") == SultanName);
            if (Sultans.IsNullOrEmpty())
                return null;
            foreach (HistoricEntity Sultan in Sultans)
            {
                string cultName = Faction.getSultanFactionName(Sultan.GetCurrentSnapshot().GetProperty("period", "0"));
                if (cultName != null)
                    return cultName;
            }
            return null;
        }
    }
}
