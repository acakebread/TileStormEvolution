using UnityEngine;
using UnityEngine.EventSystems;

namespace MassiveHadronLtd
{
	/// <summary>
	/// Generic UI pointer input handler for drag, scroll, and click events.
	/// Attach to any UI element with a raycast target (RawImage, Image, etc.).
	/// </summary>
	[AddComponentMenu("Massive Hadron/UI/Pointer Drag Scroll Handler")]
	public class PointerDragScrollHandler : MonoBehaviour,
		IPointerDownHandler,
		IPointerUpHandler,
		IDragHandler,
		IScrollHandler
	{
		private Vector2 lastPointerPosition;

		// Callbacks — intentionally different names to avoid collision with interface methods
		public System.Action OnPointerDownCallback { get; set; }
		public System.Action OnPointerUpCallback { get; set; }
		public System.Action<Vector2> OnDragCallback { get; set; }
		public System.Action<float> OnScrollCallback { get; set; }

		// ── Interface implementations (these are the actual event handlers) ──

		void IPointerDownHandler.OnPointerDown(PointerEventData eventData)
		{
			lastPointerPosition = eventData.position;
			OnPointerDownCallback?.Invoke();
		}

		void IPointerUpHandler.OnPointerUp(PointerEventData eventData)
		{
			OnPointerUpCallback?.Invoke();
		}

		void IDragHandler.OnDrag(PointerEventData eventData)
		{
			Vector2 delta = eventData.position - lastPointerPosition;
			lastPointerPosition = eventData.position;

			OnDragCallback?.Invoke(delta);
		}

		void IScrollHandler.OnScroll(PointerEventData eventData)
		{
			OnScrollCallback?.Invoke(eventData.scrollDelta.y);
		}

		// Optional: convenience setup method
		public void Setup(
			System.Action onDown = null,
			System.Action onUp = null,
			System.Action<Vector2> onDrag = null,
			System.Action<float> onScroll = null)
		{
			OnPointerDownCallback = onDown;
			OnPointerUpCallback = onUp;
			OnDragCallback = onDrag;
			OnScrollCallback = onScroll;
		}
	}
}