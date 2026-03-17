using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	partial class Emitter : ISelectable, ITransformableAttachment
	{
        public void Select(EditorController controller)
        {
			if (controller.IsMultiSelect)
			{
				Deselect(controller);
				return;
			}
			var worldPos = controller.iMap.WorldPosition(tile, Position);
            EditorTransformUtil.UpdateTransform(worldPos, Rotation, controller._camera);
            EditorPrimitiveUtil.UpdateCone(worldPos, Rotation, Distance, Apex);
        }

        public void Deselect(EditorController controller)
        {
            EditorTransformUtil.Hide();
            EditorPrimitiveUtil.Hide();
        }

        public bool OnGizmoInput(EditorController controller)
        {
            if (!EditorTransformUtil.HandleInput(controller._camera, out Vector3 newWorldPos, out Quaternion newWorldRot))
                return false;

            EditorTransformUtil.UpdateTransformGizmoVisuals(controller._camera);

            Position = controller.iMap.LocalPosition(tile, newWorldPos);
            Rotation = controller.iMap.LocalRotation(tile, newWorldRot);
            controller.iMap.RefreshAttachment(this);

            EditorPrimitiveUtil.UpdateCone(newWorldPos, Rotation, Distance, Apex);
            return true;
        }

        public void Update(EditorController controller)
        {
			if (controller.IsMultiSelect)
			{
				Deselect(controller);
				return;
			}
			var worldPos = controller.iMap.WorldPosition(tile, Position);
            var worldRot = controller.iMap.WorldRotation(tile, Rotation);
            EditorTransformUtil.ShowAt(worldPos, worldRot, controller._camera);
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