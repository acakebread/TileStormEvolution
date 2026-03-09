using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	internal class CellSelectableHandler : IEditorAttachmentHandler
	{
		public static readonly CellSelectableHandler Instance = new();

		//public void OnSelect(IMapEdit iMap, Camera camera, ISelectable selection)
		//{
		//	var cell = (Cell)selection;
		//	var tile = iMap.GetTile(cell.tile);
		//	if (null != tile.gameObject)
		//	{
		//		tile.gameObject.SetActive(false);
		//		var rotation = Quaternion.AngleAxis(cell.variant.angle, Vector3.up);
		//		GhostMeshUtil.UpdateGhostMesh(iMap, cell.position, cell.variant, true);
		//		EditorDirectionUtil.ShowAt(Map.WorldToRender(cell.position), rotation, camera);
		//	}
		//}

		//public void OnDeselect(IMapEdit iMap, Camera camera, ISelectable selection)
		//{
		//	var cell = (Cell)selection;
		//	var tile = iMap.GetTile(cell.tile);
		//	if (null != tile.gameObject) tile.gameObject.SetActive(true);

		//	//ToDo detect no change and skip
		//	//cell.position = snappedWorld + new Vector3(cell.variant.delta.x, 0f, cell.variant.delta.z);
		//	//if (cell.position == cell.startPosition) return;//unchanged - do not alter map

		//	iMap.UpdateTileAt(cell.startPosition, cell.variant);//apply the changes
		//	GhostMeshUtil.HideGhostMesh();
		//	EditorDirectionUtil.Hide();
		//}

		//public bool OnGizmoInput(IMapEdit iMap, Camera camera, ISelectable selection)
		//{
		//	if (!EditorDirectionUtil.HandleInput(camera, out Quaternion newWorldRot))
		//		return false;

		//	var cell = (Cell)selection;
		//	cell.variant.angle = newWorldRot.eulerAngles.y;
		//	iMap.UpdateTileAt(cell.startPosition, cell.variant);//apply the rotation to original tile variant
		//	GhostMeshUtil.UpdateGhostMesh(cell.variant);
		//	return true;
		//}

		//public void OnUpdate(IMapEdit iMap, Camera camera, ISelectable selection)
		//{
		//	var cell = (Cell)selection;
		//	var position = new Vector3(cell.position.x, 0f, cell.position.z);	
		//	var rotation = Quaternion.AngleAxis(cell.variant.angle, Vector3.up);
		//	GhostMeshUtil.UpdateGhostMesh(iMap, cell.position, cell.variant, true);
		//	EditorDirectionUtil.ShowAt(Map.WorldToRender(cell.position), rotation, camera);
		//}

		public void OnSelect(IMapEdit iMap, Camera camera, ISelectable selection)
		{
			if (selection is not Cell cell) return;

			var tile = iMap.GetTile(cell.tile);
			if (tile.gameObject != null)
				tile.gameObject.SetActive(false);

			var renderPos = Map.WorldToRender(cell.position);

			// Create using Variant
			cell.highlightMesh = EditorSelectionUtil.Create(cell.variant, renderPos);

			// Immediately apply selected look + position check
			if (cell.highlightMesh != null)
			{
				EditorSelectionUtil.Update(
					cell.highlightMesh,
					iMap,
					cell.position,
					cell.variant,
					isSelectedOrDragging: true);
			}

			var rotation = Quaternion.AngleAxis(cell.variant.angle, Vector3.up);
			EditorDirectionUtil.ShowAt(renderPos, rotation, camera);
		}

		public void OnDeselect(IMapEdit iMap, Camera camera, ISelectable selection)
		{
			if (selection is not Cell cell) return;

			var tile = iMap.GetTile(cell.tile);
			if (tile.gameObject != null)
				tile.gameObject.SetActive(true);

			iMap.UpdateTileAt(cell.startPosition, cell.variant);

			cell.DestroyHighlight();   // ← your helper or just EditorSelectionUtil.Destroy(cell.highlightMesh); cell.highlightMesh = null;

			EditorDirectionUtil.Hide();
		}

		public bool OnGizmoInput(IMapEdit iMap, Camera camera, ISelectable selection)
		{
			if (selection is not Cell cell) return false;
			if (!EditorDirectionUtil.HandleInput(camera, out Quaternion newWorldRot)) return false;

			cell.variant.angle = newWorldRot.eulerAngles.y;
			iMap.UpdateTileAt(cell.startPosition, cell.variant);

			if (cell.highlightMesh != null)
			{
				EditorSelectionUtil.Update(
					cell.highlightMesh,
					iMap,
					cell.position,
					cell.variant,
					true);
			}

			return true;
		}

		public void OnUpdate(IMapEdit iMap, Camera camera, ISelectable selection)
		{
			if (selection is not Cell cell) return;

			if (cell.highlightMesh != null)
			{
				EditorSelectionUtil.Update(
					cell.highlightMesh,
					iMap,
					cell.position,
					cell.variant,
					true);
			}

			var renderPos = Map.WorldToRender(cell.position);
			var rotation = Quaternion.AngleAxis(cell.variant.angle, Vector3.up);
			EditorDirectionUtil.ShowAt(renderPos, rotation, camera);
		}
	}
}