using UnityEngine;
using UnityEngine.UI;

namespace ClassicTilestorm
{
	public class EffectEditorPanel : UIPanel
	{
		[Header("UI References (optional for testing)")]
		[SerializeField] private Button closeButton;

		protected override void Awake()
		{
			// Optional: connect close button in code (or do it in Inspector)
			if (closeButton != null)
			{
				closeButton.onClick.AddListener(() => gameObject.SetActive(false));
			}
		}

		public override void OnPanelOpened()
		{
			Debug.Log("Effect Editor panel opened");
			// You can add test content here later (e.g. particle previews, effect lists, etc.)
		}

		public override void OnPanelClosed()
		{
			Debug.Log("Effect Editor panel closed");
		}
	}
}





//using UnityEngine;
//using UnityEngine.UI;
//using System.Collections.Generic;
//using UnityEngine.EventSystems;
//using ClassicTilestorm.Editor;

//namespace ClassicTilestorm
//{
//	/// <summary>
//	/// Editor panel for browsing and previewing Definitions with isolated preview scene
//	/// </summary>
//	public class DefinitionEditorPanel : UIPanel, IPreviewUser
//	{
//		[Header("UI References")]
//		[SerializeField] private Button closeButton;
//		[SerializeField] private ScrollRect definitionScrollView;
//		[SerializeField] private Transform contentParent;
//		[SerializeField] private GameObject definitionListItemPrefab;
//		[SerializeField] private RawImage previewImage;

//		[Header("Preview Settings (Visual Only)")]
//		[SerializeField] private Vector2 previewResolution = new Vector2(320, 240);
//		[SerializeField] private Color backgroundColor = new Color(0.08f, 0.10f, 0.15f);

//		// Runtime preview references
//		private PreviewSceneInstance previewInstance;
//		private RenderTexture previewRenderTexture;
//		private GameObject currentModelInstance;
//		private string selectedDefinitionId;

//		private readonly List<GameObject> spawnedListItems = new List<GameObject>();

//		// ─────────────────────────────────────────────────────────────

//		protected override void Awake()
//		{
//			base.Awake();

//			if (closeButton != null)
//				closeButton.onClick.AddListener(() => gameObject.SetActive(false));

//			if (contentParent == null && definitionScrollView != null)
//				contentParent = definitionScrollView.content;
//		}

//		public override void OnPanelOpened()
//		{
//			base.OnPanelOpened();
//			CreatePreviewInstance();
//			RefreshDefinitionList();

//			if (ResourceManager.Definitions.Count > 0)
//				SelectDefinition(ResourceManager.Definitions[0].id);
//		}

//		public override void OnPanelClosed()
//		{
//			CleanupPreview();
//			ClearListItems();
//			base.OnPanelClosed();
//		}

//		protected override void OnDestroy()
//		{
//			CleanupPreview();
//			base.OnDestroy();
//		}

//		private void Update()
//		{
//			if (previewInstance != null && previewRenderTexture != null)
//			{
//				previewInstance.RenderTo(previewRenderTexture);
//			}
//		}

//		// ─────────────────────────── LIST ───────────────────────────

//		private void RefreshDefinitionList()
//		{
//			ClearListItems();

//			if (ResourceManager.Definitions == null || ResourceManager.Definitions.Count == 0)
//				return;

//			foreach (var def in ResourceManager.Definitions)
//			{
//				CreateDefinitionListItem(def);
//			}
//		}

//		private void ClearListItems()
//		{
//			foreach (var item in spawnedListItems)
//			{
//				if (item != null) Destroy(item);
//			}
//			spawnedListItems.Clear();
//		}

//		private void CreateDefinitionListItem(Definition def)
//		{
//			if (definitionListItemPrefab == null) return;

//			var go = Instantiate(definitionListItemPrefab, contentParent);
//			spawnedListItems.Add(go);

//			var item = go.GetComponent<DefinitionListItem>();
//			if (item == null) return;

//			item.Initialize(def.id, SelectDefinition);

//			if (item.label != null)
//			{
//				item.label.richText = false;
//				item.label.enableAutoSizing = false;
//				item.label.fontSize = 20;
//				item.label.text = $"{def.id}  ({def.model ?? "—"})";
//			}

//			item.SetSelected(def.id == selectedDefinitionId);
//		}

//		private void SelectDefinition(string defId)
//		{
//			if (string.IsNullOrEmpty(defId)) return;

//			selectedDefinitionId = defId;

//			foreach (var go in spawnedListItems)
//			{
//				if (go == null) continue;
//				var item = go.GetComponent<DefinitionListItem>();
//				if (item != null)
//					item.SetSelected(item.DefinitionId == selectedDefinitionId);
//			}

//			UpdatePreview(defId);
//		}

//		// ──────────────────────── PREVIEW SYSTEM ─────────────────────────

//		private void CreatePreviewInstance()
//		{
//			if (previewInstance != null) return;

//			previewInstance = PreviewSceneManager.Instance.CreatePreviewInstance("DefinitionEditorPreview");

//			previewRenderTexture = new RenderTexture(
//				(int)previewResolution.x,
//				(int)previewResolution.y,
//				24,
//				RenderTextureFormat.ARGB32
//			);

//			previewRenderTexture.Create();

//			if (previewImage != null)
//			{
//				previewImage.texture = previewRenderTexture;
//				previewImage.enabled = true;
//			}

//			AddPreviewInputBlocker();
//		}

//		private void UpdatePreview(string defId)
//		{
//			if (previewInstance == null) return;

//			previewInstance.ClearContent();

//			if (string.IsNullOrEmpty(defId))
//			{
//				if (previewImage != null) previewImage.enabled = false;
//				return;
//			}

//			var def = ResourceManager.GetDefinition(defId);
//			if (def == null || string.IsNullOrEmpty(def.model))
//			{
//				Debug.LogWarning($"Definition not found or has no model: {defId}");
//				return;
//			}

//			currentModelInstance = DefinitionFactory.Instantiate(def, parent: previewInstance.ContentRoot);

//			if (currentModelInstance == null)
//			{
//				Debug.LogWarning($"Failed to instantiate model for definition: {def.id} ({def.model})");
//				return;
//			}

//			// Reset transform
//			var t = currentModelInstance.transform;
//			t.localPosition = Vector3.zero;
//			t.localRotation = Quaternion.identity;
//			t.localScale = Vector3.one;
//		}

//		private void AddPreviewInputBlocker()
//		{
//			if (previewImage == null) return;

//			var blockerGo = new GameObject("PreviewInputBlocker");
//			blockerGo.transform.SetParent(previewImage.transform, false);

//			var rt = blockerGo.AddComponent<RectTransform>();
//			rt.anchorMin = Vector2.zero;
//			rt.anchorMax = Vector2.one;
//			rt.offsetMin = Vector2.zero;
//			rt.offsetMax = Vector2.zero;

//			var img = blockerGo.AddComponent<Image>();
//			img.color = new Color(0, 0, 0, 0);
//			img.raycastTarget = true;

//			var handler = blockerGo.AddComponent<PreviewInputHandler>();
//			handler.Initialize(this);
//		}

//		private void CleanupPreview()
//		{
//			if (previewInstance != null)
//			{
//				PreviewSceneManager.Instance.DestroyPreviewInstance(previewInstance);
//				previewInstance = null;
//			}

//			if (previewRenderTexture != null)
//			{
//				previewRenderTexture.Release();
//				previewRenderTexture = null;
//			}

//			if (previewImage != null)
//				previewImage.texture = null;

//			currentModelInstance = null;
//		}

//		// ── IPreviewUser implementation ─────────────────────────────────

//		public void OnPreviewDrag(Vector2 delta)
//		{
//			var camCtrl = previewInstance?.GetComponentInChildren<PreviewCameraController>();
//			camCtrl?.Drag(delta);
//		}

//		public void OnPreviewScroll(float scrollDelta)
//		{
//			var camCtrl = previewInstance?.GetComponentInChildren<PreviewCameraController>();
//			camCtrl?.Zoom(scrollDelta);
//		}

//		// ── Input Handler ───────────────────────────────────────────────

//		private class PreviewInputHandler : MonoBehaviour,
//			IPointerDownHandler, IDragHandler, IScrollHandler
//		{
//			private DefinitionEditorPanel panel;
//			private Vector2 lastPosition;

//			public void Initialize(DefinitionEditorPanel owner)
//			{
//				panel = owner;
//			}

//			public void OnPointerDown(PointerEventData eventData)
//			{
//				lastPosition = eventData.position;
//			}

//			public void OnDrag(PointerEventData eventData)
//			{
//				Vector2 delta = eventData.position - lastPosition;
//				lastPosition = eventData.position;
//				panel?.OnPreviewDrag(delta);
//			}

//			public void OnScroll(PointerEventData eventData)
//			{
//				panel?.OnPreviewScroll(eventData.scrollDelta.y);
//			}
//		}
//	}
//}