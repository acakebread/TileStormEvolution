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

		public static void OnSelectionChanged(this ISelectable attachment, IMapEdit iMap, Camera camera)
		{
			if (Handlers.TryGetValue(attachment.GetType(), out var handler))
				handler.OnSelectionChanged(iMap, camera, attachment);
		}

		public static bool OnGizmoInput(this ISelectable attachment, IMapEdit iMap, Camera camera)
		{
			if (Handlers.TryGetValue(attachment.GetType(), out var handler))
				return handler.OnGizmoInput(iMap, camera, attachment);
			return false;
		}

		public static bool OnDragInput(this ISelectable attachment, IMapEdit iMap)
		{
			if (Handlers.TryGetValue(attachment.GetType(), out var handler))
				return handler.OnDragInput(iMap, attachment);
			return false;
		}
	}
}