namespace ClassicTilestorm
{
	public class AttachmentPickupEditing : AttachmentEditing
	{
		public static readonly AttachmentPickupEditing Instance = new();

		public Pickup CreatePickup(IMapManager mapManager, int tile)
		{
			if (mapManager == null) return null;

			var pickup = new Pickup
			{
				tile = tile,
				pickupType = 0,
				amount = 1,
				respawn = false
			};

			mapManager.AddAttachment(pickup);
			return pickup;
		}
	}
}