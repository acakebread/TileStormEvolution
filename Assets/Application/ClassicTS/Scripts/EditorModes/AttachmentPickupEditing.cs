namespace ClassicTilestorm
{
	public class AttachmentPickupEditing : AttachmentEditing
	{
		public static readonly AttachmentPickupEditing Instance = new();

		public Pickup AddNewPickup(EditorControllerAttachment editor, int tile)
		{
			var map = editor.editorController?.iMapManager?.CurrentMap;
			if (map == null) return null;

			var pickup = new Pickup
			{
				tile = tile,
				pickupType = 0,// default type
				amount = 1,
				respawn = false
			};

			map.AddAttachment(pickup);

			editor.editorController.iMapManager.RefreshAttachmentInstance(pickup);

			editor.editorController.OnMapEdited();
			editor.SelectAttachments(new[] { pickup });

			return pickup;
		}
	}
}