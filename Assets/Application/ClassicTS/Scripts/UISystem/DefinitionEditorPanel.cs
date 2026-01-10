using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro; // if using TextMeshPro for better text

namespace ClassicTilestorm
{
	public class DefinitionEditorPanel : UIPanel
	{
		[Header("UI References")]
		[SerializeField] private Button closeButton;
		[SerializeField] private ScrollRect definitionScrollView;
		[SerializeField] private Transform contentParent;           // usually ScrollView → Viewport → Content
		[SerializeField] private GameObject definitionListItemPrefab; // prefab with Button + Text (TMP or legacy)
		[SerializeField] private RawImage previewImage;             // RawImage that will show the RenderTexture

		[Header("Preview Settings")]
		[SerializeField] private Vector2 previewResolution = new Vector2(256, 256);
		[SerializeField] private float previewCameraDistance = 5f;
		[SerializeField] private Vector3 previewCameraOffset = new Vector3(0, 1.5f, -5f);

		private RenderTexture previewRenderTexture;
		private Camera previewCamera;
		private GameObject previewRoot;
		private string selectedDefinitionId;

		private readonly List<GameObject> spawnedListItems = new();

		protected override void Awake()
		{
			base.Awake();

			// Optional: code wiring for close button
			if (closeButton != null)
				closeButton.onClick.AddListener(() => gameObject.SetActive(false));

			// Safety check
			if (contentParent == null)
				contentParent = definitionScrollView?.content;

			if (contentParent == null)
				Debug.LogError("DefinitionEditorPanel: Content parent not assigned!", this);
		}

		public override void OnPanelOpened()
		{
			base.OnPanelOpened();

			Debug.Log("Definition Editor opened - loading definitions...");

			CleanupPreview();
			CreatePreviewSetup();
			RefreshDefinitionList();

			// Optional: auto-select first definition
			if (ResourceManager.Definitions.Count > 0)
				SelectDefinition(ResourceManager.Definitions[0].id);
		}

		public override void OnPanelClosed()
		{
			base.OnPanelClosed();
			CleanupPreview();
			ClearListItems();
		}

		private void OnDestroy()
		{
			CleanupPreview();
		}

		private void RefreshDefinitionList()
		{
			ClearListItems();

			if (ResourceManager.Definitions == null || ResourceManager.Definitions.Count == 0)
			{
				Debug.LogWarning("No definitions found in ResourceManager!");
				return;
			}

			foreach (var def in ResourceManager.Definitions)
			{
				CreateDefinitionListItem(def);
			}

			// Optional: force layout rebuild
			LayoutRebuilder.ForceRebuildLayoutImmediate(contentParent as RectTransform);
		}

		private void ClearListItems()
		{
			foreach (var item in spawnedListItems)
				if (item != null) Destroy(item);
			spawnedListItems.Clear();
		}

		private void CreateDefinitionListItem(Definition def)
		{
			if (definitionListItemPrefab == null) return;

			var itemGO = Instantiate(definitionListItemPrefab, contentParent);
			spawnedListItems.Add(itemGO);

			var item = itemGO.GetComponent<DefinitionListItem>();
			if (item == null)
			{
				Debug.LogError("Definition list item prefab is missing DefinitionListItem component!", itemGO);
				return;
			}

			item.Initialize(def.id, SelectDefinition);

			// Initial selection state
			item.SetSelected(def.id == selectedDefinitionId);
		}

		private void SelectDefinition(string defId)
		{
			if (string.IsNullOrEmpty(defId)) return;

			selectedDefinitionId = defId;

			// Update all items' visual state
			foreach (var go in spawnedListItems)
			{
				if (go == null) continue;
				var item = go.GetComponent<DefinitionListItem>();
				if (item != null)
				{
					item.SetSelected(item.DefinitionId == selectedDefinitionId);
				}
			}

			UpdatePreview(defId);
		}

		// ── Preview System ─────────────────────────────────────────────────────

		private void CreatePreviewSetup()
		{
			if (previewImage == null) return;

			// Create root for preview objects
			previewRoot = new GameObject("DefinitionPreviewRoot");
			previewRoot.hideFlags = HideFlags.HideAndDontSave;

			// Create camera
			var camGO = new GameObject("PreviewCam");
			camGO.transform.SetParent(previewRoot.transform);
			previewCamera = camGO.AddComponent<Camera>();
			previewCamera.clearFlags = CameraClearFlags.SolidColor;
			previewCamera.backgroundColor = new Color(0.1f, 0.1f, 0.15f);
			previewCamera.cullingMask = 1 << LayerMask.NameToLayer("Default"); // adjust if needed
			previewCamera.orthographic = false;
			previewCamera.fieldOfView = 60f;
			previewCamera.nearClipPlane = 0.1f;
			previewCamera.farClipPlane = 100f;

			// Render Texture
			previewRenderTexture = new RenderTexture(
				(int)previewResolution.x,
				(int)previewResolution.y,
				24, RenderTextureFormat.ARGB32);
			previewRenderTexture.Create();

			previewCamera.targetTexture = previewRenderTexture;
			previewImage.texture = previewRenderTexture;
		}

		private void UpdatePreview(string defId)
		{
			if (previewCamera == null || string.IsNullOrEmpty(defId))
			{
				previewImage.enabled = false;
				return;
			}

			previewImage.enabled = true;

			// Clear previous model
			foreach (Transform child in previewRoot.transform)
				if (child.name != "PreviewCam")
					Destroy(child.gameObject);

			var def = ResourceManager.GetDefinition(defId);
			if (def == null || string.IsNullOrEmpty(def.model)) return;

			// Here you need to instantiate your model
			// This part depends heavily on how your models are loaded!
			// Example placeholder - replace with your real loading system

			GameObject modelPrefab = Resources.Load<GameObject>(def.model); // ← CHANGE THIS
			if (modelPrefab == null)
			{
				Debug.LogWarning($"Could not load model for definition: {def.model}");
				return;
			}

			var modelInstance = Instantiate(modelPrefab, previewRoot.transform);
			modelInstance.transform.localPosition = Vector3.zero;
			modelInstance.transform.localRotation = Quaternion.identity;

			// Position camera nicely
			previewCamera.transform.localPosition = previewCameraOffset;
			previewCamera.transform.LookAt(Vector3.zero + new Vector3(0, 1f, 0)); // look slightly above center
		}

		private void CleanupPreview()
		{
			if (previewRenderTexture != null)
			{
				previewRenderTexture.Release();
				previewRenderTexture = null;
			}

			if (previewRoot != null)
			{
				Destroy(previewRoot);
				previewRoot = null;
			}

			previewCamera = null;
			if (previewImage != null)
				previewImage.texture = null;
		}
	}
}