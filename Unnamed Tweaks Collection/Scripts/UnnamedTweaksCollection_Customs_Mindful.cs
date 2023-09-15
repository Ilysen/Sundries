using System;
using System.Collections.Generic;
using System.Threading;
using XRL.UI;

namespace XRL.World.Parts.Skill
{
    [Serializable]
    public class UnnamedTweaksCollection_Customs_Mindful : BaseSkill
    {
        private static string RecordString => "UnnamedTweaksCollection_UsedMindful";

        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade) || ID == GetWaterRitualReputationAmountEvent.ID || ID == WaterRitualStartEvent.ID;
        }

        public override bool HandleEvent(WaterRitualStartEvent E)
        {
            // Retroactive - handle the reputation increase immediately
            // Although this fires once for every water ritual, if we're doing it for the first time, it comes *after* the other event calls
            // Therefore, if it meets the relevant conditions, we can safely assume that the player is doing this retroactively
            if (E.Actor == ParentObject && !E.Record.Has(RecordString) && E.Actor.IsPlayer())
                ChooseMindfulFaction(E.Record, true);
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetWaterRitualReputationAmountEvent E)
        {
            // Performing for the first time - check for the relevant property
            if (E.Actor == ParentObject && !E.Record.Has(RecordString) && E.Actor.IsPlayer())
            {
                CheckFaction:
                if (E.Record.ParentObject.TryGetStringProperty("UnnamedTweaksCollection_MindfulFaction", out string mindfulOf))
                {
                    if (E.Faction == mindfulOf)
                    {
                        E.Amount += 25;
                        E.Record.attributes.Add(RecordString);
                    }
                }
                else
                {
                    ChooseMindfulFaction(E.Record);
                    goto CheckFaction;
                }
            }
            return base.HandleEvent(E);
        }

        /// <summary>
        /// This function gives the player a prompt of factions that the water ritual speaker is enemies with, and then:
        /// <list type="bullet">
        /// <item><b><c>true:</c></b> Immediately increases the player's reputation with that faction by 25.</item>
        /// <item><b><c>false:</c></b> Sets a string property on the speaker that is then referenced immediately after in one of the HandleEvent calls.</item>
        /// </list>
        /// </summary>
        private void ChooseMindfulFaction(WaterRitualRecord record, bool retroactive = false)
        {
            GivesRep rep = record.ParentObject.GetPart<GivesRep>();
            string chosenFaction = string.Empty;
            if (rep == null)
                return;
            List<string> foes = new List<string>();
            foreach (FriendorFoe fof in rep.relatedFactions)
            {
                if (fof.status != "friend")
                    foes.Add(fof.faction);
            }
            if (foes.Count <= 0) // No reason to continue - just back out
            {
                record.ParentObject.SetStringProperty("UnnamedTweaksCollection_MindfulFaction", chosenFaction);
                return;
            }
            else if (foes.Count == 1)
                chosenFaction = foes[0];
            else if (foes.Count > 1)
            {
                List<string> factionNames = new List<string>();
                foreach (string foe in foes)
                {
                    Faction faction = Factions.get(foe);
                    factionNames.Add($"{faction.DisplayName} (current reputation {faction.CurrentReputation})");
                }
                int index = Popup.ShowOptionList("Choose a faction to be mindful of.", factionNames.ToArray());
                chosenFaction = foes[index];
            }
            if (chosenFaction != null)
            {
                if (retroactive)
                {
                    The.Game.PlayerReputation.modify(chosenFaction, 25, "UnnamedTweaksCollection_WaterRitualMindfulAward", because: $"because you were mindful of their sensitive history with {record.ParentObject.ShortDisplayName}");
                }
                else
                    record.ParentObject.SetStringProperty("UnnamedTweaksCollection_MindfulFaction", chosenFaction);
            }
        }
    }
}
