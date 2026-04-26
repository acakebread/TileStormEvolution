using UnityEngine;
using System;
using System.Collections.Generic;

namespace ClassicTilestorm
{
	public static class EditorAttachmentDispatcher
	{
		private static readonly Dictionary<Type, IEditorAttachmentHandler> Handlers = new()
		{
			{ typeof(Cell), CellSelectableHandler.Instance },
			{ typeof(Emitter), EmitterAttachmentHandler.Instance },
			{ typeof(View), ViewAttachmentHandler.Instance },
			{ typeof(Pickup), PickupAttachmentHandler.Instance },
			{ typeof(Waypoint), WaypointAttachmentHandler.Instance }
		};

		public static void OnSelect(this ISelectable a, IMapEdit m, Camera c)
		{
			if (Handlers.TryGetValue(a.GetType(), out var h))
				h.OnSelect(m, c, a);
		}

		public static void OnDeselect(this ISelectable a, IMapEdit m, Camera c)
		{
			if (Handlers.TryGetValue(a.GetType(), out var h))
				h.OnDeselect(m, c, a);
		}

		public static void OnUpdate(this ISelectable a, IMapEdit m, Camera c)
		{
			if (Handlers.TryGetValue(a.GetType(), out var h))
				h.OnUpdate(m, c, a);
		}

		public static bool OnGizmoInput(this ISelectable a, IMapEdit m, Camera c)
			=> Handlers.TryGetValue(a.GetType(), out var h) && h.OnGizmoInput(m, c, a);
	}
}