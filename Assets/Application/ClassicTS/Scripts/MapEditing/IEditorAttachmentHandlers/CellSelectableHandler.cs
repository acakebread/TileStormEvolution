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

			cell.originalMesh = iMap.GetTile(cell.origin).gameObject;
			if (null != cell.originalMesh) cell.originalMesh.SetActive(false);

			var renderPos = Map.WorldToRender(cell.position);

			cell.highlightMesh = EditorSelectionUtil.Create(cell.variant, renderPos);
			EditorSelectionUtil.Update( cell.highlightMesh, iMap, cell.position, cell.variant, isSelectedOrDragging: true);

			var rotation = Quaternion.AngleAxis(cell.variant.angle, Vector3.up);
			EditorDirectionUtil.ShowAt(renderPos, rotation, camera);
		}

		public void OnDeselect(IMapEdit iMap, Camera camera, ISelectable selection)
		{
			if (selection is not Cell cell) return;

			if (null != cell.originalMesh) cell.originalMesh.SetActive(true);

			iMap.UpdateTileAt(cell.origin, cell.variant, false);

			cell.DestroyHighlight();

			EditorDirectionUtil.Hide();
		}

		public bool OnGizmoInput(IMapEdit iMap, Camera camera, ISelectable selection)
		{
			if (selection is not Cell cell) return false;
			if (!EditorDirectionUtil.HandleInput(camera, out Quaternion newWorldRot)) return false;

			cell.variant.angle = newWorldRot.eulerAngles.y;
			EditorSelectionUtil.Update(cell.highlightMesh, iMap, cell.position, cell.variant, true);

			return true;
		}

		public void OnUpdate(IMapEdit iMap, Camera camera, ISelectable selection)
		{
			if (selection is not Cell cell) return;

			EditorSelectionUtil.Update(cell.highlightMesh, iMap, cell.position, cell.variant, true);
			var renderPos = Map.WorldToRender(cell.position);
			var rotation = Quaternion.AngleAxis(cell.variant.angle, Vector3.up);
			EditorDirectionUtil.ShowAt(renderPos, rotation, camera);
		}
	}
}