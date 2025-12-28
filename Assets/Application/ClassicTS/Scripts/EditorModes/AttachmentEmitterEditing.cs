using System.Linq;
using UnityEngine;

namespace ClassicTilestorm
{
	public static class AttachmentEmitterEditing
	{
		public static void OnSelectionChanged(IMapManager mapManager, Camera camera)
		{
			var emitter = (Emitter)AttachmentEditing.selectedAttachments![0];

			var worldPos = MapManager.WorldPosition(emitter.tile, emitter.Position);
			EditorTransformUtil.UpdateTransform(worldPos, emitter.Rotation, camera);

			EditorPrimitiveUtil.UpdateCone(worldPos, emitter.Rotation, emitter.Distance, emitter.Apex);
		}

		public static void OnGizmoInput(IMapManager mapManager, Camera camera)
		{
			var emitter = (Emitter)AttachmentEditing.selectedAttachments![0];

			if (EditorTransformUtil.HandleInput(camera, out Vector3 newWorldPos, out Quaternion newWorldRot))
			{
				emitter.Position = MapManager.LocalPosition(emitter.tile, newWorldPos);
				emitter.Rotation = MapManager.LocalRotation(emitter.tile, newWorldRot);
				mapManager.RefreshAttachmentInstance(emitter);

				EditorPrimitiveUtil.UpdateCone(newWorldPos, emitter.Rotation, emitter.Distance, emitter.Apex);
			}
		}

		public static void OnDragInput(IMapManager mapManager)
		{
			var emitter = (Emitter)AttachmentEditing.selectedAttachments![0];
			var worldPos = MapManager.WorldPosition(emitter.tile, emitter.Position);
			EditorPrimitiveUtil.UpdateCone(worldPos, emitter.Rotation, emitter.Distance, emitter.Apex);
		}

		public static Emitter CreateEmitter(IMapManager mapManager, int tile, string variant)
		{
			if (null == mapManager || string.IsNullOrEmpty(variant)) return null;

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

			// Update the helper to take mapManager
			static float ComputeEmitterPlacementHeight(IMapManager mapManager, int tile)
			{
				if (null == mapManager) return 1f;
				var tileBounds = mapManager.GetTileGeometryBounds(tile);
				var tileWorldCenter = mapManager.TileWorldPosition(tile);
				return (tileBounds.max.y - tileWorldCenter.y) + 0.05f;
			}
		}
	}
}