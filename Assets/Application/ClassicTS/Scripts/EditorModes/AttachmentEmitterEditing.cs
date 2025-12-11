// AttachmentEmitterEditing.cs
using System.Linq;
using UnityEngine;

namespace ClassicTilestorm
{
	public class AttachmentEmitterEditing : AttachmentEditing
	{
		public static readonly AttachmentEmitterEditing Instance = new();

		public override void HandleSelectionChanged(EditorControllerAttachment editor)
		{
			var emitter = editor.selectedAttachments?.OfType<Emitter>().FirstOrDefault();
			if (emitter == null)
			{
				EditorPrimitiveUtil.HideCone();
				return;
			}

			Vector3 worldPos = editor.editorController.iMapManager.TileWorldPosition(emitter.tile) + emitter.Position;
			EditorTransformUtil.UpdateTransform(worldPos, emitter.Rotation, editor.editorCamera);

			// Show cone: tip at emitter, pointing along rotation, using Distance and Apex
			emitter.Distance = 5f;
			EditorPrimitiveUtil.UpdateCone(worldPos, emitter.Rotation, emitter.Distance, emitter.Apex);
		}

		public override void HandleDrag(EditorControllerAttachment editor, MapAttachment attachment)
		{
			if (attachment is Emitter emitter)
			{
				Vector3 worldPos = editor.editorController.iMapManager.TileWorldPosition(emitter.tile) + emitter.Position;
				EditorTransformUtil.ShowAt(worldPos, emitter.Rotation, editor.editorCamera);

				emitter.Distance = 5f;
				EditorPrimitiveUtil.UpdateCone(worldPos, emitter.Rotation, emitter.Distance, emitter.Apex);
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

				// Update cone after transform change
				Vector3 worldPos = editor.editorController.iMapManager.TileWorldPosition(emitter.tile) + emitter.Position;
				emitter.Distance = 5f;
				EditorPrimitiveUtil.UpdateCone(worldPos, emitter.Rotation, emitter.Distance, emitter.Apex);
			}
		}

		// Optional: hide cone when deselected (in base class or controller if needed)
		// Currently handled by HideCone() in HandleSelectionChanged when emitter == null
	}
}