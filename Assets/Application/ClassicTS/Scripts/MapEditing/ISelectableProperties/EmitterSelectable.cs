using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	partial class Emitter : ISelectable, ITransformableAttachment
	{
		public void OnSelect(IMapEdit iMap, Camera camera)
		{
			var worldPos = iMap.WorldPosition(tile, Position);
			EditorTransformUtil.UpdateTransform(worldPos, Rotation, camera);
			EditorPrimitiveUtil.UpdateCone(worldPos, Rotation, Distance, Apex);
		}

		public void OnDeselect(IMapEdit iMap, Camera camera)
		{
			EditorTransformUtil.Hide();
			EditorPrimitiveUtil.Hide();
		}

		public bool OnGizmoInput(IMapEdit iMap, Camera camera)
		{
			if (!EditorTransformUtil.HandleInput(camera, out Vector3 newWorldPos, out Quaternion newWorldRot))
				return false;
			EditorTransformUtil.UpdateTransformGizmoVisuals(camera);

			Position = iMap.LocalPosition(tile, newWorldPos);
			Rotation = iMap.LocalRotation(tile, newWorldRot);
			iMap.RefreshAttachment(this);
			EditorPrimitiveUtil.UpdateCone(newWorldPos, Rotation, Distance, Apex);
			return true;
		}

		public void OnUpdate(IMapEdit iMap, Camera camera)
		{
			var worldPos = iMap.WorldPosition(tile, Position);
			var worldRot = iMap.WorldRotation(tile, Rotation);
			EditorTransformUtil.ShowAt(worldPos, worldRot, camera);
			EditorPrimitiveUtil.UpdateCone(worldPos, Rotation, Distance, Apex);
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
				//var tileBounds = iMap.GetTileGeometryBounds(tile);
				//var tileWorldCenter = iMap.TileRenderPosition(tile);
				//return (tileBounds.max.y - tileWorldCenter.y) + 0.05f;

				//var tileWorldCenter = iMap.TileRenderPosition(tile);
				var tileBounds = iMap.GetTile(tile).GetGeometryBounds();// null != iMap.GetTile(tile).gameObject ? iMap.GetTile(tile).GetGeometryBounds() : new Bounds(tileWorldCenter, Vector3.zero);
				return tileBounds.max.y + 0.05f;// need to get EditorController::editAltitude if no game object//  //return (tileBounds.max.y - tileWorldCenter.y) + 0.05f;
			}
		}
	}
}