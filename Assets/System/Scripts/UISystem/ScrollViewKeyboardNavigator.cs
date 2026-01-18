using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

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

		private ScrollRect scrollRect;
		private readonly List<Selectable> selectables = new();
		private int lastSelectedIndex = -1;
		private float repeatTimer;
		private bool isRepeating;

		private int pendingSelectIndex = -2; // -2 = no pending, -1 or >=0 = select this after rebuild

		private void Awake()
		{
			scrollRect = GetComponent<ScrollRect>();
		}

		private void OnEnable()
		{
			RebuildSelectables();
		}

		private void Update()
		{
			CleanupDestroyedItems();

			if (selectables.Count == 0 && scrollRect?.content?.childCount > 0)
			{
				StartCoroutine(RebuildAfterFrame());
			}

			if (selectables.Count == 0) return;

			// Apply any pending selection from previous frame/rebuild
			if (pendingSelectIndex > -2)
			{
				SelectIndex(pendingSelectIndex, false);
				pendingSelectIndex = -2;
			}

			HandleKeyboardInput();
			HandleRepeatTimer();
		}

		private void CleanupDestroyedItems()
		{
			selectables.RemoveAll(s => s == null || !s.gameObject.activeInHierarchy);
			if (lastSelectedIndex >= selectables.Count)
				lastSelectedIndex = selectables.Count - 1;
		}

		private IEnumerator RebuildAfterFrame()
		{
			yield return null;
			RebuildSelectables();

			// Apply pending selection after rebuild
			if (pendingSelectIndex > -2)
			{
				SelectIndex(pendingSelectIndex, false);
				pendingSelectIndex = -2;
			}
		}

		public void ForceRefresh()
		{
			RebuildSelectables();
		}

		public void ClearAndRebuild()
		{
			int desiredIndex = lastSelectedIndex;

			CleanupDestroyedItems();
			selectables.Clear();

			RebuildSelectables();

			// Schedule selection for next frame to avoid race with destroyed objects
			pendingSelectIndex = desiredIndex;

			// Smart clamp
			if (pendingSelectIndex >= selectables.Count)
				pendingSelectIndex = selectables.Count - 1;
			else if (pendingSelectIndex < 0 && selectables.Count > 0)
				pendingSelectIndex = 0;
		}

		private void RebuildSelectables()
		{
			selectables.Clear();

			if (scrollRect?.content == null) return;

			foreach (Transform child in scrollRect.content)
			{
				if (child == null) continue;
				var sel = child.GetComponent<Selectable>();
				if (sel != null && sel.IsInteractable())
					selectables.Add(sel);
			}
		}

		private void HandleKeyboardInput()
		{
			CleanupDestroyedItems();

			int direction = 0;

			if (GetKeyDownWithRepeat(KeyCode.UpArrow) || GetKeyDownWithRepeat(KeyCode.LeftArrow))
				direction = -1;
			else if (GetKeyDownWithRepeat(KeyCode.DownArrow) || GetKeyDownWithRepeat(KeyCode.RightArrow))
				direction = +1;
			else if (GetKeyDownWithRepeat(KeyCode.PageUp))
				direction = -CalculatePageStep();
			else if (GetKeyDownWithRepeat(KeyCode.PageDown))
				direction = +CalculatePageStep();
			else if (Input.GetKeyDown(KeyCode.Home))
			{
				ScheduleSelect(0);
				return;
			}
			else if (Input.GetKeyDown(KeyCode.End))
			{
				ScheduleSelect(selectables.Count - 1);
				return;
			}

			if (direction == 0) return;

			int current = Mathf.Clamp(lastSelectedIndex, 0, selectables.Count - 1);
			int next = current + direction;

			if (wrapAround && Mathf.Abs(direction) == 1)
			{
				if (next < 0) next = selectables.Count - 1;
				else if (next >= selectables.Count) next = 0;
			}
			else
			{
				next = Mathf.Clamp(next, 0, selectables.Count - 1);
			}

			ScheduleSelect(next);
		}

		private void ScheduleSelect(int index)
		{
			pendingSelectIndex = index;
		}

		public void NotifyItemSelected(int index)
		{
			if (index < 0 || index >= selectables.Count) return;
			lastSelectedIndex = index;
			pendingSelectIndex = -2; // Clear pending if user manually selects
		}

		private void SelectIndex(int index, bool scrollToIt = true)
		{
			index = Mathf.Clamp(index, -1, selectables.Count - 1);
			if (index < 0) return;

			var target = selectables[index];
			if (target == null) return;

			lastSelectedIndex = index;
			target.Select();

			if (target is Toggle toggle)
				toggle.isOn = true;

			if (scrollToIt)
				StartCoroutine(ScrollAfterFrame(target));
		}

		private IEnumerator ScrollAfterFrame(Selectable target)
		{
			yield return null;
			if (target == null) yield break; // Safety against destroy race
			ScrollTo(target);
		}

		private void ScrollTo(Selectable selectable)
		{
			if (scrollRect == null || scrollRect.content == null || scrollRect.viewport == null)
				return;

			var itemRT = selectable.GetComponent<RectTransform>();
			if (itemRT == null) return; // Critical safety - prevents MissingReferenceException

			Canvas.ForceUpdateCanvases();

			RectTransform content = scrollRect.content;
			RectTransform viewport = scrollRect.viewport;

			float contentHeight = content.rect.height;
			float viewportHeight = viewport.rect.height;

			float scrollableHeight = contentHeight - viewportHeight;
			if (scrollableHeight <= 0f) return;

			Vector3[] itemCorners = new Vector3[4];
			itemRT.GetWorldCorners(itemCorners);

			for (int i = 0; i < 4; i++)
				itemCorners[i] = content.InverseTransformPoint(itemCorners[i]);

			float itemTop = itemCorners[1].y;
			float itemBottom = itemCorners[0].y;

			float currentY = Mathf.Lerp(0f, -scrollableHeight, 1f - scrollRect.verticalNormalizedPosition);

			float newY = currentY;

			if (itemTop > currentY)
				newY = itemTop;
			else if (itemBottom < currentY - viewportHeight)
				newY = itemBottom + viewportHeight;

			newY = Mathf.Clamp(newY, -scrollableHeight, 0f);

			scrollRect.verticalNormalizedPosition =
				1f - Mathf.InverseLerp(0f, -scrollableHeight, newY);
		}

		private int CalculatePageStep()
		{
			CleanupDestroyedItems();

			if (scrollRect == null || scrollRect.viewport == null || selectables.Count == 0)
				return Mathf.RoundToInt(fallbackPageStep);

			float viewportH = scrollRect.viewport.rect.height;
			float totalHeight = 0f;
			int count = 0;

			foreach (var s in selectables)
			{
				var rt = s.GetComponent<RectTransform>();
				if (rt == null) continue;
				totalHeight += rt.rect.height;
				count++;
			}

			if (count == 0) return Mathf.RoundToInt(fallbackPageStep);

			float avgHeight = totalHeight / count;
			int step = Mathf.FloorToInt((viewportH / avgHeight) * pageStepMultiplier);
			return Mathf.Clamp(step, 1, selectables.Count);
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
			if (isRepeating && AnyNavigationKeyPressed())
				repeatTimer -= Time.deltaTime;
			else
				isRepeating = false;
		}

		private bool AnyNavigationKeyPressed()
		{
			return Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow) ||
				   Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow) ||
				   Input.GetKey(KeyCode.PageUp) || Input.GetKey(KeyCode.PageDown);
		}

		public IEnumerator ForceReselectAfterFrame(int targetIndex)
		{
			yield return null; // wait until end of frame — all new toggles are fully initialized
			CleanupDestroyedItems(); // safety

			// Clamp to current list
			int clamped = Mathf.Clamp(targetIndex, -1, selectables.Count - 1);

			if (clamped >= 0 && clamped < selectables.Count)
			{
				var target = selectables[clamped];
				if (target != null)
				{
					lastSelectedIndex = clamped;
					target.Select();

					if (target is Toggle toggle)
						toggle.isOn = true;

					StartCoroutine(ScrollAfterFrame(target));
				}
			}
		}

		// ──────────────────────────────────────────────────────────────
		// Item handler with delayed registration to avoid race conditions
		// ──────────────────────────────────────────────────────────────
		[AddComponentMenu("")]
		public class ItemSelectionHandler : MonoBehaviour
		{
			private ScrollViewKeyboardNavigator navigator;
			private Selectable selectable;

			private void Awake()
			{
				selectable = GetComponent<Selectable>();
				if (selectable == null)
				{
					Destroy(this);
					return;
				}
			}

			private void OnEnable()
			{
				StartCoroutine(TryRegisterDelayed());
			}

			private IEnumerator TryRegisterDelayed()
			{
				yield return null; // Wait one frame - crucial for rebuild timing
				TryRegister();
			}

			private void TryRegister()
			{
				if (navigator != null) return;

				navigator = GetComponentInParent<ScrollViewKeyboardNavigator>(true);
				if (navigator == null) return;

				if (selectable is Toggle toggle)
				{
					toggle.onValueChanged.AddListener(isOn =>
					{
						if (isOn)
						{
							int index = navigator.selectables.IndexOf(selectable);
							if (index >= 0)
							{
								navigator.NotifyItemSelected(index);
								navigator.StartCoroutine(navigator.ScrollAfterFrame(selectable));
							}
						}
					});
				}
			}
		}
	}
}