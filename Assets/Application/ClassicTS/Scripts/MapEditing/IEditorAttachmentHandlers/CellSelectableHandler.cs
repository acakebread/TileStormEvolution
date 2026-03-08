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
				var rotation = Quaternion.AngleAxis(cell.variant.angle, Vector3.up);
				EditorSelectionUtil.UpdateGhostMesh(iMap, cell.position, cell.variant, true);
				EditorDirectionUtil.ShowAt(Map.WorldToRender(cell.position), rotation, camera);
			}
		}

		public void OnDeselect(IMapEdit iMap, Camera camera, ISelectable selection)
		{
			var cell = (Cell)selection;
			var tile = iMap.GetTile(cell.tile);
			if (null != tile.gameObject) tile.gameObject.SetActive(true);

			//ToDo detect no change and skip
			//cell.position = snappedWorld + new Vector3(cell.variant.delta.x, 0f, cell.variant.delta.z);
			//if (cell.position == cell.startPosition) return;//unchanged - do not alter map

			iMap.UpdateTileAt(cell.startPosition, cell.variant);//apply the changes
			EditorSelectionUtil.HideGhostMesh();
			EditorDirectionUtil.Hide();
		}

		public bool OnGizmoInput(IMapEdit iMap, Camera camera, ISelectable selection)
		{
			if (!EditorDirectionUtil.HandleInput(camera, out Quaternion newWorldRot))
				return false;

			var cell = (Cell)selection;
			cell.variant.angle = newWorldRot.eulerAngles.y;
			iMap.UpdateTileAt(cell.startPosition, cell.variant);//apply the rotation to original tile variant
			EditorSelectionUtil.UpdateGhostMesh(cell.variant);
			return true;
		}

		public void OnUpdate(IMapEdit iMap, Camera camera, ISelectable selection)
		{
			var cell = (Cell)selection;
			var position = new Vector3(cell.position.x, 0f, cell.position.z);	
			var rotation = Quaternion.AngleAxis(cell.variant.angle, Vector3.up);
			EditorSelectionUtil.UpdateGhostMesh(iMap, cell.position, cell.variant, true);
			EditorDirectionUtil.ShowAt(Map.WorldToRender(cell.position), rotation, camera);
		}
	}
}