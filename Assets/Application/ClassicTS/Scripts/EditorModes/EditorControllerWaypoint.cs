using UnityEngine;
using UnityEngine.EventSystems;

namespace ClassicTilestorm
{
    public class EditorControllerWaypoint : EditorControllerMovement
    {
		public EditorControllerWaypoint(EditorController editorController) : base(editorController) { }

		public override void Update()
		{
			base.Update();
			if (!camera || editorController.GetEditorUI().IsGuiControlActive() || EventSystem.current.IsPointerOverGameObject()) return;

		}
	}
}