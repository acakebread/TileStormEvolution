// AttachmentEmitterEditing.cs
using System.Linq;
using UnityEngine;

namespace ClassicTilestorm
{
	public class AttachmentEmitterEditing : AttachmentEditing
	{
		public static readonly AttachmentEmitterEditing Instance = new();

		/// <summary>
		/// Creates a new Emitter on the given tile with the specified variant,
		/// adds it to the map, updates runtime visuals, and selects it.
		/// Returns the created emitter, or null on failure.
		/// </summary>
		public Emitter AddNewEmitter(EditorControllerAttachment editor, int tile, string variant)
		{
			var map = editor.editorController?.iMapManager?.CurrentMap;
			if (map == null || string.IsNullOrEmpty(variant)) return null;

			var emitter = new Emitter
			{
				tile = tile,
				Position = Vector3.up,
				LookAt = Vector3.up,
				variant = variant  // Critical: variant must be set BEFORE RefreshEmitterInstance
			};

			map.AddAttachment(emitter);

			// This now creates the GameObject if needed
			editor.editorController.iMapManager.RefreshAttachmentInstance(emitter);

			editor.editorController.OnMapChanged();
			editor.SelectAttachments(new MapAttachment[] { emitter });

			// Show gizmo/cone immediately
			HandleSelectionChanged(editor);

			return emitter;
		}

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
				editor.editorController.iMapManager.RefreshAttachmentInstance(emitter);

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
				editor.editorController.iMapManager.RefreshAttachmentInstance(emitter);

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