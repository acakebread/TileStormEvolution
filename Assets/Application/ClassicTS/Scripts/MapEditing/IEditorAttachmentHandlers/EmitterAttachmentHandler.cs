using UnityEngine;

namespace ClassicTilestorm
{
	internal class EmitterAttachmentHandler : IEditorAttachmentHandler
	{
		public static readonly EmitterAttachmentHandler Instance = new();

		public void OnSelectionChanged(IMapEdit iMap, Camera camera, MapAttachment[] selection)
		{
			var emitter = (Emitter)selection[0];
			var worldPos = iMap.WorldPosition(emitter.tile, emitter.Position);
			EditorTransformUtil.UpdateTransform(worldPos, emitter.Rotation, camera);
			EditorPrimitiveUtil.UpdateCone(worldPos, emitter.Rotation, emitter.Distance, emitter.Apex);
		}

		public bool OnGizmoInput(IMapEdit iMap, Camera camera, MapAttachment[] selection)
		{
			if (!EditorTransformUtil.HandleInput(camera, out Vector3 newWorldPos, out Quaternion newWorldRot))
				return false;

			var emitter = (Emitter)selection[0];
			emitter.Position = iMap.LocalPosition(emitter.tile, newWorldPos);
			emitter.Rotation = iMap.LocalRotation(emitter.tile, newWorldRot);
			iMap.RefreshAttachment(emitter);
			EditorPrimitiveUtil.UpdateCone(newWorldPos, emitter.Rotation, emitter.Distance, emitter.Apex);
			return true;
		}

		public bool OnDragInput(IMapEdit iMap, MapAttachment[] selection)
		{
			var emitter = (Emitter)selection[0];
			var worldPos = iMap.WorldPosition(emitter.tile, emitter.Position);
			return EditorPrimitiveUtil.UpdateCone(worldPos, emitter.Rotation, emitter.Distance, emitter.Apex);
		}

		public static Emitter Create(IMapEdit iMap, int tile, string variant)
		{
			if (iMap == null || string.IsNullOrEmpty(variant)) return null;

			var localY = ComputeEmitterPlacementHeight(iMap, tile);
			var emitter = new Emitter
			{
				tile = tile,
				Position = new Vector3(0f, localY, 0f),
				LookAt = new Vector3(0f, localY + 1f, 0f),
				variant = variant
			};

			iMap.AddAttachment(emitter);
			return emitter;

			static float ComputeEmitterPlacementHeight(IMapEdit iMap, int tile)
			{
				if (iMap == null) return 1f;
				var tileBounds = iMap.GetTileGeometryBounds(tile);
				var tileWorldCenter = iMap.TileRenderPosition(tile);
				return (tileBounds.max.y - tileWorldCenter.y) + 0.05f;
			}
		}
	}
}