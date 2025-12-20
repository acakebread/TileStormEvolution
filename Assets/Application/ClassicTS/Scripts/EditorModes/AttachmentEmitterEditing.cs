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
			if (editor.currentMap == null || editor.iMapManager == null || string.IsNullOrEmpty(variant)) return null;

			var localY = ComputeEmitterPlacementHeight(editor, tile);
			var emitter = new Emitter
			{
				tile = tile,
				Position = new Vector3(0f, localY, 0f),
				LookAt = new Vector3(0f, localY + 1f, 0f),
				variant = variant
			};

			editor.iMapManager.AddAttachment(emitter);
			editor.SelectAttachments(new MapAttachment[] { emitter });
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
				editor.iMapManager.RefreshAttachmentInstance(emitter);

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

		/// <summary>
		/// Computes the ideal local Y position for placing an emitter on top of the tile geometry.
		/// Adds a small offset to prevent z-fighting.
		/// </summary>
		private static float ComputeEmitterPlacementHeight(EditorControllerAttachment editor, int tile)
		{
			if (editor.iMapManager == null) return 1f;

			// Get accurate geometry bounds from MapManager
			var tileBounds = editor.iMapManager.GetTileGeometryBounds(tile);
			var tileWorldCenter = editor.iMapManager.TileWorldPosition(tile);
			var topYWorld = tileBounds.max.y;

			// Convert to local space and add small lift
			return (topYWorld - tileWorldCenter.y) + 0.05f;
		}
	}
}