using UnityEngine;
using UnityEngine.UI;

namespace ClassicTilestorm
{
	internal static class ImguiInputBlocker
	{
		private static int blockerHotControl;

		public static void BlockMouseInput(Rect rect)
		{
			var e = Event.current;
			if (e == null || !e.isMouse || !rect.Contains(e.mousePosition))
				return;

			switch (e.type)
			{
				case EventType.MouseDown:
					if (GUIUtility.hotControl == 0)
					{
						blockerHotControl = GUIUtility.GetControlID(FocusType.Passive);
						GUIUtility.hotControl = blockerHotControl;
					}

					e.Use();
					break;

				case EventType.MouseDrag:
				case EventType.ScrollWheel:
					e.Use();
					break;

				case EventType.MouseUp:
					if (blockerHotControl != 0 && GUIUtility.hotControl == blockerHotControl)
					{
						GUIUtility.hotControl = 0;
						blockerHotControl = 0;
					}

					e.Use();
					break;
			}
		}
	}

	internal sealed class ImguiRaycastBlocker
	{
		private const string BlockerNameSuffix = " Raycast Blocker";
		private readonly string name;
		private GameObject root;
		private RectTransform panelRect;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void CleanupOrphanedBlockers()
		{
			foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
			{
				if (go == null || !go.name.EndsWith(BlockerNameSuffix))
					continue;
				if ((go.hideFlags & HideFlags.HideAndDontSave) == 0)
					continue;

				UnityEngine.Object.Destroy(go);
			}
		}

		public ImguiRaycastBlocker(string name)
		{
			this.name = string.IsNullOrWhiteSpace(name) ? nameof(ImguiRaycastBlocker) : name;
		}

		public void Sync(Rect guiRect)
		{
			EnsureCreated();
			if (root == null || panelRect == null)
				return;

			root.SetActive(true);
			panelRect.anchorMin = new Vector2(0f, 1f);
			panelRect.anchorMax = new Vector2(0f, 1f);
			panelRect.pivot = new Vector2(0f, 1f);
			panelRect.anchoredPosition = new Vector2(guiRect.x, -guiRect.y);
			panelRect.sizeDelta = new Vector2(Mathf.Max(1f, guiRect.width), Mathf.Max(1f, guiRect.height));
		}

		public void SetVisible(bool visible)
		{
			if (root != null)
				root.SetActive(visible);
		}

		public void Destroy()
		{
			if (root != null)
				UnityEngine.Object.Destroy(root);

			root = null;
			panelRect = null;
		}

		private void EnsureCreated()
		{
			if (root != null)
				return;

			root = new GameObject(name, typeof(Canvas), typeof(GraphicRaycaster));
			root.hideFlags = HideFlags.HideAndDontSave;
			root.SetActive(false);
			UnityEngine.Object.DontDestroyOnLoad(root);

			var canvas = root.GetComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = -1000;

			var panel = new GameObject("Raycast Panel", typeof(RectTransform), typeof(Image));
			panel.hideFlags = HideFlags.HideAndDontSave;
			panel.transform.SetParent(root.transform, false);
			panelRect = panel.GetComponent<RectTransform>();
			var image = panel.GetComponent<Image>();
			image.color = new Color(0f, 0f, 0f, 0.75f);
			image.raycastTarget = true;
		}
	}
}
