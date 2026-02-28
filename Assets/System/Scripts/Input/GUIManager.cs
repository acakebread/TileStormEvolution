//using UnityEngine;
//using System.Collections.Generic;

//namespace MassiveHadronLtd
//{
//	public static class GUIManager
//	{
//		private static List<Rect> guiRects = new List<Rect>();

//		public static void RegisterGuiRect(Rect rect) => guiRects.Add(rect);

//		public static void ResetGuiState() => guiRects.Clear();

//		public static bool IsMouseOverGui()
//		{
//			if (GUIUtility.hotControl != 0) return true;
//			Vector2 mousePos = InputX.mousePosition;
//			mousePos.y = Screen.height - mousePos.y; // Convert to GUI coordinates
//			foreach (var rect in guiRects)
//			{
//				if (rect.Contains(mousePos)) return true;
//			}
//			return false;
//		}
//	}
//}