using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.UI;

namespace ClassicTilestorm
{
	public abstract class UIPanel : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
	{
		[Header("Drag & Resize")]
		[SerializeField] private bool draggable = true;
		[SerializeField] private bool resizable = true;

		[SerializeField, Tooltip("Resize grab zone thickness in pixels")]
		private float resizeBorder = 16f; // increased from 8f

		[SerializeField] private Vector2 minSize = new Vector2(150, 100);

		private RectTransform rect;
		private Canvas canvas;

		private bool isPointerDown;
		private Vector2 pointerStart;
		private Vector2 sizeStart;
		private Vector2 posStart;

		private Edge dragEdge;

		// Represents which edges are being modified (can be all for drag)
		[System.Flags]
		private enum Edge
		{
			None = 0,
			Left = 1,
			Right = 2,
			Top = 4,
			Bottom = 8,
			All = Left | Right | Top | Bottom
		}

		private void EnsureInit()
		{
			if (rect != null) return;
			rect = GetComponent<RectTransform>();
			canvas = GetComponentInParent<Canvas>();
		}

		public void OnPointerDown(PointerEventData eventData)
		{
			if (eventData.button != PointerEventData.InputButton.Left)
				return;

			EnsureInit();

			if (IsPointerOverInteractableUI(eventData))
				return;

			RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas.transform as RectTransform,
				eventData.position, eventData.pressEventCamera, out pointerStart);

			sizeStart = rect.sizeDelta;
			posStart = rect.anchoredPosition;

			dragEdge = Edge.All; // default: drag entire panel

			if (resizable)
			{
				Rect r = rect.rect;
				Vector2 local;
				RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out local);

				// Thicker edges for easier detection
				float cornerExtra = resizeBorder * 0.5f;

				bool left = local.x < r.xMin + resizeBorder;
				bool right = local.x > r.xMax - resizeBorder;
				bool top = local.y > r.yMax - resizeBorder;
				bool bottom = local.y < r.yMin + resizeBorder;

				Edge e = Edge.None;

				// Corner detection (both edges)
				if (left && top) e |= Edge.Left | Edge.Top;
				else if (right && top) e |= Edge.Right | Edge.Top;
				else if (left && bottom) e |= Edge.Left | Edge.Bottom;
				else if (right && bottom) e |= Edge.Right | Edge.Bottom;
				else
				{
					if (left) e |= Edge.Left;
					if (right) e |= Edge.Right;
					if (top) e |= Edge.Top;
					if (bottom) e |= Edge.Bottom;
				}

				if (e != Edge.None) dragEdge = e;
			}

			isPointerDown = true;
			transform.SetAsLastSibling();
		}

		public void OnPointerUp(PointerEventData eventData)
		{
			isPointerDown = false;
		}

		public void OnDrag(PointerEventData eventData)
		{
			if (!isPointerDown)
				return;

			Vector2 local;
			RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas.transform as RectTransform,
				eventData.position, eventData.pressEventCamera, out local);

			Vector2 delta = local - pointerStart;

			Vector2 newSize = sizeStart;
			Vector2 newPos = posStart;

			// Horizontal resizing
			if (dragEdge.HasFlag(Edge.Left)) { newSize.x -= delta.x; newPos.x += delta.x * 0.5f; }
			if (dragEdge.HasFlag(Edge.Right)) { newSize.x += delta.x; newPos.x += delta.x * 0.5f; }

			// Vertical resizing
			if (dragEdge.HasFlag(Edge.Bottom)) { newSize.y -= delta.y; newPos.y += delta.y * 0.5f; }
			if (dragEdge.HasFlag(Edge.Top)) { newSize.y += delta.y; newPos.y += delta.y * 0.5f; }

			newSize.x = Mathf.Max(minSize.x, newSize.x);
			newSize.y = Mathf.Max(minSize.y, newSize.y);

			rect.sizeDelta = newSize;
			rect.anchoredPosition = newPos;
		}

		private bool IsPointerOverInteractableUI(PointerEventData eventData)
		{
			var results = new List<RaycastResult>();
			EventSystem.current.RaycastAll(eventData, results);

			foreach (var r in results)
			{
				if (r.gameObject == gameObject) continue;

				if (r.gameObject.GetComponent<Selectable>() ||
					r.gameObject.GetComponent<ScrollRect>() ||
					r.gameObject.GetComponent<Scrollbar>())
					return true;
			}
			return false;
		}

		// ── Lifecycle / overrides ──────────────────────────────

		protected virtual void OnDisable()
		{
			var controller = UIController.Instance;
			if (controller != null && controller.IsThisPanelCurrent(this))
				controller.NotifyPanelDeactivated(this);
		}

		protected virtual void OnDestroy()
		{
			var controller = UIController.Instance;
			if (controller != null)
				controller.NotifyPanelDestroyed(this);
		}

		public virtual void OnPanelOpened() { }
		public virtual void OnPanelClosed() { }
	}
}
