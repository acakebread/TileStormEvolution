using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	internal class CellSelectableHandler : IEditorAttachmentHandler
	{
		public static readonly CellSelectableHandler Instance = new();

		public void OnSelect(IMapEdit iMap, Camera camera, ISelectable selection)
		{
			if (selection is not Cell cell) return;

			var tile = iMap.GetTile(cell.tile);
			if (tile.gameObject != null)
				tile.gameObject.SetActive(false);

			var renderPos = Map.WorldToRender(cell.position);

			cell.highlightMesh = EditorSelectionUtil.Create(cell.variant, renderPos);
			if (cell.highlightMesh != null)EditorSelectionUtil.Update( cell.highlightMesh, iMap, cell.position, cell.variant, isSelectedOrDragging: true);

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

			cell.DestroyHighlight();

			EditorDirectionUtil.Hide();
		}

		public bool OnGizmoInput(IMapEdit iMap, Camera camera, ISelectable selection)
		{
			if (selection is not Cell cell) return false;
			if (!EditorDirectionUtil.HandleInput(camera, out Quaternion newWorldRot)) return false;

			cell.variant.angle = newWorldRot.eulerAngles.y;
			iMap.UpdateTileAt(cell.startPosition, cell.variant);

			if (cell.highlightMesh != null) EditorSelectionUtil.Update(cell.highlightMesh, iMap, cell.position, cell.variant, true);

			return true;
		}

		public void OnUpdate(IMapEdit iMap, Camera camera, ISelectable selection)
		{
			if (selection is not Cell cell) return;

			if (cell.highlightMesh != null) EditorSelectionUtil.Update(cell.highlightMesh, iMap, cell.position, cell.variant, true);
			var renderPos = Map.WorldToRender(cell.position);
			var rotation = Quaternion.AngleAxis(cell.variant.angle, Vector3.up);
			EditorDirectionUtil.ShowAt(renderPos, rotation, camera);
		}
	}
}