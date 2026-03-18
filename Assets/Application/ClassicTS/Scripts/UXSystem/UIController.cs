using UnityEngine;
using System;
using System.Collections.Generic;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class UIController : MonoBehaviour
	{
		public static UIController Instance { get; private set; }

		[Header("UI Setup")]
		[SerializeField] private Canvas mainCanvas; // Drag your Screen Space - Overlay canvas here

		[Header("Panel Prefabs – drag prefabs here (must have UIPanel component)")]
		[SerializeField] private List<GameObject> panelPrefabs = new();

		[SerializeField] private GameObject editorScreenUIPrefab;
		[HideInInspector] public GameObject editorScreenUI;

		private readonly Dictionary<Type, GameObject> prefabByType = new();

		private GameObject currentPanel;
		private UIPanel currentPanelScript; // Cached reference to the UIPanel component

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}

			Instance = this;
			DontDestroyOnLoad(gameObject);

			// Automatically build lookup table from prefabs
			prefabByType.Clear();
			foreach (var prefab in panelPrefabs)
			{
				if (prefab == null) continue;

				var panel = prefab.GetComponent<UIPanel>();
				if (panel == null)
				{
					Debug.LogWarning($"Prefab {prefab.name} is missing UIPanel component!", prefab);
					continue;
				}

				var type = panel.GetType();
				if (prefabByType.ContainsKey(type))
				{
					Debug.LogWarning($"Duplicate panel type: {type.Name}", prefab);
					continue;
				}

				prefabByType[type] = prefab;
			}

			if (prefabByType.Count == 0)
				Debug.LogWarning("UIController: No valid panel prefabs assigned!");

			editorScreenUI = Instantiate(editorScreenUIPrefab, mainCanvas.transform);
			editorScreenUI.SetActive(false);

			var editorComp = editorScreenUI.GetComponent<EditorScreenUI>();
			if (null != editorComp) ReadyCallbackRegistry.Raise(editorComp);// This broadcasts to all who registered for EditorScreenUI
		}

		// ── Public API ────────────────────────────────────────────────────────

		public static void OpenPanel<T>() where T : UIPanel
		{
			if (Instance == null)
			{
				Debug.LogError("UIController instance not found!");
				return;
			}

			Instance.OpenPanelInternal(typeof(T));
		}

		public static void CloseCurrent()
		{
			if (Instance == null) return;
			Instance.CloseCurrentInternal();
		}

		// ── Internal logic ────────────────────────────────────────────────────

		private void OpenPanelInternal(Type panelType)
		{
			// Case 1: Same panel type already exists → just reactivate (state preserved)
			if (currentPanel != null && currentPanel.TryGetComponent(panelType, out var existingPanel))
			{
				currentPanel.SetActive(true);
				currentPanelScript = (UIPanel)existingPanel;
				currentPanelScript.OnPanelOpened();
				return;
			}

			// Case 2: Different panel → clean up old one first
			CloseCurrentInternal();

			// Instantiate new panel
			if (!prefabByType.TryGetValue(panelType, out var prefab) || prefab == null)
			{
				Debug.LogError($"No prefab registered for panel type: {panelType.Name}");
				return;
			}

			currentPanel = Instantiate(prefab, mainCanvas.transform);
			currentPanel.name = panelType.Name;
			currentPanel.SetActive(true);

			currentPanelScript = currentPanel.GetComponent<UIPanel>();
			if (currentPanelScript != null)
				currentPanelScript.OnPanelOpened();
		}

		private void CloseCurrentInternal()
		{
			if (currentPanel == null) return;

			if (currentPanelScript != null)
				currentPanelScript.OnPanelClosed();

			Destroy(currentPanel);
			currentPanel = null;
			currentPanelScript = null;
		}

		// ── Called by UIPanel base class when deactivated/destroyed ──────────

		public bool IsThisPanelCurrent(UIPanel panel)
		{
			return currentPanelScript == panel;
		}

		public void NotifyPanelDeactivated(UIPanel panel)
		{
			if (currentPanelScript == panel)
			{
				// We keep the instance alive but mark it as no longer "current"
				// → allows reactivation with preserved state next time
				currentPanelScript = null;
				// Note: we do NOT destroy here — destruction happens when switching panels
			}
		}

		public void NotifyPanelDestroyed(UIPanel panel)
		{
			if (currentPanelScript == panel)
			{
				currentPanel = null;
				currentPanelScript = null;
			}
		}
	}
}