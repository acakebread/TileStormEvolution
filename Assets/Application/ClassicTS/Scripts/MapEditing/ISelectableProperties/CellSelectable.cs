using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	partial class Cell : ISelectable
	{
		public void Revert(EditorController controller)
		{
			position = origin;
			Update(controller);
		}

		public void Select(EditorController controller)
		{
			var originalMesh = controller.iMap.GetTile(origin).gameObject;
			if (originalMesh != null) originalMesh.SetActive(false);

			var renderPos = Map.WorldToRender(position);

			highlightMesh = EditorSelectionUtil.Create(variant, renderPos);
			EditorSelectionUtil.Update(highlightMesh, controller.iMap, position, variant, isSelectedOrDragging: true);

			var rotation = Quaternion.AngleAxis(variant.angle, Vector3.up);
			if (controller.IsMultiSelect)
				EditorDirectionUtil.Hide();
			else
				EditorDirectionUtil.ShowAt(renderPos, rotation, controller._camera);
		}

		public void Deselect(EditorController controller)
		{
			var originalMesh = controller.iMap.GetTile(origin).gameObject;
			if (originalMesh != null) originalMesh.SetActive(true);

			controller.iMap.UpdateTileAt(position, variant);

			EditorSelectionUtil.Destroy(highlightMesh);
			highlightMesh = null;

			EditorDirectionUtil.Hide();
		}

		public bool OnGizmoInput(EditorController controller)
		{
			if (!controller.IsMultiSelect)
			{
				if (!EditorDirectionUtil.HandleInput(controller._camera, out Quaternion newWorldRot))
					return false;
				variant.angle = newWorldRot.eulerAngles.y;
			}

			EditorSelectionUtil.Update(highlightMesh, controller.iMap, position, variant, true);
			return true;
		}

		public void Update(EditorController controller)
		{
			var originalMesh = controller.iMap.GetTile(origin).gameObject;
			if (originalMesh != null) originalMesh.SetActive(false);

			EditorSelectionUtil.Update(highlightMesh, controller.iMap, position, variant, true);
			if (controller.IsMultiSelect)
				EditorDirectionUtil.Hide();
			else
			{
				var renderPos = Map.WorldToRender(position);
				var rotation = Quaternion.AngleAxis(variant.angle, Vector3.up);
				EditorDirectionUtil.ShowAt(renderPos, rotation, controller._camera);
			}
		}
	}
}