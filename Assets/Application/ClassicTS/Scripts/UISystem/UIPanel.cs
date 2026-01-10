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
		[SerializeField] private Vector2 minSize = new Vector2(256, 100);

		private RectTransform rect;
		private Canvas canvas;
		private RectTransform parentRect;
		private bool isPointerDown;
		private Vector2 pointerStart;
		private Vector2 offsetMinStart;
		private Vector2 offsetMaxStart;
		private Vector2 parentSize;
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
			parentRect = rect.parent as RectTransform;
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

			offsetMinStart = rect.offsetMin;
			offsetMaxStart = rect.offsetMax;
			parentSize = parentRect.rect.size;

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

			if (dragEdge == Edge.All && !draggable)
				return;

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

			Vector2 offsetMinNew = offsetMinStart;
			Vector2 offsetMaxNew = offsetMaxStart;

			if (dragEdge.HasFlag(Edge.Left)) offsetMinNew.x += delta.x;
			if (dragEdge.HasFlag(Edge.Right)) offsetMaxNew.x += delta.x;
			if (dragEdge.HasFlag(Edge.Bottom)) offsetMinNew.y += delta.y;
			if (dragEdge.HasFlag(Edge.Top)) offsetMaxNew.y += delta.y;

			bool isResizing = dragEdge != Edge.All;

			if (isResizing)
			{
				Vector2 anchorDelta = rect.anchorMax - rect.anchorMin;
				Vector2 stretch = new Vector2(anchorDelta.x * parentSize.x, anchorDelta.y * parentSize.y);
				Vector2 offsetDeltaNew = offsetMaxNew - offsetMinNew;
				Vector2 actualNew = stretch + offsetDeltaNew;

				// Clamp x
				if (actualNew.x < minSize.x)
				{
					float deficit = minSize.x - actualNew.x;
					float leftW = dragEdge.HasFlag(Edge.Left) ? 1f : 0f;
					float rightW = dragEdge.HasFlag(Edge.Right) ? 1f : 0f;
					float totalW = leftW + rightW;
					if (totalW > 0)
					{
						offsetMinNew.x -= deficit * leftW / totalW;
						offsetMaxNew.x += deficit * rightW / totalW;
					}
				}

				// Clamp y
				if (actualNew.y < minSize.y)
				{
					float deficit = minSize.y - actualNew.y;
					float bottomW = dragEdge.HasFlag(Edge.Bottom) ? 1f : 0f;
					float topW = dragEdge.HasFlag(Edge.Top) ? 1f : 0f;
					float totalW = bottomW + topW;
					if (totalW > 0)
					{
						offsetMinNew.y -= deficit * bottomW / totalW;
						offsetMaxNew.y += deficit * topW / totalW;
					}
				}
			}

			rect.offsetMin = offsetMinNew;
			rect.offsetMax = offsetMaxNew;
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
		protected virtual void Awake() { }

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