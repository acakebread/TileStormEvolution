namespace ClassicTilestorm
{
	internal class PickupAttachmentHandler : IEditorAttachmentHandler
	{
		public static readonly PickupAttachmentHandler Instance = new();

		public static Pickup Create(IMapManager mapManager, int tile)
		{
			if (mapManager == null) return null;

			var pickup = new Pickup
			{
				tile = tile,
				pickupType = 0,
				amount = 1,
				respawn = false
			};

			mapManager.CurrentMap.AddAttachment(pickup);
			return pickup;
		}
	}
}