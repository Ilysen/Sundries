using Ceres.Sundries.Scripts;
using System;
using System.Collections.Generic;
using System.Linq;
using XRL.Messages;
using XRL.Rules;
using XRL.World.Capabilities;

namespace XRL.World.Parts
{
	/// <summary>
	/// This class is used to handle the logic of the item hauling system. The part itself is added and removed from the player in <c><see cref="LoadGameHandler"/></c> and <c><see cref="NewCharacterHandler"/></c>.
	/// </summary>
	[Serializable]
	public class Ceres_Sundries_HaulingHandler : IPlayerPart
	{
		/// <summary>
		/// The string command used to toggle hauling. Should correspond to the key in Abilities.xml.
		/// </summary>
		public static readonly string ItemHaulCommand = "Ceres_Sundries_ToggleHaul";

		/// <summary>
		/// Wrapper that checks if <c><see cref="ActivatedAbility"/></c> exists and is toggled on.
		/// </summary>
		private bool IsHauling => IsMyActivatedAbilityToggledOn(ActivatedAbility, ParentObject);

		/// <summary>
		/// The <c><see cref="Guid"/></c> of the active ability that's used to keep track of 
		/// </summary>
		public Guid ActivatedAbility;

		/// <summary>
		/// Returns whether or not a given <c><see cref="GameObject"/></c> can be moved by the hauling system.
		/// </summary>
		private bool CanHaul(GameObject go) => go.IsTakeable() && go.IsValid() && !go.IsInGraveyard() && !go.IsOwned() && go.InInventory == null;

		/// <summary>
		/// A cached list of <c><see cref="GameObject"/></c> that we're hauling. If <c><see cref="Tweaks.EnableItemHauling"/></c> is set to <c>All</c>, this will be updated every turn;
		/// otherwise, it will only be populated once, when hauling starts.
		/// </summary>
		private List<GameObject> ToHaul = new();

		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, GetType());
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, GetType());
		}

		public override void Attach()
		{
			if (ActivatedAbility == Guid.Empty)
				ActivatedAbility = ParentObject.AddActivatedAbility("Haul Items", ItemHaulCommand, "Skill", Silent: true, Toggleable: true);
			base.Attach();
		}

		public override void Remove()
		{
			RemoveMyActivatedAbility(ref ActivatedAbility, ParentObject);
			base.Remove();
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
						if (haulables.Count >= 3)
							loadWarning = " {{W|Hauling this load will be slow.}}";
						if (haulables.Count >= 5)
							loadWarning = " {{R|Hauling this load will be very slow.}}";
					}
					MessageQueue.AddPlayerMessage("You {{c|" + (!IsHauling ? "start" : "stop") + " hauling items}}." + loadWarning);
					ToggleMyActivatedAbility(ActivatedAbility, E.Actor, true);
					if (!IsHauling)
						ToHaul.Clear();
					else
						ToHaul = haulables;
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
				List<GameObject> _toHaul = ToHaul;
				if (Helpers.GetTweakSetting(Tweaks.EnableItemHauling) == "All")
					_toHaul = prevCell.GetObjects().Where(x => CanHaul(x)).ToList();
				foreach (GameObject go in _toHaul.ToArray())
				{
					if (!CanHaul(go))
						ToHaul.Remove(go);
					else
					{
						if (E.Actor.TakeObject(go, Silent: true, EnergyCost: 1000) &&
							InventoryActionEvent.Check(E.Actor, E.Actor, go, "CommandDropObject", Forced: true, Silent: true))
							hauled = true;
						AutoAct.Interrupt();
					}
					if (ToHaul.Count <= 0)
					{
						hauled = false;
						break;
					}
				}
				if (!hauled)
				{
					MessageQueue.AddPlayerMessage("There are no more items to haul, so you {{c|stop hauling}}.");
					E.Actor.ToggleActivatedAbility(ActivatedAbility, true, false);
				}
			}
			return base.HandleEvent(E);
		}
	}
}
