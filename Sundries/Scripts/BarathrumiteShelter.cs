using System.Collections.Generic;
using XRL.Core;
using XRL.Messages;
using XRL.UI;
using XRL.World.AI.GoalHandlers;
using XRL.World.WorldBuilders;

namespace XRL.World.Parts
{
	/// <summary>
	/// <para>This is attached to all of the core Barathrumites in Grit Gate if you tell Otho to get to safety.
	/// It <b>aggressively</b> forces their AI to drop everything they're doing and go to a random cell in the study.
	/// To help make sure they don't get stuck, they only walk on the first z-level; once they're in the study they will teleport directly to their chosen spot.</para>
	///
	/// <para>After the quest is done, each creature will this part will teleport to the closest reachable cell to the upstairs staircase, at which point the part will be removed.</para>
	/// </summary>
	public class Ceres_Sundries_BarathrumiteShelter : IPart
	{
		/// <summary>
		/// A randomly chosen <see cref="Cell"/> in Barathrum's study that this part's parent object will attempt to aggressively move to.
		/// </summary>
		public Cell SafePlace;

		/// <summary>
		/// A list of <see cref="Cell"/>s that can be used as valid <see cref="SafePlace"/> destinations.
		/// If something picks this, we remove it from the list to ensure that two creatures don't try to pick the same cell.
		/// </summary>
		public static List<Cell> ValidCells = new List<Cell>();

		/// <summary>
		/// This is the method called from Otho's conversation. It adds this part to every core Barathrumite in Grit gate as well as populating <see cref="ValidCells"/>.
		/// </summary>
		public static void HitTheBricks()
		{
			Zone gritGateZone = XRLCore.Core.Game.ZoneManager.GetZone(JoppaWorldBuilder.ID_GRIT_GATE);
			ValidCells = gritGateZone.GetZoneFromDirection("D").GetCells((Cell C) => C.IsEmpty() && C.IsReachable() && C.X < 25 && C.Y < 15);
			foreach (GameObject gameObject in gritGateZone.FindObjects((GameObject o) =>
				o.BelongsToFaction("Barathrumites") &&
				o.HasTag("ExcludeFromDynamicEncounters") &&
				(o.GetBlueprint().Inherits == "Barathrumite" || o.Blueprint == "Shem -1")))
			{

				gameObject.Brain.Goals.Clear();
				gameObject.AddPart<Ceres_Sundries_BarathrumiteShelter>().GetToSafety();
			}
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == PooledEvent<AIBoredEvent>.ID || ID == SingletonEvent<GeneralAmnestyEvent>.ID;
		}

		public override bool HandleEvent(AIBoredEvent E)
		{
			return false;
		}

		public override bool WantTurnTick()
		{
			return true;
		}

		public override void TurnTick(long TurnNumber)
		{
			if (ParentObject.CurrentCell != SafePlace)
			{
				if (ParentObject.CurrentZone == SafePlace.ParentZone)
					TeleportToSafeSpot();
				else if (!ParentObject.Brain.HasGoal(nameof(MoveTo)))
					GetToSafety();
			}
		}

		/// <summary>
		/// Attempts to find a safe place. If we have one, then we push a MoveTo goal to get there with appropriate parameters to minimize our risk of getting stuck.
		/// </summary>
		private void GetToSafety()
		{
			if (SafePlace == null)
				FindSafePlace();
			int attempts = 0;
			while (true)
			{
				attempts++;
				if (!SafePlace.IsEmpty() || !SafePlace.IsReachable())
					FindSafePlace();
				else
					break;
				if (attempts >= 10)
				{
					MessageQueue.AddPlayerMessage($"{ParentObject} failed to find a valid spot after 10 attempts.");
					break;
				}
			}
			ParentObject.Brain.PushGoal(new MoveTo(SafePlace, true, true, global: true));
		}

		/// <summary>
		/// Fetches a random element from <see cref="ValidCells"/>. If we find one, it becomes our new destination cell, and is removed from the list.
		/// </summary>
		private void FindSafePlace()
		{
			Cell destination = ValidCells.GetRandomElement();
			if (destination != null)
			{
				SafePlace = destination;
				ValidCells.Remove(destination);
			}
		}

		/// <summary>
		/// Teleports our parent object to their current safe place and sets them to guard it.
		/// We use this once we've entered the study to prevent pathfinding from getting stuck.
		/// </summary>
		internal void TeleportToSafeSpot()
		{
			if (SafePlace == null)
				FindSafePlace();
			if (SafePlace == null)
				Popup.Show($"{ParentObject} failed to find any valid safe spots!!! Report this to the mod author!!!");
			else
			{
				ParentObject.TeleportTo(SafePlace);
				ParentObject.Brain.PushGoal(new Guard());
			}
		}

		/// <summary>
		/// Moves the parent object directly above the study's staircase, then removes this part.
		/// </summary>
		internal void RemoveSelf()
		{
			if (ParentObject.GetPropertyOrTag("ReturnToGritGateAfterAttack").IsNullOrEmpty())
			{
				ParentObject.TeleportTo(ParentObject.CurrentZone.GetZoneFromDirection("U").GetCell(25, 21).getClosestPassableCell(), leaveVerb: string.Empty, arriveVerb: string.Empty);
				ParentObject.Brain.Goals.Clear();
			}
			ParentObject.RemovePart(this);
		}

		public override void Register(GameObject Object, IEventRegistrar Registrar)
		{
			Registrar.Register("CheckZoneSuspend");
		}

		public override bool FireEvent(Event E)
		{
			return !(E.ID == "CheckZoneSuspend") && base.FireEvent(E);
		}
	}
}
