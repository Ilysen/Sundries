using ConsoleLib.Console;
using System;
using System.Collections.Generic;
using System.Linq;
using XRL.Messages;
using XRL.Rules;

namespace XRL.World.Parts
{
    [Serializable]
    [PlayerMutator]
    public class UnnamedTweaksCollection_HaulingHandler : IPart, IPlayerMutator
    {
        public static readonly string ItemHaulCommand = "UnnamedTweaksCollectionToggleHaul";
        private bool IsHauling => IsMyActivatedAbilityToggledOn(ActivatedAbility, ParentObject);
        public Guid ActivatedAbility;

        public void mutate(GameObject player)
        {
            player.AddPart<UnnamedTweaksCollection_HaulingHandler>();
        }

        public override void Attach()
        {
            if (ActivatedAbility == Guid.Empty)
                ActivatedAbility = ParentObject.AddActivatedAbility("Haul Items", ItemHaulCommand, "Skill", Toggleable: true);
            base.Attach();
        }

        private bool CanHaul(GameObject go)
        {
            return go.IsTakeable() && go.IsValid() && !go.IsInGraveyard();
        }

        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade) || ID == EnteredCellEvent.ID || ID == CommandEvent.ID;
        }

        public override bool HandleEvent(CommandEvent E)
        {
            if (E.Command == ItemHaulCommand && E.Actor == ParentObject)
            {
                List<GameObject> haulables = E.Actor.CurrentCell.GetObjects().Where(x => CanHaul(x)).ToList();
                if (haulables.Count == 0)
                    MessageQueue.AddPlayerMessage("There's nothing to haul there.");
                else
                {
                    string loadWarning = "";
                    if (!IsHauling)
                    {
                        int totalLoad = 0;
                        foreach (GameObject go in haulables)
                            totalLoad += EnergyForItem(go);
                        if (totalLoad >= 2000)
                            loadWarning = " {{W|Hauling this load will take some time.}}.";
                        if (totalLoad >= 5000)
                            loadWarning = " {{R|Hauling this load will be very slow}}.";
                    }
                    MessageQueue.AddPlayerMessage("You {{c|" + (!IsHauling ? "start" : "stop") + " hauling items}}." + loadWarning);
                    ToggleMyActivatedAbility(ActivatedAbility, E.Actor, true);
                }
            }
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(EnteredCellEvent E)
        {
            if (IsHauling)
            {
                string oppositeDir = Directions.GetOppositeDirection(E.Direction);
                Cell prevCell = E.Cell.GetCellFromDirection(oppositeDir);
                if (prevCell == null || E.Cell.OnWorldMap() || (!E.Cell.IsAdjacentTo(prevCell) && !E.Cell.HasStairs()))
                {
                    MessageQueue.AddPlayerMessage("You can no longer reach your previous cell, so you {{c|stop hauling}}.");
                    The.Player.ToggleActivatedAbility(ActivatedAbility, true, false);
                    return base.HandleEvent(E);
                }
                bool hauled = false;
                List<GameObject> objects = prevCell.GetObjectsWithPart("Physics");
                foreach (GameObject go in objects.Where(o => o.IsTakeable() && o.IsValid() && !o.IsInGraveyard()))
                {
                    ParentObject.UseEnergy(EnergyForItem(go));
                    if (go.Move(E.Direction, true, Actor: E.Actor))
                        hauled = true;
                }
                if (!hauled)
                {
                    MessageQueue.AddPlayerMessage("There are no more items to haul, so you {{c|stop hauling}}.");
                    E.Actor.ToggleActivatedAbility(ActivatedAbility, true, false);
                }
            }
            return base.HandleEvent(E);
        }

        private int EnergyForItem(GameObject go) => 50 * Math.Max(1, go.Weight);
    }
}
