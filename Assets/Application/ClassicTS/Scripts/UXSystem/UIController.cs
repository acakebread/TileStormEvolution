using System;
using UnityEngine;
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
		private readonly List<GameObject> activePanels = new();
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
			var effectiveCloseOthers = requestedCloseOthers && IsPanelTypeManaged(panelType);

			if (effectiveCloseOthers)
				CloseManagedPanelsInternal();

			var existing = activePanels.Find(p => p != null && p.TryGetComponent(panelType, out _));

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

			var newPanelObj = Instantiate(prefab, mainCanvas.transform);
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
			for (var i = activePanels.Count - 1; i >= 0; i--)
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
			for (var i = activePanels.Count - 1; i >= 0; i--)
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
			for (var i = activePanels.Count - 1; i >= 0; i--)
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
			currentTopPanel = activePanels.Count > 0 ? activePanels[^1].GetComponent<UIPanel>() : null;
		}
	}
}
