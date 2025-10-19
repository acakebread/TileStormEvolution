using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class GUIManager
	{
		private static List<Rect> guiRects = new List<Rect>();
		private static bool isMouseOverAnyGui = false;

		// Call from each OnGUI method to register GUI areas
		public static void RegisterGuiRect(Rect rect)
		{
			guiRects.Add(rect);
		}

		// Call at the start of each frame's OnGUI to reset
		public static void ResetGuiState()
		{
			guiRects.Clear();
			isMouseOverAnyGui = false;
		}

		// Check if mouse is over any registered GUI rect
		public static bool IsMouseOverGui()
		{
			if (GUIUtility.hotControl != 0)
				return true;

			Vector2 mousePos = Event.current?.mousePosition ?? Input.mousePosition;
			foreach (var rect in guiRects)
			{
				if (rect.Contains(mousePos))
				{
					isMouseOverAnyGui = true;
					return true;
				}
			}
			return isMouseOverAnyGui;
		}
	}
}