using UnityEngine;

namespace ClassicTilestorm
{
	public interface IEditorAttachmentHandler
	{
		void OnSelect(IMapEdit map, Camera camera, ISelectable selection) { } // Default: do nothing
		void OnDeselect(ISelectable selection) { } // Default: do nothing
		bool OnGizmoInput(IMapEdit map, Camera camera, ISelectable selection) => false; // Default: return false
		bool OnDragInput(IMapEdit map, Camera camera, ISelectable selection) => false; // Default: return false
	}
}