using UnityEngine;

namespace ClassicTilestorm
{
	public interface IEditorAttachmentHandler
	{
		void OnSelectionChanged(IMapEdit map, Camera camera, ISelectable selection)
		{
			// Default: do nothing
		}

		bool OnGizmoInput(IMapEdit map, Camera camera, ISelectable selection)
		{
			// Default: do nothing
			return false;
		}

		bool OnDragInput(IMapEdit map, ISelectable selection)
		{
			// Default: do nothing
			return false;
		}
	}
}