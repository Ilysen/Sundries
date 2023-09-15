using HistoryKit;
using Qud.API;
using System.Collections.Generic;
using System.Linq;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace UnnamedTweaksCollection.Scripts
{
    public static class Helpers
    {
        public static void RemoveModification(GameObject Object, IModification ModPart)
        {
            // If we have no need to run the logic below, then we won't
            AddsRep rep = Object.GetPart<AddsRep>();
            if (rep == null)
                goto PostDisguise;
            // Everything below this line is extremely awful. You have been warned.



            // So, a bit of primer on why this code has to be existential horror:
            // In the game's code, AddsRep is partitioned as a formatted string of faction names associated to their rep bonus.
            // The Scaled mod has it formatted like 'Unshelled Reptiles:250:hidden', for instance;
            // If we have a scaled fork-horned helm, it'll look like 'Antelopes:100,Goatfolk:100,Unshelled Reptiles:250:hidden'
            // Usually, the part's code handles everything as needed through unequipping, wielding, and the like
            // The problem here is that AddsRep is built around being permanent. It is NOT meant to have things removed from it, only added.
            // As a result, this entire section is bespoke code to break down the string and parse what it means, and remove piecemeal parts of it as needed.
            // We need to do this carefully so as to not malform the string and mess it up, and doing that takes a lot of work on our part.
            // It is awful and terrible and why don't you just use a dictionary or a list of structs, AddsRep, whyyyy.
            // But it's what we have available, so it's what we're dealing with. Paradoxically, this ensures maximum compatibility, so it's worth the pain.
            string relevantSultan = null;
            if (ModPart is ModEngraved Engraved)
                relevantSultan = Engraved.Sultan;
            else if (ModPart is ModPainted Painted)
                relevantSultan = Painted.Sultan;
            if (relevantSultan != null)
            {
                RemoveRepBoostForFaction(rep, FindFactionForSultan(relevantSultan));
                /*MessageQueue.AddPlayerMessage($"Searching for sultan in history: {relevantSultan}");
                string[] entries = rep.Faction.Split(',');
                MessageQueue.AddPlayerMessage($"AddsRep faction {rep.Faction} has been broken down into the following entries: {string.Join(" ~ ", entries)}");
                foreach (string factionEntry in entries)
                {
                    string sanitizedFaction = factionEntry.Split(':')[0];
                    MessageQueue.AddPlayerMessage($"Searching AddsRep part with faction of {sanitizedFaction}");
                    IEnumerable<HistoricEntity> entities = HistoryAPI.GetSultans().Where(c => c.GetCurrentSnapshot().GetProperty("name") == relevantSultan);
                    if (entities == null)
                    {
                        MessageQueue.AddPlayerMessage($"Found no entities with the specified name. Breaking");
                        return;
                    }
                    foreach (HistoricEntity sultan in entities)
                    {
                        MessageQueue.AddPlayerMessage($"Checking data of historical entity {sultan.id} with name {sultan.GetCurrentSnapshot().Name}...");
                        string cultName = Faction.getSultanFactionName(sultan.GetCurrentSnapshot().GetProperty("period", "0"));
                        MessageQueue.AddPlayerMessage($"This cult name is {cultName}. The period is {sultan.GetCurrentSnapshot().GetProperty("period", "NOT FOUND")}.");
                        if (cultName != null && sanitizedFaction == cultName)
                        {
                            RemoveRepBoostForFaction(rep, cultName);
                            goto PostSultan;
                        }
                    }
                }*/
            }

            if (ModPart is ModScaled)
                RemoveRepBoostForFaction(rep, "Unshelled Reptiles");
            else if (ModPart is ModFeathered)
                RemoveRepBoostForFaction(rep, "Birds");
            else if (ModPart is ModJewelEncrusted)
                RemoveRepBoostForFaction(rep, "Water"); // WHY IS THE WATER BARON FACTION JUST NAMED "WATER"? WHY???
            else if (ModPart is ModSnailEncrusted)
                RemoveRepBoostForFaction(rep, "Mollusks");

            // Disguise logic is less cursed than Engraved and Painted, but only just
            // Instead of finding the historical figure, we emulate/repeat the logic that the base mod uses to find the right blueprint,
            // and remove the relevant AddsRep part if it exists based upon that
            /*if (ModPart is ModDisguise Disguise)
            {
                MessageQueue.AddPlayerMessage($"Handling disguise logic where DisguiseBlueprint is {Disguise.DisguiseBlueprint}");
                MessageQueue.AddPlayerMessage($"Searching AddsRep part with faction of {rep.Faction}");
                string partParameter = GameObjectFactory.Factory.Blueprints[Disguise.DisguiseBlueprint]?.GetPartParameter<string>("Brain", "Factions", null);
                if (partParameter == null)
                {
                    MessageQueue.AddPlayerMessage($"Found no matching parameter for this blueprint. Breaking");
                }
                foreach (string text in partParameter.CachedCommaExpansion())
                {
                    MessageQueue.AddPlayerMessage($"Checking this faction: {text}");
                    if (Brain.ExtractFactionMembership(text) > 0 && rep.Faction == text)
                    {
                        MessageQueue.AddPlayerMessage($"Faction found. Removing this part.");
                        Object.RemovePart(rep);
                        goto PostDisguise;
                    }

                }
            }*/
            PostDisguise:

            MessageQueue.AddPlayerMessage($"Adjust commerce");
            Commerce commerce = Object.GetPart<Commerce>();
            if (commerce != null)
                commerce.Value /= ModificationFactory.ModsByPart[ModPart.Name].Value;

            MessageQueue.AddPlayerMessage($"Removing mod of name {ModPart.Name}");
            Object.RemovePart(ModPart);
        }

        private static void RemoveRepBoostForFaction(AddsRep Rep, string Faction)
        {
            if (Faction.IsNullOrEmpty())
                return;
            MessageQueue.AddPlayerMessage($"Attempting to remove rep from the following rep string: {Rep.Faction}, for the following faction: {Faction}");
            string[] factionEntries = Rep.Faction.Split(',');
            MessageQueue.AddPlayerMessage($"Rep string has been split up as follows: {string.Join("~", factionEntries)}");
            foreach (string factionEntry in factionEntries)
            {
                string[] splitEntry = factionEntry.Split(':');
                MessageQueue.AddPlayerMessage($"Parsing for faction entry {factionEntry}, further split into {string.Join("~", splitEntry)}");
                if (splitEntry[0] == Faction && int.TryParse(splitEntry[1], out int toRemove))
                {
                    MessageQueue.AddPlayerMessage("This is our faction. Attempting to remove...");
                    if (Rep.AppliedBonus)
                        The.Game.PlayerReputation.modify(Faction, -toRemove, "AddsRepApply");
                    // There's probably a cleaner way to do this but I've been awake for 20 hours. :3
                    Rep.Faction = Rep.Faction.Replace($",{factionEntry}", string.Empty);
                    Rep.Faction = Rep.Faction.Replace($"{factionEntry},", string.Empty);
                    Rep.Faction = Rep.Faction.Replace(factionEntry, string.Empty);
                    MessageQueue.AddPlayerMessage($"New string state is: {Rep.Faction}");
                    break;
                }
            }
            if (Rep.Faction == string.Empty)
            {
                MessageQueue.AddPlayerMessage("Rep part is now empty. Removing.");
                Rep.ParentObject.RemovePart(Rep);
            }
        }

        private static string FindFactionForSultan(string SultanName)
        {
            IEnumerable<HistoricEntity> Sultans = HistoryAPI.GetSultans().Where(c => c.GetCurrentSnapshot().GetProperty("name") == SultanName);
            if (Sultans.IsNullOrEmpty())
            {
                MessageQueue.AddPlayerMessage($"Found no entities with the specified name. Breaking");
                return null;
            }
            foreach (HistoricEntity Sultan in Sultans)
            {
                string cultName = Faction.getSultanFactionName(Sultan.GetCurrentSnapshot().GetProperty("period", "0"));
                MessageQueue.AddPlayerMessage($"This cult name is {cultName}. The period is {Sultan.GetCurrentSnapshot().GetProperty("period", "NOT FOUND")}.");
                if (cultName != null)
                    return cultName;
            }
            return null;
        }
    }
}
