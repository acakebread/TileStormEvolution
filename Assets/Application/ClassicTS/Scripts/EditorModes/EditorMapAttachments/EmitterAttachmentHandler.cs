using UnityEngine;

namespace ClassicTilestorm
{
	internal class EmitterAttachmentHandler : IEditorAttachmentHandler
	{
		public static readonly EmitterAttachmentHandler Instance = new();

		public void OnSelectionChanged(IMapManager mapManager, Camera camera, MapAttachment[] selection)
		{
			var emitter = (Emitter)selection[0];
			var worldPos = MapManager.WorldPosition(emitter.tile, emitter.Position);
			EditorTransformUtil.UpdateTransform(worldPos, emitter.Rotation, camera);
			EditorPrimitiveUtil.UpdateCone(worldPos, emitter.Rotation, emitter.Distance, emitter.Apex);
		}

		public void OnGizmoInput(IMapManager mapManager, Camera camera, MapAttachment[] selection)
		{
			var emitter = (Emitter)selection[0];

			if (EditorTransformUtil.HandleInput(camera, out Vector3 newWorldPos, out Quaternion newWorldRot))
			{
				emitter.Position = MapManager.LocalPosition(emitter.tile, newWorldPos);
				emitter.Rotation = MapManager.LocalRotation(emitter.tile, newWorldRot);
				mapManager.RefreshAttachmentInstance(emitter);
				EditorPrimitiveUtil.UpdateCone(newWorldPos, emitter.Rotation, emitter.Distance, emitter.Apex);
			}
		}

		public void OnDragInput(IMapManager mapManager, MapAttachment[] selection)
		{
			var emitter = (Emitter)selection[0];
			var worldPos = MapManager.WorldPosition(emitter.tile, emitter.Position);
			EditorPrimitiveUtil.UpdateCone(worldPos, emitter.Rotation, emitter.Distance, emitter.Apex);
		}

		public static Emitter Create(IMapManager mapManager, int tile, string variant)
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

			mapManager.CurrentMap.AddAttachment(emitter);
			return emitter;

			static float ComputeEmitterPlacementHeight(IMapManager mapManager, int tile)
			{
				if (mapManager == null) return 1f;
				var tileBounds = mapManager.CurrentMap.GetTileGeometryBounds(tile);
				var tileWorldCenter = mapManager.CurrentMap.TileWorldPosition(tile);
				return (tileBounds.max.y - tileWorldCenter.y) + 0.05f;
			}
		}
	}
}