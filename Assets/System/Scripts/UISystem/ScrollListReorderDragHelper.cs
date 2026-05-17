using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MassiveHadronLtd
{
	public sealed class ScrollListReorderDragHelper : IDisposable
	{
		private readonly ScrollRect scrollRect;
		private readonly RectTransform contentParent;
		private readonly Func<Vector2, Transform> resolveHitTest;
		private readonly Action<Transform> onDragStarted;
		private readonly Action<Vector2, Transform> onDrop;
		private readonly float holdDelay;
		private readonly float dragThreshold;
		private readonly float edgeMargin;
		private readonly float edgeScrollSpeed;
		private readonly float maxEdgeScrollMultiplier;

		private Transform candidateRow;
		private Vector2 candidateStartPos;
		private float candidateStartTime;
		private bool dragging;
		private bool scrollWasEnabled;
		private RectTransform ghostRect;
		private Image ghostImage;
		private LayoutElement ghostLayout;

		public ScrollListReorderDragHelper(
			ScrollRect scrollRect,
			RectTransform contentParent,
			Func<Vector2, Transform> resolveHitTest,
			Action<Transform> onDragStarted,
			Action<Vector2, Transform> onDrop,
			float holdDelay = 0.5f,
			float dragThreshold = 8f,
			float edgeMargin = 72f,
			float edgeScrollSpeed = 900f,
			float maxEdgeScrollMultiplier = 3f)
		{
			this.scrollRect = scrollRect;
			this.contentParent = contentParent;
			this.resolveHitTest = resolveHitTest;
			this.onDragStarted = onDragStarted;
			this.onDrop = onDrop;
			this.holdDelay = holdDelay;
			this.dragThreshold = dragThreshold;
			this.edgeMargin = edgeMargin;
			this.edgeScrollSpeed = edgeScrollSpeed;
			this.maxEdgeScrollMultiplier = Mathf.Max(1f, maxEdgeScrollMultiplier);
		}

		public void Update()
		{
			if (contentParent == null || EventSystem.current == null)
				return;

			if (InputX.GetMouseButtonDown(0))
			{
				candidateRow = resolveHitTest?.Invoke(InputX.mousePosition);
				candidateStartPos = InputX.mousePosition;
				candidateStartTime = Time.unscaledTime;
				dragging = false;
			}

			if (candidateRow == null)
				return;

			if (InputX.GetMouseButton(0))
			{
				if (!dragging)
				{
					var heldFor = Time.unscaledTime - candidateStartTime;
					var movedDistance = Vector2.Distance(candidateStartPos, InputX.mousePosition);

					if (movedDistance > dragThreshold && heldFor < holdDelay)
					{
						ResetState();
						return;
					}

					if (heldFor < holdDelay)
						return;

					if (movedDistance > dragThreshold)
					{
						ResetState();
						return;
					}

					if (resolveHitTest?.Invoke(InputX.mousePosition) != candidateRow)
					{
						ResetState();
						return;
					}

					BeginDrag(InputX.mousePosition);
				}

				if (dragging)
				{
					UpdateGhostPosition(InputX.mousePosition);
					ApplyAutoScroll(InputX.mousePosition);
				}

				return;
			}

			if (InputX.GetMouseButtonUp(0))
			{
				if (dragging)
					onDrop?.Invoke(InputX.mousePosition, candidateRow);

				ResetState();
			}
		}

		public void Dispose()
		{
			ResetState();
			DestroyGhost();
		}

		public void ResetState()
		{
			if (scrollRect != null && dragging)
				scrollRect.enabled = scrollWasEnabled;

			dragging = false;
			candidateRow = null;
			candidateStartPos = default;
			candidateStartTime = 0f;
			scrollWasEnabled = false;

			HideGhost();
		}

		private void BeginDrag(Vector2 screenPos)
		{
			if (candidateRow == null)
				return;

			dragging = true;
			scrollWasEnabled = scrollRect != null && scrollRect.enabled;
			if (scrollRect != null)
			{
				scrollRect.enabled = false;
				scrollRect.velocity = Vector2.zero;
			}

			ShowGhost(candidateRow, screenPos);
			onDragStarted?.Invoke(candidateRow);
		}

		private void ShowGhost(Transform row, Vector2 screenPos)
		{
			EnsureGhost();
			if (ghostRect == null || contentParent == null)
				return;

			if (!row.TryGetComponent<RectTransform>(out var rowRect))
				return;

			float width = scrollRect != null && scrollRect.viewport != null ? scrollRect.viewport.rect.width : rowRect.rect.width;
			ghostLayout.preferredWidth = width;
			ghostLayout.preferredHeight = rowRect.rect.height;
			var ghostParent = scrollRect != null && scrollRect.viewport != null
				? scrollRect.viewport
				: (contentParent != null ? contentParent.GetComponentInParent<Canvas>()?.transform : null);
			if (ghostParent != null)
				ghostRect.SetParent(ghostParent, false);
			ghostRect.anchorMin = new Vector2(0f, 0.5f);
			ghostRect.anchorMax = new Vector2(1f, 0.5f);
			ghostRect.pivot = new Vector2(0.5f, 0.5f);
			ghostRect.sizeDelta = new Vector2(width, rowRect.rect.height);
			ghostRect.SetAsLastSibling();
			ghostRect.gameObject.SetActive(true);
			UpdateGhostPosition(screenPos);
		}

		private void UpdateGhostPosition(Vector2 screenPos)
		{
			if (ghostRect == null || !ghostRect.gameObject.activeSelf)
				return;

			var anchorRect = scrollRect != null && scrollRect.viewport != null
				? scrollRect.viewport
				: (contentParent != null ? contentParent.GetComponentInParent<Canvas>()?.transform as RectTransform : null);
			if (anchorRect == null)
				return;

			var canvas = anchorRect.GetComponentInParent<Canvas>();
			var screenCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

			if (RectTransformUtility.ScreenPointToLocalPointInRectangle(anchorRect, screenPos, screenCamera, out var localPoint))
			{
				var centerOffsetY = anchorRect.rect.height * (0.5f - anchorRect.pivot.y);
				ghostRect.anchoredPosition = new Vector2(0f, localPoint.y - centerOffsetY);
			}
		}

		private void ApplyAutoScroll(Vector2 screenPos)
		{
			if (scrollRect == null || scrollRect.viewport == null || scrollRect.content == null)
				return;

			var viewport = scrollRect.viewport;
			var canvas = viewport.GetComponentInParent<Canvas>();
			var screenCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

			if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, screenPos, screenCamera, out var localPoint))
				return;

			float top = viewport.rect.yMax;
			float bottom = viewport.rect.yMin;
			float normalizedDelta = 0f;

			if (localPoint.y > top - edgeMargin)
			{
				float amount = Mathf.InverseLerp(top - edgeMargin, top, localPoint.y);
				float speedMultiplier = Mathf.Lerp(1f, maxEdgeScrollMultiplier, amount * amount);
				normalizedDelta = amount * speedMultiplier * edgeScrollSpeed * Time.unscaledDeltaTime / Mathf.Max(1f, scrollRect.content.rect.height);
			}
			else if (localPoint.y < bottom + edgeMargin)
			{
				float amount = Mathf.InverseLerp(bottom + edgeMargin, bottom, localPoint.y);
				float speedMultiplier = Mathf.Lerp(1f, maxEdgeScrollMultiplier, amount * amount);
				normalizedDelta = -amount * speedMultiplier * edgeScrollSpeed * Time.unscaledDeltaTime / Mathf.Max(1f, scrollRect.content.rect.height);
			}

			if (normalizedDelta == 0f)
				return;

			scrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollRect.verticalNormalizedPosition + normalizedDelta);
			if (scrollRect.verticalScrollbar != null)
				scrollRect.verticalScrollbar.value = scrollRect.verticalNormalizedPosition;
			if (scrollRect.horizontalScrollbar != null)
				scrollRect.horizontalScrollbar.value = scrollRect.horizontalNormalizedPosition;
			scrollRect.Rebuild(CanvasUpdate.PostLayout);
			UpdateGhostPosition(screenPos);
		}

		private void HideGhost()
		{
			if (ghostRect != null)
				ghostRect.gameObject.SetActive(false);
		}

		private void DestroyGhost()
		{
			if (ghostRect != null)
			{
				UnityEngine.Object.Destroy(ghostRect.gameObject);
				ghostRect = null;
				ghostImage = null;
				ghostLayout = null;
			}
		}

		private void EnsureGhost()
		{
			if (ghostRect != null)
				return;

			var canvas = contentParent != null ? contentParent.GetComponentInParent<Canvas>() : null;
			if (canvas == null)
				return;

			var go = new GameObject("ListDragGhost", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(LayoutElement));
			go.transform.SetParent(canvas.transform, false);

			ghostRect = go.GetComponent<RectTransform>();
			ghostImage = go.GetComponent<Image>();
			ghostLayout = go.GetComponent<LayoutElement>();

			ghostLayout.ignoreLayout = true;

			ghostImage.raycastTarget = false;
			ghostImage.sprite = CreateFallbackSprite();
			ghostImage.type = Image.Type.Sliced;
			ghostImage.color = new Color(0.58f, 0.86f, 1f, 0.12f);

			var outline = go.GetComponent<Outline>();
			outline.effectColor = new Color(0.58f, 0.86f, 1f, 0.85f);
			outline.effectDistance = new Vector2(2f, -2f);
			outline.useGraphicAlpha = true;

			ghostRect.anchorMin = new Vector2(0f, 0.5f);
			ghostRect.anchorMax = new Vector2(1f, 0.5f);
			ghostRect.pivot = new Vector2(0.5f, 0.5f);
			ghostRect.sizeDelta = Vector2.zero;
			ghostRect.gameObject.SetActive(false);
		}

		private static Sprite CreateFallbackSprite()
		{
			var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false)
			{
				name = "ListDragGhostTexture",
				hideFlags = HideFlags.HideAndDontSave
			};

			texture.SetPixels(new[]
			{
				Color.white, Color.white,
				Color.white, Color.white
			});
			texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);

			return Sprite.Create(
				texture,
				new Rect(0f, 0f, 2f, 2f),
				new Vector2(0.5f, 0.5f),
				100f,
				0,
				SpriteMeshType.FullRect);
		}
	}
}
