using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	partial class Cell : ISelectable
	{
		public void Revert(EditorController controller)
		{
			offset = Vector3.zero;
			Update(controller);
		}

		public void Select(EditorController controller)
		{
			Update(controller);
		}

		public void Deselect(EditorController controller)
		{
			var originalMesh = controller.iMap.GetTile(origin).gameObject;
			if (originalMesh != null) originalMesh.SetActive(true);

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

			var renderPos = Map.WorldToRender(position);
			if (null == highlightMesh)
				highlightMesh = EditorSelectionUtil.Create(variant, renderPos);

			EditorSelectionUtil.Update(highlightMesh, controller.iMap, position, variant, true);
			if (controller.IsMultiSelect)
				EditorDirectionUtil.Hide();
			else
			{
				var rotation = Quaternion.AngleAxis(variant.angle, Vector3.up);
				EditorDirectionUtil.ShowAt(renderPos, rotation, controller._camera);
			}
		}
	}
}