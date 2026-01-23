using UnityEngine;

namespace ClassicTilestorm
{
	public interface IEditorAttachmentHandler
	{
		void OnSelectionChanged(IMap map, Camera camera, MapAttachment[] selection)
		{
			// Default: do nothing
		}

		void OnGizmoInput(IMap map, Camera camera, MapAttachment[] selection)
		{
			// Default: do nothing
		}

		void OnDragInput(IMap map, MapAttachment[] selection)
		{
			// Default: do nothing
		}
	}
}