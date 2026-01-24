namespace ClassicTilestorm
{
	internal class PickupAttachmentHandler : IEditorAttachmentHandler
	{
		public static readonly PickupAttachmentHandler Instance = new();

		public static Pickup Create(IMap map, int tile)
		{
			if (map == null) return null;

			var pickup = new Pickup
			{
				tile = tile,
				pickupType = 0,
				amount = 1,
				respawn = false
			};

			map.AddAttachment(pickup);
			return pickup;
		}
	}
}