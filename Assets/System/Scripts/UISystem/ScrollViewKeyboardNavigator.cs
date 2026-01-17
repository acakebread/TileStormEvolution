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

		private ScrollRect scrollRect;
		private readonly List<Selectable> selectables = new();
		private int lastSelectedIndex = -1;
		private float repeatTimer;
		private bool isRepeating;

		private void Awake()
		{
			scrollRect = GetComponent<ScrollRect>();
		}

		private void OnEnable()
		{
			ForceRefresh();
		}

		private void Update()
		{
			CleanupDestroyed();

			// Auto-recover when list is empty but content has items now
			if (selectables.Count == 0 && scrollRect?.content?.childCount > 0)
			{
				RebuildSelectables();
			}

			if (selectables.Count == 0) return;

			HandleKeyboardInput();
			HandleRepeatTimer();
		}

		private void CleanupDestroyed()
		{
			if (selectables.RemoveAll(s => s == null) > 0)
			{
				lastSelectedIndex = Mathf.Clamp(lastSelectedIndex, -1, selectables.Count - 1);
			}
		}

		public void ForceRefresh()
		{
			RebuildSelectables();
		}

		public void ClearAndRebuild()
		{
			// Try to remember meaningful context before we nuke everything
			int desiredIndex = lastSelectedIndex;
			Selectable previouslySelected = null;

			if (lastSelectedIndex >= 0 && lastSelectedIndex < selectables.Count)
			{
				previouslySelected = selectables[lastSelectedIndex];
			}

			CleanupDestroyed();
			selectables.Clear();

			RebuildSelectables();

			// Smart restoration logic
			if (previouslySelected != null)
			{
				// 1. Best case: same object still exists (most common after move/insert)
				int newIndex = selectables.IndexOf(previouslySelected);
				if (newIndex >= 0)
				{
					lastSelectedIndex = newIndex;
					SelectIndex(newIndex, false); // false = no forced scroll
					return;
				}
			}

			// 2. Delete case: try to keep roughly the same position (usually selects the one above)
			//    If we deleted item at index N, we want index N-1 (or N if at top)
			if (desiredIndex > 0)
			{
				lastSelectedIndex = desiredIndex - 1;  // ← this is the key line for delete
			}
			else if (desiredIndex == 0 && selectables.Count > 0)
			{
				lastSelectedIndex = 0;                 // stay at top if deleted first item
			}
			else
			{
				lastSelectedIndex = Mathf.Clamp(desiredIndex, 0, selectables.Count - 1);
			}

			// Final safety
			if (lastSelectedIndex >= 0 && lastSelectedIndex < selectables.Count)
			{
				SelectIndex(lastSelectedIndex, false);
			}
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

			// IMPORTANT: do NOT reset lastSelectedIndex here anymore
			// We handle it in ClearAndRebuild instead
		}

		private void HandleKeyboardInput()
		{
			CleanupDestroyed();   // ← important: clean before we try to use anything

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
				SelectIndex(0, true);
				return;
			}
			else if (Input.GetKeyDown(KeyCode.End))
			{
				SelectIndex(selectables.Count - 1, true);
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

			SelectIndex(next, true);
		}

		public void NotifyItemSelected(int index)
		{
			if (index < 0 || index >= selectables.Count) return;
			lastSelectedIndex = index;
		}

		private void SelectIndex(int index, bool scrollToIt = true)
		{
			if (index < 0 || index >= selectables.Count) return;

			var target = selectables[index];
			if (target == null) return;

			lastSelectedIndex = index;
			target.Select();

			if (target is Toggle toggle)
				toggle.isOn = true;

			if (scrollToIt)
			{
				if (index == 0)
				{
					scrollRect.verticalNormalizedPosition = 1f;
				}
				else if (index == selectables.Count - 1)
				{
					scrollRect.verticalNormalizedPosition = 0f;
				}
				else if (Mathf.Abs(index - lastSelectedIndex) > 1) // big jump = page up/down/home/end
				{
					StartCoroutine(ScrollAfterFrame(target));
				}
				else
				{
					ScrollTo(target);
				}
			}
		}

		private System.Collections.IEnumerator ScrollAfterFrame(Selectable target)
		{
			yield return null;
			ScrollTo(target);
		}

		private void ScrollTo(Selectable selectable)
		{
			if (scrollRect == null || scrollRect.content == null || scrollRect.viewport == null)
				return;

			var itemRT = selectable.GetComponent<RectTransform>();
			if (!itemRT) return;

			Canvas.ForceUpdateCanvases();

			RectTransform content = scrollRect.content;
			RectTransform viewport = scrollRect.viewport;

			float contentHeight = content.rect.height;
			float viewportHeight = viewport.rect.height;

			float scrollableHeight = contentHeight - viewportHeight;
			if (scrollableHeight <= 0f)
				return;

			Vector3[] itemCorners = new Vector3[4];
			itemRT.GetWorldCorners(itemCorners);

			for (int i = 0; i < 4; i++)
				itemCorners[i] = content.InverseTransformPoint(itemCorners[i]);

			float itemTop = itemCorners[1].y;
			float itemBottom = itemCorners[0].y;

			float currentY = Mathf.Lerp(0f, -scrollableHeight, 1f - scrollRect.verticalNormalizedPosition);

			float newY = currentY;

			if (itemTop > currentY)
			{
				newY = itemTop;
			}
			else if (itemBottom < currentY - viewportHeight)
			{
				newY = itemBottom + viewportHeight;
			}

			newY = Mathf.Clamp(newY, -scrollableHeight, 0f);

			scrollRect.verticalNormalizedPosition =
				1f - Mathf.InverseLerp(0f, -scrollableHeight, newY);
		}

		private int CalculatePageStep()
		{
			CleanupDestroyed();   // ← crucial: prevents MissingReferenceException here

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

		// ──────────────────────────────────────────────────────────────
		// Item handler (unchanged)
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
				TryRegister();
			}

			private void OnTransformParentChanged()
			{
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
							if (index >= 0) navigator.NotifyItemSelected(index);
						}
					});
				}
			}
		}
	}
}