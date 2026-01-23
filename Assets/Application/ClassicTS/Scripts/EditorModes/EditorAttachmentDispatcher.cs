using UnityEngine;
using System;
using System.Collections.Generic;

namespace ClassicTilestorm
{
	public static class EditorAttachmentDispatcher
	{
		private static readonly Dictionary<Type, IEditorAttachmentHandler> Handlers = new()
		{
			{ typeof(Emitter), EmitterAttachmentHandler.Instance },
			{ typeof(View), ViewAttachmentHandler.Instance },
			{ typeof(Pickup), PickupAttachmentHandler.Instance },
			{ typeof(Waypoint), WaypointAttachmentHandler.Instance }
		};

		public static void OnSelectionChanged(this MapAttachment attachment, IMap mapManager, Camera camera, MapAttachment[] selection)
		{
			if (Handlers.TryGetValue(attachment.GetType(), out var handler))
				handler.OnSelectionChanged(mapManager, camera, selection);
		}

		public static void OnGizmoInput(this MapAttachment attachment, IMap mapManager, Camera camera, MapAttachment[] selection)
		{
			if (Handlers.TryGetValue(attachment.GetType(), out var handler))
				handler.OnGizmoInput(mapManager, camera, selection);
		}

		public static void OnDragInput(this MapAttachment attachment, IMap mapManager, MapAttachment[] selection)
		{
			if (Handlers.TryGetValue(attachment.GetType(), out var handler))
				handler.OnDragInput(mapManager, selection);
		}
	}
}