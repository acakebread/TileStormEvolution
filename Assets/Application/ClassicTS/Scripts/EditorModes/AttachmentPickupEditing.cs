using UnityEngine;

namespace ClassicTilestorm
{
	public static class AttachmentPickupEditing
	{
		// Pickup currently has no special gizmo/drag/selection visuals
		public static void OnSelectionChanged(IMapManager mapManager, Camera camera, MapAttachment[] selection) { }
		public static void OnGizmoInput(IMapManager mapManager, Camera camera, MapAttachment[] selection) { }
		public static void OnDragInput(IMapManager mapManager, MapAttachment[] selection) { }

		public static Pickup Create(IMapManager mapManager, int tile)
		{
			if (mapManager == null) return null;

			var pickup = new Pickup
			{
				tile = tile,
				pickupType = 0,
				amount = 1,
				respawn = false
			};

			mapManager.AddAttachment(pickup);
			return pickup;
		}
	}
}