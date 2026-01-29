//using UnityEngine;

//namespace ClassicTilestorm
//{
//	public static class LayerUtility
//	{
//		public const string PREVIEW_LAYER_NAME = "Preview";
//		private static int previewLayer = -1;

//		public static void EnsurePreviewLayer()
//		{
//			if (previewLayer >= 0) return;

//			for (int i = 8; i < 32; i++) // typical user layers start from 8
//			{
//				string name = LayerMask.LayerToName(i);
//				if (name == PREVIEW_LAYER_NAME)
//				{
//					previewLayer = i;
//					return;
//				}
//			}

//			Debug.LogWarning($"Layer '{PREVIEW_LAYER_NAME}' not found. Please add it in Project Settings → Tags and Layers.");
//			previewLayer = 0; // fallback – will be visible everywhere (bad)
//		}
//	}
//}