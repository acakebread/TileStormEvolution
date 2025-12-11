// AttachmentEmitterEditing.cs
using System.Linq;
using UnityEngine;

namespace ClassicTilestorm
{
	public class AttachmentEmitterEditing : AttachmentEditing
	{
		public static readonly AttachmentEmitterEditing Instance = new();

		// Override when needed
		public override void HandleSelectionChanged(EditorControllerAttachment editor)
		{
			var emitter = editor.selectedAttachments?.OfType<Emitter>().FirstOrDefault();
			if (emitter == null) return;

			Vector3 worldPos = editor.editorController.iMapManager.TileWorldPosition(emitter.tile) + emitter.Position;
			EditorTransformUtil.ShowAt(worldPos, emitter.Rotation, editor.editorCamera);
		}

		public override void HandleDrag(EditorControllerAttachment editor, MapAttachment attachment)
		{
			if (attachment is Emitter emitter)
			{
				Vector3 worldPos = editor.editorController.iMapManager.TileWorldPosition(emitter.tile) + emitter.Position;
				EditorTransformUtil.ShowAt(worldPos, emitter.Rotation, editor.editorCamera);
			}
		}

		public override void HandleGizmoInput(EditorControllerAttachment editor)
		{
			var emitter = editor.selectedAttachments?.OfType<Emitter>().FirstOrDefault();
			if (emitter == null) return;

			if (EditorTransformUtil.HandleInput(editor.editorCamera, out Vector3 newWorldPos, out Quaternion newWorldRot))
			{
				emitter.Position = newWorldPos - editor.editorController.iMapManager.TileWorldPosition(emitter.tile);
				emitter.Rotation = newWorldRot;
			}
		}
	}
}
