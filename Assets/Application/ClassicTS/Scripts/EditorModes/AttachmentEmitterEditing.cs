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

			editor.editorController.OnMapEdited();
			editor.SelectAttachments(new MapAttachment[] { emitter });

			// Show gizmo/cone immediately
			OnHandleSelectionChanged(editor);

			return emitter;
		}

		protected override void OnHandleSelectionChanged(EditorControllerAttachment editor)
		{
			var emitter = editor.selectedAttachments?.OfType<Emitter>().FirstOrDefault();
			if (emitter == null) return;

			var worldPos = MapManager.WorldPosition(emitter.tile, emitter.Position);
			EditorTransformUtil.UpdateTransform(worldPos, emitter.Rotation, editor.camera);

			// Show cone: tip at emitter, pointing along rotation, using Distance and Apex
			EditorPrimitiveUtil.UpdateCone(worldPos, emitter.Rotation, emitter.Distance, emitter.Apex);
		}

		protected override void OnHandleGizmoInput(EditorControllerAttachment editor)
		{
			var emitter = editor.selectedAttachments?.OfType<Emitter>().FirstOrDefault();
			if (emitter == null) return;

			if (EditorTransformUtil.HandleInput(editor.camera, out Vector3 newWorldPos, out Quaternion newWorldRot))
			{
				emitter.Position = MapManager.LocalPosition(emitter.tile, newWorldPos);
				emitter.Rotation = MapManager.LocalRotation(emitter.tile, newWorldRot);
				editor.editorController.iMapManager.RefreshAttachmentInstance(emitter);

				// Update cone after transform change
				EditorPrimitiveUtil.UpdateCone(newWorldPos, emitter.Rotation, emitter.Distance, emitter.Apex);
			}
		}

		protected override void OnRefreshDragVisuals(EditorControllerAttachment editor, MapAttachment attachment)
		{
			if (attachment is Emitter emitter)
			{
				var worldPos = MapManager.WorldPosition(emitter.tile, emitter.Position);
				EditorPrimitiveUtil.UpdateCone(worldPos, emitter.Rotation, emitter.Distance, emitter.Apex);
			}
		}
	}
}