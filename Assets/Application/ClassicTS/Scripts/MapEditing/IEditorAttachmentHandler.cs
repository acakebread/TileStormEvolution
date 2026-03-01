using UnityEngine;

namespace ClassicTilestorm
{
	public interface IEditorAttachmentHandler
	{
		void OnSelectionChanged(IMapEdit map, Camera camera, MapAttachment[] selection)
		{
			// Default: do nothing
		}

		bool OnGizmoInput(IMapEdit map, Camera camera, MapAttachment[] selection)
		{
			// Default: do nothing
			return false;
		}

		bool OnDragInput(IMapEdit map, MapAttachment[] selection)
		{
			// Default: do nothing
			return false;
		}
	}
}