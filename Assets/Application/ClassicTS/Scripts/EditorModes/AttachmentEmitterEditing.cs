using System.Linq;
using UnityEngine;

namespace ClassicTilestorm
{
	public class AttachmentEmitterEditing : AttachmentEditing
	{
		public static readonly AttachmentEmitterEditing Instance = new();

		public Emitter CreateEmitter(IMapManager mapManager, int tile, string variant)
		{
			if (mapManager == null || string.IsNullOrEmpty(variant)) return null;

			var localY = ComputeEmitterPlacementHeight(mapManager, tile);
			var emitter = new Emitter
			{
				tile = tile,
				Position = new Vector3(0f, localY, 0f),
				LookAt = new Vector3(0f, localY + 1f, 0f),
				variant = variant
			};

			mapManager.AddAttachment(emitter);
			return emitter;
		}

		protected override void OnHandleSelectionChanged(IMapManager mapManager, Camera camera)
		{
			var emitter = selectedAttachments?.OfType<Emitter>().FirstOrDefault();
			if (emitter == null) return;

			var worldPos = MapManager.WorldPosition(emitter.tile, emitter.Position);
			EditorTransformUtil.UpdateTransform(worldPos, emitter.Rotation, camera);

			EditorPrimitiveUtil.UpdateCone(worldPos, emitter.Rotation, emitter.Distance, emitter.Apex);
		}

		protected override void OnHandleGizmoInput(IMapManager mapManager, Camera camera)
		{
			var emitter = selectedAttachments?.OfType<Emitter>().FirstOrDefault();
			if (emitter == null) return;

			if (EditorTransformUtil.HandleInput(camera, out Vector3 newWorldPos, out Quaternion newWorldRot))
			{
				emitter.Position = MapManager.LocalPosition(emitter.tile, newWorldPos);
				emitter.Rotation = MapManager.LocalRotation(emitter.tile, newWorldRot);
				mapManager.RefreshAttachmentInstance(emitter);

				EditorPrimitiveUtil.UpdateCone(newWorldPos, emitter.Rotation, emitter.Distance, emitter.Apex);
			}
		}

		protected override void OnRefreshDragVisuals(IMapManager mapManager, MapAttachment attachment)
		{
			if (attachment is Emitter emitter)
			{
				var worldPos = MapManager.WorldPosition(emitter.tile, emitter.Position);
				EditorPrimitiveUtil.UpdateCone(worldPos, emitter.Rotation, emitter.Distance, emitter.Apex);
			}
		}

		// Update the helper to take mapManager
		private static float ComputeEmitterPlacementHeight(IMapManager mapManager, int tile)
		{
			if (mapManager == null) return 1f;
			var tileBounds = mapManager.GetTileGeometryBounds(tile);
			var tileWorldCenter = mapManager.TileWorldPosition(tile);
			return (tileBounds.max.y - tileWorldCenter.y) + 0.05f;
		}
	}
}