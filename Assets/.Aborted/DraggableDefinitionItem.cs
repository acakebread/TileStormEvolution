using ClassicTilestorm;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DraggableDefinitionItem : MonoBehaviour,
	IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
{
	[Header("Settings")]
	[SerializeField] private float holdDelay = 0.18f;           // ← key change - wait before allowing drag
	[SerializeField] private float draggedAlpha = 0.7f;
	[SerializeField] private float minDragDistance = 15f;

	private RectTransform rt;
	private Canvas canvas;
	private CanvasGroup canvasGroup;

	private Transform originalParent;
	private int originalSiblingIndex;

	private DefinitionEditorPanel panel;

	private Vector2 pointerDownPos;
	private float pointerDownTime;
	private bool isDragging;

	private void Awake()
	{
		rt = GetComponent<RectTransform>();
		canvas = GetComponentInParent<Canvas>();

		// Force-add CanvasGroup if missing – this is now very aggressive
		canvasGroup = GetComponent<CanvasGroup>();
		if (canvasGroup == null)
		{
			canvasGroup = gameObject.AddComponent<CanvasGroup>();
			canvasGroup.alpha = 1f;
			canvasGroup.interactable = true;
			canvasGroup.blocksRaycasts = true;
			Debug.Log($"[Drag Fix] Automatically added CanvasGroup to {gameObject.name}", this);
		}

		panel = GetComponentInParent<DefinitionEditorPanel>();
	}

	public void OnPointerDown(PointerEventData eventData)
	{
		pointerDownPos = eventData.position;
		pointerDownTime = Time.unscaledTime;
		isDragging = false;
	}

	public void OnBeginDrag(PointerEventData eventData)
	{
		// Only allow drag after hold time AND moved enough distance
		if (Time.unscaledTime - pointerDownTime < holdDelay ||
			Vector2.Distance(pointerDownPos, eventData.position) < minDragDistance)
		{
			eventData.pointerDrag = null;
			isDragging = false;
			return;
		}

		isDragging = true;

		originalParent = transform.parent;
		originalSiblingIndex = transform.GetSiblingIndex();

		transform.SetParent(canvas.transform, true);
		transform.SetAsLastSibling();

		canvasGroup.alpha = draggedAlpha;
		canvasGroup.blocksRaycasts = false;
	}

	public void OnDrag(PointerEventData eventData)
	{
		if (!isDragging) return;
		rt.anchoredPosition += eventData.delta / canvas.scaleFactor;
	}

	public void OnEndDrag(PointerEventData eventData)
	{
		if (!isDragging) return;

		canvasGroup.alpha = 1f;
		canvasGroup.blocksRaycasts = true;

		var results = new System.Collections.Generic.List<RaycastResult>();
		EventSystem.current.RaycastAll(eventData, results);

		int targetIndex = originalSiblingIndex;

		foreach (var result in results)
		{
			var item = result.gameObject.GetComponent<DraggableDefinitionItem>();
			if (item != null && item.transform != transform)
			{
				var otherRect = item.GetComponent<RectTransform>();
				Vector2 local = otherRect.InverseTransformPoint(eventData.position);

				targetIndex = (local.y > 0)
					? item.transform.GetSiblingIndex()
					: item.transform.GetSiblingIndex() + 1;
				break;
			}
		}

		transform.SetParent(originalParent, true);
		targetIndex = Mathf.Clamp(targetIndex, 0, originalParent.childCount);
		transform.SetSiblingIndex(targetIndex);

		// This is the critical call
		if (panel != null)
		{
			panel.OnListOrderChanged();
		}

		isDragging = false;
	}
}