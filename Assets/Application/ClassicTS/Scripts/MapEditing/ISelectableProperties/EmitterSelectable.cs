using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	partial class Emitter
	{
		public void OnSelect(IMapEdit iMap, Camera camera, ISelectable selection)
		{
			var emitter = (Emitter)selection;
			var worldPos = iMap.WorldPosition(emitter.tile, emitter.Position);
			EditorTransformUtil.UpdateTransform(worldPos, emitter.Rotation, camera);
			EditorPrimitiveUtil.UpdateCone(worldPos, emitter.Rotation, emitter.Distance, emitter.Apex);
		}

		public void OnDeselect(IMapEdit iMap, Camera camera, ISelectable selection)
		{
			EditorTransformUtil.Hide();
			EditorPrimitiveUtil.Hide();
		}

		public bool OnGizmoInput(IMapEdit iMap, Camera camera, ISelectable selection)
		{
			//OnUpdate(iMap, camera, selection);
			if (!EditorTransformUtil.HandleInput(camera, out Vector3 newWorldPos, out Quaternion newWorldRot))
				return false;
			EditorTransformUtil.UpdateTransformGizmoVisuals(camera);

			var emitter = (Emitter)selection;
			emitter.Position = iMap.LocalPosition(emitter.tile, newWorldPos);
			emitter.Rotation = iMap.LocalRotation(emitter.tile, newWorldRot);
			iMap.RefreshAttachment(emitter);
			EditorPrimitiveUtil.UpdateCone(newWorldPos, emitter.Rotation, emitter.Distance, emitter.Apex);
			return true;
		}

		public void OnUpdate(IMapEdit iMap, Camera camera, ISelectable selection)
		{
			var emitter = (Emitter)selection;
			var worldPos = iMap.WorldPosition(emitter.tile, emitter.Position);
			var worldRot = iMap.WorldRotation(emitter.tile, emitter.Rotation);
			EditorTransformUtil.ShowAt(worldPos, worldRot, camera);
			EditorPrimitiveUtil.UpdateCone(worldPos, emitter.Rotation, emitter.Distance, emitter.Apex);
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