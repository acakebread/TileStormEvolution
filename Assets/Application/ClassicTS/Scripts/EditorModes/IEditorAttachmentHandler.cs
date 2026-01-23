using UnityEngine;

namespace ClassicTilestorm
{
	public interface IEditorAttachmentHandler
	{
		void OnSelectionChanged(IMapData mapManager, Camera camera, MapAttachment[] selection)
		{
			// Default: do nothing
		}

		void OnGizmoInput(IMapData mapManager, Camera camera, MapAttachment[] selection)
		{
			// Default: do nothing
		}

		void OnDragInput(IMapData mapManager, MapAttachment[] selection)
		{
			// Default: do nothing
		}
	}
}