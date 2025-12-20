namespace ClassicTilestorm
{
	public class AttachmentPickupEditing : AttachmentEditing
	{
		public static readonly AttachmentPickupEditing Instance = new();

		public Pickup AddNewPickup(EditorControllerAttachment editor, int tile)
		{
			if (null == editor.currentMap) return null;

			var pickup = new Pickup
			{
				tile = tile,
				pickupType = 0,// default type
				amount = 1,
				respawn = false
			};

			editor.iMapManager.AddAttachment(pickup);
			editor.SelectAttachments(new[] { pickup });
			return pickup;
		}
	}
}