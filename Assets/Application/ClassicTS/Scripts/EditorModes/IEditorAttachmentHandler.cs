using UnityEngine;

namespace ClassicTilestorm
{
	public interface IEditorAttachmentHandler
	{
		void OnSelectionChanged(IMapEdit map, Camera camera, MapAttachment[] selection)
		{
			// Default: do nothing
		}

		void OnGizmoInput(IMapEdit map, Camera camera, MapAttachment[] selection)
		{
			// Default: do nothing
		}

		void OnDragInput(IMapEdit map, MapAttachment[] selection)
		{
			// Default: do nothing
		}
	}
}