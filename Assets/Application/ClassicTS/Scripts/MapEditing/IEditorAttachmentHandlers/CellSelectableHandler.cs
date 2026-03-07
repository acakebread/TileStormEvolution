using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	internal class CellSelectableHandler : IEditorAttachmentHandler
	{
		public static readonly CellSelectableHandler Instance = new();

		public void OnSelect(IMapEdit iMap, Camera camera, ISelectable selection)
		{
			var cell = (Cell)selection;
			var tile = iMap.GetTile(cell.tile);
			if (null != tile.gameObject)
			{
				tile.gameObject.SetActive(false);
				var variant = cell.variant(iMap);
				EditorSelectionUtil.UpdateGhostMesh(iMap, iMap.IndexToVector(cell.tile), variant, true);
				EditorDirectionUtil.ShowAt(Map.WorldToRender(iMap.IndexToVector(cell.tile)) + variant.delta, tile.gameObject.transform.rotation, camera);
			}
		}

		public void OnDeselect(IMapEdit iMap, Camera camera, ISelectable selection)
		{
			var cell = (Cell)selection;
			var tile = iMap.GetTile(cell.tile);
			if (null != tile.gameObject)
			{
				tile.gameObject.SetActive(true);
				var variant = cell.variant(iMap);
				variant.angle = EditorDirectionUtil.CurrentRotation;
				iMap.UpdateTileAt(iMap.IndexToVector(cell.tile), variant);
			}
			EditorSelectionUtil.HideGhostMesh();
			EditorDirectionUtil.Hide();
		}

		public bool OnGizmoInput(IMapEdit iMap, Camera camera, ISelectable selection)
		{
			if (!EditorDirectionUtil.HandleInput(camera, out Quaternion newWorldRot))
				return false;

			var cell = (Cell)selection;
			var variant = cell.variant(iMap);
			variant.angle = newWorldRot.eulerAngles.y;
			EditorSelectionUtil.UpdateGhostMesh(variant);
			return true;
		}

		public void OnUpdate(IMapEdit iMap, Camera camera, ISelectable selection)
		{
			var cell = (Cell)selection;
			var variant = cell.variant(iMap);
			var rotation = Quaternion.AngleAxis(EditorSelectionUtil.CurrentRotation, Vector3.up);
			EditorSelectionUtil.UpdateGhostMesh(iMap, cell.position, variant, true);
			EditorDirectionUtil.ShowAt(Map.WorldToRender(cell.position) + variant.delta, rotation, camera);
		}
	}
}