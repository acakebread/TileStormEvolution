using UnityEngine;

namespace ClassicTilestorm
{
	internal class EmitterAttachmentHandler : IEditorAttachmentHandler
	{
		public static readonly EmitterAttachmentHandler Instance = new();

		public void OnSelectionChanged(IMap map, Camera camera, MapAttachment[] selection)
		{
			var emitter = (Emitter)selection[0];
			var worldPos = map.WorldPosition(emitter.tile, emitter.Position);
			EditorTransformUtil.UpdateTransform(worldPos, emitter.Rotation, camera);
			EditorPrimitiveUtil.UpdateCone(worldPos, emitter.Rotation, emitter.Distance, emitter.Apex);
		}

		public void OnGizmoInput(IMap map, Camera camera, MapAttachment[] selection)
		{
			var emitter = (Emitter)selection[0];

			if (EditorTransformUtil.HandleInput(camera, out Vector3 newWorldPos, out Quaternion newWorldRot))
			{
				emitter.Position = map.LocalPosition(emitter.tile, newWorldPos);
				emitter.Rotation = map.LocalRotation(emitter.tile, newWorldRot);
				map.RefreshAttachmentInstance(emitter);
				EditorPrimitiveUtil.UpdateCone(newWorldPos, emitter.Rotation, emitter.Distance, emitter.Apex);
			}
		}

		public void OnDragInput(IMap map, MapAttachment[] selection)
		{
			var emitter = (Emitter)selection[0];
			var worldPos = map.WorldPosition(emitter.tile, emitter.Position);
			EditorPrimitiveUtil.UpdateCone(worldPos, emitter.Rotation, emitter.Distance, emitter.Apex);
		}

		public static Emitter Create(IMap map, int tile, string variant)
		{
			if (map == null || string.IsNullOrEmpty(variant)) return null;

			var localY = ComputeEmitterPlacementHeight(map, tile);
			var emitter = new Emitter
			{
				tile = tile,
				Position = new Vector3(0f, localY, 0f),
				LookAt = new Vector3(0f, localY + 1f, 0f),
				variant = variant
			};

			map.AddAttachment(emitter);
			return emitter;

			static float ComputeEmitterPlacementHeight(IMap mapManager, int tile)
			{
				if (mapManager == null) return 1f;
				var tileBounds = mapManager.GetTileGeometryBounds(tile);
				var tileWorldCenter = mapManager.TileWorldPosition(tile);
				return (tileBounds.max.y - tileWorldCenter.y) + 0.05f;
			}
		}
	}
}