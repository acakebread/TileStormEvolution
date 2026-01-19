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

		// Pending re-selection after rebuild/content change
		private int pendingReselectIndex = -2; // -2 = none, -1 or >=0 = target index

		private void Awake()
		{
			scrollRect = GetComponent<ScrollRect>();
		}

		private void OnEnable()
		{
			RebuildSelectables();
			pendingReselectIndex = lastSelectedIndex; // queue for scroll/selection
		}

		private void Update()
		{
			CleanupDestroyedItems();

			// Auto-recover if content has items but selectables is empty
			if (selectables.Count == 0 && scrollRect?.content?.childCount > 0)
			{
				StartCoroutine(RebuildAfterFrame());
			}

			// Apply pending re-selection
			if (pendingReselectIndex > -2)
			{
				SelectIndex(pendingReselectIndex, true);
				pendingReselectIndex = -2;
			}

			if (selectables.Count == 0) return;

			HandleKeyboardInput();
			HandleRepeatTimer();
		}

		private IEnumerator RebuildAfterFrame()
		{
			yield return null;
			RebuildSelectables();

			if (pendingReselectIndex > -2)
			{
				SelectIndex(pendingReselectIndex, false);
				pendingReselectIndex = -2;
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
				{
					selectables.Add(sel);

					if (sel is Toggle toggle)
					{
						HookToggleListener(toggle);
					}
				}
			}

			void HookToggleListener(Toggle toggle)
			{
				// Local function to handle selection + scroll (reused)
				void HandleSelection()
				{
					int index = selectables.IndexOf(toggle);
					if (index >= 0)
					{
						NotifyItemSelected(index);
						StartCoroutine(ScrollAfterFrame(toggle));
					}
				}

				// Hook for future changes
				toggle.onValueChanged.AddListener(isOn =>
				{
					if (isOn) HandleSelection();
				});

				// Immediate check for already-on toggles (after rebuild)
				if (toggle.isOn)
				{
					HandleSelection();
				}
			}
		}

		private void CleanupDestroyedItems()
		{
			selectables.RemoveAll(s => s == null || !s.gameObject.activeInHierarchy);
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
			pendingReselectIndex = index;
		}

		private void NotifyItemSelected(int index)
		{
			if (index < 0 || index >= selectables.Count) return;
			lastSelectedIndex = index;
			pendingReselectIndex = -2;
		}

		private void SelectIndex(int index, bool scrollToIt = true)
		{
			// Lazy clamp here - this is the ONLY place we enforce bounds
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
			if (target == null) yield break;
			ScrollTo(target);
			yield return null;
		}

		private void ScrollTo(Selectable selectable)
		{
			if (scrollRect == null || scrollRect.content == null || scrollRect.viewport == null)
				return;

			var itemRT = selectable.GetComponent<RectTransform>();
			if (itemRT == null) return;

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
	}
}
