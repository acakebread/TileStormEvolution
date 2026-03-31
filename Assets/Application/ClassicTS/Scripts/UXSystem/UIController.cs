using UnityEngine;
using System;
using System.Collections.Generic;

namespace ClassicTilestorm
{
	public class UIController : MonoBehaviour
	{
		public static UIController Instance { get; private set; }

		[Header("UI Setup")]
		[SerializeField] private Canvas mainCanvas;

		[Header("Panel Prefabs – drag all panel prefabs here")]
		[SerializeField] private List<GameObject> panelPrefabs = new();

		private readonly Dictionary<Type, GameObject> prefabByType = new();
		private readonly List<GameObject> activePanels = new List<GameObject>();
		private UIPanel currentTopPanel;

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}

			Instance = this;
			DontDestroyOnLoad(gameObject);

			prefabByType.Clear();
			foreach (var prefab in panelPrefabs)
			{
				if (prefab == null) continue;

				var panel = prefab.GetComponent<UIPanel>();
				if (panel == null) continue;

				var type = panel.GetType();
				if (!prefabByType.ContainsKey(type))
					prefabByType[type] = prefab;
			}
		}

		public static T OpenPanel<T>(bool closeOthers = true) where T : UIPanel
		{
			if (Instance == null) return null;
			return Instance.OpenPanelInternal<T>(typeof(T), closeOthers);
		}

		public static T GetOpenPanel<T>() where T : UIPanel
		{
			if (Instance == null) return null;
			return Instance.GetOpenPanelInternal<T>();
		}

		public static void CloseCurrent() => Instance?.CloseTopPanelInternal();
		public static void ClosePanel<T>() where T : UIPanel => Instance?.ClosePanelInternal(typeof(T));

		private T OpenPanelInternal<T>(Type panelType, bool requestedCloseOthers) where T : UIPanel
		{
			bool effectiveCloseOthers = requestedCloseOthers && IsPanelTypeManaged(panelType);

			if (effectiveCloseOthers)
				CloseManagedPanelsInternal();

			GameObject existing = activePanels.Find(p => p != null && p.TryGetComponent(panelType, out _));

			if (existing != null)
			{
				existing.SetActive(true);
				BringToFront(existing);
				var panelScript = existing.GetComponent<T>();
				currentTopPanel = panelScript;
				panelScript?.OnPanelOpened();
				return panelScript;
			}

			if (!prefabByType.TryGetValue(panelType, out var prefab) || prefab == null)
			{
				Debug.LogError($"No prefab for {panelType.Name}");
				return null;
			}

			GameObject newPanelObj = Instantiate(prefab, mainCanvas.transform);
			newPanelObj.name = panelType.Name;
			newPanelObj.SetActive(true);

			BringToFront(newPanelObj);

			var newPanelScript = newPanelObj.GetComponent<T>();
			if (newPanelScript != null)
				newPanelScript.OnPanelOpened();

			activePanels.Add(newPanelObj);
			currentTopPanel = newPanelScript;

			return newPanelScript;
		}

		private T GetOpenPanelInternal<T>() where T : UIPanel
		{
			foreach (var p in activePanels)
				if (p != null && p.TryGetComponent(out T panel))
					return panel;
			return null;
		}

		private bool IsPanelTypeManaged(Type panelType)
		{
			if (!prefabByType.TryGetValue(panelType, out var prefab) || prefab == null)
				return true;

			var p = prefab.GetComponent<UIPanel>();
			return p != null && p.IsManagedByUIController;
		}

		private void BringToFront(GameObject panelObj)
		{
			if (panelObj == null) return;

			var uiPanel = panelObj.GetComponent<UIPanel>();

			if (uiPanel != null && uiPanel.IsAlwaysOnTop)
			{
				panelObj.transform.SetAsLastSibling();
			}
			else
			{
				panelObj.transform.SetAsLastSibling();
			}
		}

		private void CloseManagedPanelsInternal()
		{
			for (int i = activePanels.Count - 1; i >= 0; i--)
			{
				var panelObj = activePanels[i];
				if (panelObj == null) continue;

				var script = panelObj.GetComponent<UIPanel>();
				if (script == null || !script.IsManagedByUIController) continue;

				script.OnPanelClosed();
				Destroy(panelObj);
				activePanels.RemoveAt(i);
			}

			currentTopPanel = activePanels.Count > 0 ? activePanels[^1].GetComponent<UIPanel>() : null;
		}

		private void CloseTopPanelInternal()
		{
			if (currentTopPanel == null) return;

			currentTopPanel.OnPanelClosed();
			activePanels.Remove(currentTopPanel.gameObject);
			Destroy(currentTopPanel.gameObject);

			currentTopPanel = activePanels.Count > 0 ? activePanels[^1].GetComponent<UIPanel>() : null;
		}

		private void ClosePanelInternal(Type panelType)
		{
			for (int i = activePanels.Count - 1; i >= 0; i--)
			{
				var panelObj = activePanels[i];
				if (panelObj == null) continue;

				if (panelObj.TryGetComponent(panelType, out Component comp))
				{
					var script = comp as UIPanel;
					if (script == null) continue;

					script.OnPanelClosed();
					Destroy(panelObj);
					activePanels.RemoveAt(i);

					if (currentTopPanel == script)
						currentTopPanel = activePanels.Count > 0 ? activePanels[^1].GetComponent<UIPanel>() : null;
					return;
				}
			}
		}

		public void NotifyPanelDestroyed(UIPanel panel)
		{
			activePanels.RemoveAll(p => p == null || p.GetComponent<UIPanel>() == panel);

			if (currentTopPanel == panel)
				currentTopPanel = activePanels.Count > 0 ? activePanels[^1].GetComponent<UIPanel>() : null;
		}

		/// <summary>
		/// Closes all panels that have Always On Top enabled.
		/// Called when a normal (non-alwaysOnTop) panel is clicked.
		/// </summary>
		public void CloseAllAlwaysOnTopPanels()
		{
			for (int i = activePanels.Count - 1; i >= 0; i--)
			{
				var panelObj = activePanels[i];
				if (panelObj == null) continue;

				var panelScript = panelObj.GetComponent<UIPanel>();
				if (panelScript == null || !panelScript.IsAlwaysOnTop)
					continue;

				panelScript.OnPanelClosed();
				Destroy(panelObj);
				activePanels.RemoveAt(i);
			}

			// Update current top panel
			currentTopPanel = activePanels.Count > 0
				? activePanels[^1].GetComponent<UIPanel>()
				: null;
		}
	}
}

//using UnityEngine;
//using System;
//using System.Collections.Generic;

//namespace ClassicTilestorm
//{
//	public class UIController : MonoBehaviour
//	{
//		public static UIController Instance { get; private set; }

//		[Header("UI Setup")]
//		[SerializeField] private Canvas mainCanvas;

//		[Header("Panel Prefabs – drag all panel prefabs here (including EditorScreenUI)")]
//		[SerializeField] private List<GameObject> panelPrefabs = new();

//		private readonly Dictionary<Type, GameObject> prefabByType = new();

//		// Supports multiple open panels at the same time
//		private readonly List<GameObject> activePanels = new List<GameObject>();
//		private UIPanel currentTopPanel;

//		private void Awake()
//		{
//			if (Instance != null && Instance != this)
//			{
//				Destroy(gameObject);
//				return;
//			}

//			Instance = this;
//			DontDestroyOnLoad(gameObject);

//			// Build prefab lookup table
//			prefabByType.Clear();
//			foreach (var prefab in panelPrefabs)
//			{
//				if (prefab == null) continue;

//				var panel = prefab.GetComponent<UIPanel>();
//				if (panel == null)
//				{
//					Debug.LogWarning($"Prefab {prefab.name} is missing UIPanel component!", prefab);
//					continue;
//				}

//				var type = panel.GetType();
//				if (prefabByType.ContainsKey(type))
//				{
//					Debug.LogWarning($"Duplicate panel type: {type.Name}", prefab);
//					continue;
//				}

//				prefabByType[type] = prefab;
//			}

//			if (prefabByType.Count == 0)
//				Debug.LogWarning("UIController: No valid panel prefabs assigned!");
//		}

//		// ── Public API ────────────────────────────────────────────────────────

//		/// <summary>
//		/// Opens a panel and returns the panel instance.
//		/// - closeOthers = true (default): Closes other managed panels.
//		/// - closeOthers = false: Keeps all other panels open and brings this one to front.
//		/// 
//		/// Note: If the panel being opened has managedByUIController = false, 
//		///       closeOthers is automatically ignored.
//		/// </summary>
//		public static T OpenPanel<T>(bool closeOthers = true) where T : UIPanel
//		{
//			if (Instance == null)
//			{
//				Debug.LogError("UIController instance not found!");
//				return null;
//			}

//			return Instance.OpenPanelInternal<T>(typeof(T), closeOthers);
//		}

//		/// <summary>
//		/// Returns the currently open panel of type T, or null if none is open.
//		/// </summary>
//		public static T GetOpenPanel<T>() where T : UIPanel
//		{
//			if (Instance == null) return null;
//			return Instance.GetOpenPanelInternal<T>();
//		}

//		public static void CloseCurrent()
//		{
//			Instance?.CloseTopPanelInternal();
//		}

//		public static void ClosePanel<T>() where T : UIPanel
//		{
//			Instance?.ClosePanelInternal(typeof(T));
//		}

//		// ── Internal logic ────────────────────────────────────────────────────

//		private T OpenPanelInternal<T>(Type panelType, bool requestedCloseOthers) where T : UIPanel
//		{
//			// Determine effective closeOthers behavior
//			bool effectiveCloseOthers = requestedCloseOthers;

//			// If the panel we are opening is NOT managed, never close other panels
//			if (!IsPanelTypeManaged(panelType))
//			{
//				effectiveCloseOthers = false;
//			}

//			if (effectiveCloseOthers)
//			{
//				CloseManagedPanelsInternal();
//			}

//			// Check if this panel type is already open
//			GameObject existing = activePanels.Find(p =>
//				p != null && p.TryGetComponent(panelType, out Component comp) && comp is UIPanel);

//			if (existing != null)
//			{
//				existing.SetActive(true);
//				BringToFront(existing);
//				var panelScript = existing.GetComponent<T>();
//				currentTopPanel = panelScript;
//				panelScript?.OnPanelOpened();
//				return panelScript;
//			}

//			// Instantiate new panel
//			if (!prefabByType.TryGetValue(panelType, out var prefab) || prefab == null)
//			{
//				Debug.LogError($"No prefab registered for panel type: {panelType.Name}");
//				return null;
//			}

//			GameObject newPanelObj = Instantiate(prefab, mainCanvas.transform);
//			newPanelObj.name = panelType.Name;
//			newPanelObj.SetActive(true);

//			BringToFront(newPanelObj);

//			var newPanelScript = newPanelObj.GetComponent<T>();
//			if (newPanelScript != null)
//				newPanelScript.OnPanelOpened();

//			activePanels.Add(newPanelObj);
//			currentTopPanel = newPanelScript;

//			return newPanelScript;
//		}

//		private T GetOpenPanelInternal<T>() where T : UIPanel
//		{
//			foreach (var panelObj in activePanels)
//			{
//				if (panelObj != null && panelObj.TryGetComponent(out T panel))
//					return panel;
//			}
//			return null;
//		}

//		/// <summary>
//		/// Checks whether a panel type is managed (i.e. should be closed when closeOthers = true)
//		/// </summary>
//		private bool IsPanelTypeManaged(Type panelType)
//		{
//			if (!prefabByType.TryGetValue(panelType, out var prefab) || prefab == null)
//				return true; // default to managed if unknown

//			var panelScript = prefab.GetComponent<UIPanel>();
//			return panelScript != null && panelScript.IsManagedByUIController;
//		}

//		private void BringToFront(GameObject panel)
//		{
//			panel?.transform.SetAsLastSibling();
//		}

//		/// <summary>
//		/// Closes only panels that have managedByUIController = true.
//		/// Non-managed panels are left untouched.
//		/// </summary>
//		private void CloseManagedPanelsInternal()
//		{
//			for (int i = activePanels.Count - 1; i >= 0; i--)
//			{
//				var panelObj = activePanels[i];
//				if (panelObj == null) continue;

//				var panelScript = panelObj.GetComponent<UIPanel>();
//				if (panelScript == null || !panelScript.IsManagedByUIController)
//					continue;

//				panelScript.OnPanelClosed();
//				Destroy(panelObj);
//				activePanels.RemoveAt(i);
//			}

//			currentTopPanel = activePanels.Count > 0
//				? activePanels[^1].GetComponent<UIPanel>()
//				: null;
//		}

//		private void CloseTopPanelInternal()
//		{
//			if (currentTopPanel == null) return;

//			currentTopPanel.OnPanelClosed();

//			activePanels.Remove(currentTopPanel.gameObject);
//			Destroy(currentTopPanel.gameObject);

//			currentTopPanel = activePanels.Count > 0
//				? activePanels[^1].GetComponent<UIPanel>()
//				: null;
//		}

//		private void ClosePanelInternal(Type panelType)
//		{
//			for (int i = activePanels.Count - 1; i >= 0; i--)
//			{
//				var panelObj = activePanels[i];
//				if (panelObj == null) continue;

//				if (panelObj.TryGetComponent(panelType, out Component component))
//				{
//					UIPanel script = component as UIPanel;
//					if (script == null) continue;

//					script.OnPanelClosed();
//					activePanels.RemoveAt(i);
//					Destroy(panelObj);

//					if (currentTopPanel == script)
//					{
//						currentTopPanel = activePanels.Count > 0
//							? activePanels[^1].GetComponent<UIPanel>()
//							: null;
//					}
//					return;
//				}
//			}
//		}

//		// ── Notifications from UIPanel ─────────────────────────────────────

//		public bool IsThisPanelCurrent(UIPanel panel)
//		{
//			return currentTopPanel == panel;
//		}

//		public void NotifyPanelDeactivated(UIPanel panel) { }

//		public void NotifyPanelDestroyed(UIPanel panel)
//		{
//			activePanels.RemoveAll(p => p == null || p.GetComponent<UIPanel>() == panel);

//			if (currentTopPanel == panel)
//			{
//				currentTopPanel = activePanels.Count > 0
//					? activePanels[^1].GetComponent<UIPanel>()
//					: null;
//			}
//		}
//	}
//}