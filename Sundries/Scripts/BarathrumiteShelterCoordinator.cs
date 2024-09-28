using XRL.World.Parts;

namespace XRL.World.ZoneParts
{
	/// <summary>
	/// This zone part is attached to the Grit Gate zone when the Barathrumites are sheltering during A Call to Arms.
	/// <c><see cref="GeneralAmnestyEvent"/></c> doesn't seem to cleanly extend across zones, so this is the compromise;
	/// we catch it here and then just dispatch a command to all of the sheltering characters on the zone below.
	/// </summary>
	public class Ceres_Sundries_BarathrumiteShelterCoordinator : IZonePart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == GeneralAmnestyEvent.ID;
		}

		public override bool HandleEvent(GeneralAmnestyEvent E)
		{
			foreach (GameObject go in ParentZone.GetZoneFromDirection("D").GetObjectsWithPart(nameof(Ceres_Sundries_BarathrumiteShelter)))
			{
				var shelter = go.GetPart<Ceres_Sundries_BarathrumiteShelter>();
				shelter.AllClear();
			}
			ParentZone.RemovePart(this);
			return base.HandleEvent(E);
		}
	}
}
