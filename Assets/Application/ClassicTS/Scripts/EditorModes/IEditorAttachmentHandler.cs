using UnityEngine;

namespace ClassicTilestorm
{
	public interface IEditorAttachmentHandler
	{
		void OnSelectionChanged(IMapManager mapManager, Camera camera, MapAttachment[] selection)
		{
			// Default: do nothing
		}

		void OnGizmoInput(IMapManager mapManager, Camera camera, MapAttachment[] selection)
		{
			// Default: do nothing
		}

		void OnDragInput(IMapManager mapManager, MapAttachment[] selection)
		{
			// Default: do nothing
		}
	}
}