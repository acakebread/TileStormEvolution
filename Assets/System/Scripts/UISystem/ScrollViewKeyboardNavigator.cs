using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	[RequireComponent(typeof(ScrollRect))]
	public class ScrollViewKeyboardNavigator : MonoBehaviour
	{
		[Header("Navigation Settings")]
		[SerializeField] private bool wrapAround = true;
		[SerializeField] private float fallbackPageStep = 8f;
		[SerializeField] private float pageStepMultiplier = 0.9f;
		[SerializeField] private float initialRepeatDelay = 0.3f;
		[SerializeField] private float repeatRate = 0.05f;

		//[Header("Scroll Settings")]
		//[SerializeField] private float scrollLerpSpeed = 12f;

		private ScrollRect scrollRect;
		private readonly List<Selectable> selectables = new();
		private int lastSelectedIndex = -1;
		private float repeatTimer;
		private bool isRepeating;
		private bool needsRefresh = true;

		private int lastMoveDirection = 0; // -1 up, +1 down

		private void Awake()
		{
			EnsureScrollRect();
		}

		private void OnEnable()
		{
			needsRefresh = true;
			repeatTimer = 0f;
			isRepeating = false;
		}

		private void LateUpdate()
		{
			if (!needsRefresh) return;

			needsRefresh = false;
			RefreshSelectables();

			if (lastSelectedIndex < 0 && selectables.Count > 0)
				SelectIndex(0, false);
		}

		private void Update()
		{
			if (selectables.Count == 0) return;

			HandleKeyboardNavigation();
			HandleRepeatTimer();
		}

		public void ForceRefresh()
		{
			EnsureScrollRect();
			RefreshSelectables();
			needsRefresh = false;
		}

		public void NotifyItemSelected(int index)
		{
			if (index < 0 || index >= selectables.Count) return;

			lastSelectedIndex = index;

			if (selectables[index] is Toggle toggle)
			{
				toggle.isOn = true;
			}
		}

		private void EnsureScrollRect()
		{
			if (!scrollRect)
				scrollRect = GetComponent<ScrollRect>();
		}

		public void RefreshSelectables()
		{
			EnsureScrollRect();

			selectables.Clear();

			if (!scrollRect || !scrollRect.content)
				return;

			foreach (Transform child in scrollRect.content)
			{
				var s = child.GetComponent<Selectable>();
				if (s && s.IsInteractable())
					selectables.Add(s);
			}
		}

		private void HandleKeyboardNavigation()
		{
			int direction = 0;

			if (GetKeyDownWithRepeat(KeyCode.UpArrow) || GetKeyDownWithRepeat(KeyCode.LeftArrow))
				direction = -1;
			else if (GetKeyDownWithRepeat(KeyCode.DownArrow) || GetKeyDownWithRepeat(KeyCode.RightArrow))
				direction = +1;
			else if (GetKeyDownWithRepeat(KeyCode.PageUp))
				direction = -CalculateVisibleItemCount();
			else if (GetKeyDownWithRepeat(KeyCode.PageDown))
				direction = +CalculateVisibleItemCount();
			else if (Input.GetKeyDown(KeyCode.Home))
			{
				lastMoveDirection = -1;
				SelectIndex(0);
				return;
			}
			else if (Input.GetKeyDown(KeyCode.End))
			{
				lastMoveDirection = +1;
				SelectIndex(selectables.Count - 1);
				return;
			}

			if (direction == 0)
				return;

			lastMoveDirection = direction > 0 ? 1 : -1;

			int current = GetCurrentSelectedIndex();
			int next = current + direction;

			bool wrapped = false;

			if (wrapAround && Mathf.Abs(direction) == 1)
			{
				if (next < 0)
				{
					next = selectables.Count - 1;
					wrapped = true;
					lastMoveDirection = 1;   // treat as moving down to bottom
				}
				else if (next >= selectables.Count)
				{
					next = 0;
					wrapped = true;
					lastMoveDirection = -1;  // treat as moving up to top
				}
			}
			else
			{
				next = Mathf.Clamp(next, 0, selectables.Count - 1);
			}

			// Always scroll when wrapped, otherwise use normal visibility check
			SelectIndex(next, forceScroll: wrapped || true);
		}

		private int GetCurrentSelectedIndex()
		{
			if (lastSelectedIndex >= 0 && lastSelectedIndex < selectables.Count)
				return lastSelectedIndex;

			return 0;
		}

		private void SelectIndex(int index, bool forceScroll = true)
		{
			if (index < 0 || index >= selectables.Count) return;

			lastSelectedIndex = index;

			var target = selectables[index];
			target.Select();

			if (target is Toggle toggle)
			{
				toggle.isOn = true;
			}

			if (forceScroll)
				SmartScrollTo(target, lastMoveDirection);
		}

		private void SmartScrollTo(Selectable selectable, int direction)
		{
			if (!scrollRect || !scrollRect.content || !scrollRect.viewport) return;

			var targetRT = selectable.GetComponent<RectTransform>();
			var viewportRT = scrollRect.viewport;

			Canvas.ForceUpdateCanvases();

			Vector3[] item = new Vector3[4];
			Vector3[] vp = new Vector3[4];

			targetRT.GetWorldCorners(item);
			viewportRT.GetWorldCorners(vp);

			float itemTop = item[1].y;
			float itemBottom = item[0].y;
			float vpTop = vp[1].y;
			float vpBottom = vp[0].y;

			bool movingDown = direction > 0;
			bool movingUp = direction < 0;

			if (movingDown && itemBottom >= vpBottom) return;
			if (movingUp && itemTop <= vpTop) return;

			float contentHeight = scrollRect.content.rect.height;
			float viewportHeight = viewportRT.rect.height;
			if (contentHeight <= viewportHeight) return;

			Vector2 local = scrollRect.content.InverseTransformPoint(targetRT.position);

			float targetY = movingDown
				? -local.y - viewportHeight + targetRT.rect.height
				: -local.y - targetRT.rect.height;

			float normalized = Mathf.Clamp01(targetY / (contentHeight - viewportHeight));
			scrollRect.verticalNormalizedPosition = 1f - normalized;
		}

		private int CalculateVisibleItemCount()
		{
			if (!scrollRect || !scrollRect.viewport || selectables.Count == 0)
				return Mathf.RoundToInt(fallbackPageStep);

			float viewportHeight = scrollRect.viewport.rect.height;

			float total = 0f;
			foreach (var s in selectables)
				total += s.GetComponent<RectTransform>().rect.height;

			float avg = total / selectables.Count;
			return Mathf.Clamp(Mathf.FloorToInt((viewportHeight / avg) * pageStepMultiplier), 1, selectables.Count);
		}

		private bool GetKeyDownWithRepeat(KeyCode key)
		{
			if (Input.GetKeyDown(key))
			{
				repeatTimer = initialRepeatDelay;
				isRepeating = true;
				return true;
			}

			if (Input.GetKey(key) && isRepeating && repeatTimer <= 0f)
			{
				repeatTimer = repeatRate;
				return true;
			}

			return false;
		}

		private void HandleRepeatTimer()
		{
			if (isRepeating && AnyNavigationKeyHeld())
				repeatTimer -= Time.deltaTime;
			else
				isRepeating = false;
		}

		private bool AnyNavigationKeyHeld()
		{
			return Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow) ||
				   Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow) ||
				   Input.GetKey(KeyCode.PageUp) || Input.GetKey(KeyCode.PageDown);
		}

		// ── Public nested handler for mouse selection ────────────────
		[AddComponentMenu("")]
		public class ItemSelectionHandler : MonoBehaviour
		{
			private Selectable selectable;

			private void Awake()
			{
				selectable = GetComponent<Selectable>();
				if (selectable == null)
				{
					Debug.LogError("ItemSelectionHandler requires a Selectable", this);
					Destroy(this);
				}
			}

			private void OnEnable()
			{
				var nav = GetComponentInParent<ScrollViewKeyboardNavigator>(true);
				if (nav == null) return;

				if (selectable is Toggle toggle)
				{
					toggle.onValueChanged.AddListener(isOn =>
					{
						if (isOn) nav.NotifyItemSelected(GetIndex());
					});
				}
				else if (selectable is Button button)
				{
					button.onClick.AddListener(() => nav.NotifyItemSelected(GetIndex()));
				}
			}

			private int GetIndex()
			{
				var nav = GetComponentInParent<ScrollViewKeyboardNavigator>(true);
				if (nav != null)
				{
					int idx = nav.selectables.IndexOf(selectable);
					if (idx >= 0) return idx;
				}
				return transform.GetSiblingIndex();
			}
		}
	}
}