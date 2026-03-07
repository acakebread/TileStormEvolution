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
				var variant = iMap.GetVariantAt(cell.tile);
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
				var variant = iMap.GetVariantAt(cell.tile);
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

			var variant = EditorSelectionUtil.CurrentVariant;
			variant.angle = newWorldRot.eulerAngles.y;
			EditorSelectionUtil.UpdateGhostMesh(variant);
			return true;
		}

		public void OnUpdate(IMapEdit iMap, Camera camera, ISelectable selection)
		{
			var cell = (Cell)selection;
			var variant = iMap.GetVariantAt(cell.tile);
			var rotation = Quaternion.AngleAxis(EditorSelectionUtil.CurrentRotation, Vector3.up);
			//EditorSelectionUtil.UpdateGhostMesh(iMap, iMap.IndexToVector(cell.tile) + cell.position, variant, true);
			EditorSelectionUtil.UpdateGhostMesh(iMap, cell.position, variant, true);
			EditorDirectionUtil.ShowAt(EditorSelectionUtil.CurrentPosition + variant.delta, rotation, camera);
		}
	}
}