using UnityEngine;
using System.Linq;

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
				pickupType = 0,  // default type
				amount = 1,
				respawn = false
			};

			map.AddAttachment(pickup);

			editor.editorController.iMapManager.RefreshAttachmentInstance(pickup);

			editor.editorController.OnMapChanged();
			editor.SelectAttachments(new[] { pickup });

			// Optional: show gizmo if you want to position pickups
			OnHandleSelectionChanged(editor);

			return pickup;
		}

		protected override void OnHandleSelectionChanged(EditorControllerAttachment editor)
		{
			var pickup = editor.selectedAttachments?.OfType<Pickup>().FirstOrDefault();
			if (pickup == null) return;

			Vector3 worldPos = editor.editorController.iMapManager.TileWorldPosition(pickup.tile) + Vector3.up * 0.5f;
			EditorTransformUtil.ShowAt(worldPos, Quaternion.identity, editor.editorCamera);
		}

		protected override void OnHandleGizmoInput(EditorControllerAttachment editor)
		{
			var pickup = editor.selectedAttachments?.OfType<Pickup>().FirstOrDefault();
			if (pickup == null) return;

			if (EditorTransformUtil.HandleInput(editor.editorCamera, out Vector3 newWorldPos, out Quaternion newRot))
			{
				// Optional: allow positioning pickups off-center
				// For now, keep them fixed above tile center
				// Or store offset if you add a Position field later
			}
		}

		//	public override void HandleSelectionChanged(EditorControllerAttachment editor) { }
		//	public override void HandleDrag(EditorControllerAttachment editor, MapAttachment attachment) { }
		//	protected override void DrawTypeSpecificGUI(EditorControllerAttachment editor) { }
	}
}