using HistoryKit;
using Qud.API;
using System.Collections.Generic;
using System.Linq;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace UnnamedTweaksCollection.Scripts
{
    public static class Helpers
    {
        public static void RemoveModification(GameObject Object, IModification ModPart)
        {
            // So, this is a bit nuts
            // Engraved and Painted add an AddsRep part based on the sultan that they generate with, rather than having that reputation bonus baked into themselves
            // We could just remove all AddsRep instances, but that could remove things native to the item as well
            // So instead, we do this: manually look up historical figures from what info we *do* have, and if they match, remove specifically that part
            string relevantSultan = null;
            if (ModPart is ModEngraved Engraved)
                relevantSultan = Engraved.Sultan;
            else if (ModPart is ModPainted Painted)
                relevantSultan = Painted.Sultan;
            if (relevantSultan != null)
            {
                MessageQueue.AddPlayerMessage($"Searching for sultan in history: {relevantSultan}");
                foreach (AddsRep rep in Object.PartsList.Where(c => c is AddsRep).Cast<AddsRep>())
                {
                    string sanitizedFaction = rep.Faction.Split(':')[0];
                    MessageQueue.AddPlayerMessage($"Searching AddsRep part with faction of {sanitizedFaction}");
                    IEnumerable<HistoricEntity> entities = HistoryAPI.GetSultans().Where(c => c.GetCurrentSnapshot().GetProperty("name") == relevantSultan);
                    if (entities == null)
                    {
                        MessageQueue.AddPlayerMessage($"Found no entities with the specified name. Breaking");
                        break;
                    }
                    foreach (HistoricEntity sultan in entities)
                    {
                        MessageQueue.AddPlayerMessage($"Checking data of historical entity {sultan.id} with name {sultan.GetCurrentSnapshot().Name}...");
                        string cultName = Faction.getSultanFactionName(sultan.GetCurrentSnapshot().GetProperty("period", "0"));
                        MessageQueue.AddPlayerMessage($"This cult name is {cultName}. The period is {sultan.GetCurrentSnapshot().GetProperty("period", "NOT FOUND")}.");
                        if (cultName != null && sanitizedFaction == cultName)
                        {
                            MessageQueue.AddPlayerMessage($"Cult name found - {cultName}. Removing this part.");
                            Object.RemovePart(rep);
                            goto PostSultan;
                        }
                    }
                }
            }
            PostSultan:

            // Disguise logic is less cursed than Engraved and Painted, but only just
            // Instead of finding the historical figure, we emulate/repeat the logic that the base mod uses to find the right blueprint,
            // and remove the relevant AddsRep part if it exists based upon that
            if (ModPart is ModDisguise Disguise)
            {
                MessageQueue.AddPlayerMessage($"Handling disguise logic where DisguiseBlueprint is {Disguise.DisguiseBlueprint}");
                foreach (AddsRep rep in Object.PartsList.Where(c => c is AddsRep).Cast<AddsRep>())
                {
                    MessageQueue.AddPlayerMessage($"Searching AddsRep part with faction of {rep.Faction}");
                    string partParameter = GameObjectFactory.Factory.Blueprints[Disguise.DisguiseBlueprint]?.GetPartParameter<string>("Brain", "Factions", null);
                    if (partParameter == null)
                    {
                        MessageQueue.AddPlayerMessage($"Found no matching parameter for this blueprint. Breaking");
                        break;
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
                }
            }
            PostDisguise:

            MessageQueue.AddPlayerMessage($"Adjust commerce");
            Commerce commerce = Object.GetPart<Commerce>();
            if (commerce != null)
                commerce.Value /= ModificationFactory.ModsByPart[ModPart.Name].Value;

            MessageQueue.AddPlayerMessage($"Removing mod of name {ModPart.Name}");
            Object.RemovePart(ModPart);
        }
    }
}
