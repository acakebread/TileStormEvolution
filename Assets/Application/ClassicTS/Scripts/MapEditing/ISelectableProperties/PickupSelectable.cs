namespace ClassicTilestorm
{
	partial class Pickup : ISelectable
	{
		public static Pickup Create(IMapEdit map, int tile)
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