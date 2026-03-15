using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	partial class Cell
	{
		public void OnSelect(IMapEdit iMap, Camera camera)
		{
			var originalMesh = iMap.GetTile(origin).gameObject;
			if (originalMesh != null) originalMesh.SetActive(false);

			var renderPos = Map.WorldToRender(position);

			highlightMesh = EditorSelectionUtil.Create(variant, renderPos);
			EditorSelectionUtil.Update(highlightMesh, iMap, position, variant, isSelectedOrDragging: true);

			var rotation = Quaternion.AngleAxis(variant.angle, Vector3.up);
			EditorDirectionUtil.ShowAt(renderPos, rotation, camera);
		}

		public void OnDeselect(IMapEdit iMap, Camera camera)
		{
			var originalMesh = iMap.GetTile(origin).gameObject;
			if (originalMesh != null) originalMesh.SetActive(true);

			iMap.UpdateTileAt(position, variant);

			EditorSelectionUtil.Destroy(highlightMesh);
			highlightMesh = null;

			EditorDirectionUtil.Hide();
		}

		public bool OnGizmoInput(IMapEdit iMap, Camera camera)
		{
			if (!EditorDirectionUtil.HandleInput(camera, out Quaternion newWorldRot))
				return false;

			variant.angle = newWorldRot.eulerAngles.y;
			EditorSelectionUtil.Update(highlightMesh, iMap, position, variant, true);

			return true;
		}

		public void OnUpdate(IMapEdit iMap, Camera camera)
		{
			EditorSelectionUtil.Update(highlightMesh, iMap, position, variant, true);

			var renderPos = Map.WorldToRender(position);
			var rotation = Quaternion.AngleAxis(variant.angle, Vector3.up);
			EditorDirectionUtil.ShowAt(renderPos, rotation, camera);
		}
	}
}