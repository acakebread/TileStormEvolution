using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

namespace MassiveHadronLtd
{
	[RequireComponent(typeof(TMP_Dropdown))]
	public class DropdownKeyboardNavigator : MonoBehaviour,
		IPointerEnterHandler, IPointerExitHandler,
		ISelectHandler, IDeselectHandler
	{
		[Header("Navigation Settings")]
		[SerializeField, Tooltip("Wrap selection when reaching top/bottom")]
		private bool wrapAround = true;

		[SerializeField, Tooltip("Initial delay before key repeat starts (seconds)")]
		private float repeatDelay = 0.4f;

		[SerializeField, Tooltip("Delay between repeated key events (seconds)")]
		private float repeatRate = 0.08f;

		// State
		private TMP_Dropdown dropdown;
		private bool isHovered;
		private bool isKeyboardSelected;
		private int currentHighlightedIndex = -1;
		private float repeatTimer;
		private bool isRepeating;

		/// <summary>
		/// Public property for ScrollViewKeyboardNavigator to check if this dropdown wants arrow key priority
		/// </summary>
		public bool IsFocusedOrNavigating =>
			enabled &&
			gameObject.activeInHierarchy &&
			(isHovered || isKeyboardSelected || (dropdown != null && dropdown.IsExpanded));

		private void Awake()
		{
			dropdown = GetComponent<TMP_Dropdown>();
			if (dropdown == null)
			{
				Debug.LogError($"[DropdownKeyboardNavigator] Missing TMP_Dropdown on {gameObject.name}", this);
				enabled = false;
			}
		}

		private void Update()
		{
			if (!IsFocusedOrNavigating)
			{
				if (currentHighlightedIndex >= 0)
				{
					// Optional: uncomment to log when we lose control
					// Debug.Log($"DropdownKeyboardNavigator lost control: {gameObject.name}");
				}
				currentHighlightedIndex = -1;
				isRepeating = false;
				return;
			}

			// Just activated → start from current value
			if (currentHighlightedIndex < 0)
			{
				currentHighlightedIndex = dropdown.value;
				// Debug.Log($"DropdownKeyboardNavigator activated: {gameObject.name} (hover={isHovered}, selected={isKeyboardSelected}, expanded={dropdown.IsExpanded})");
			}

			HandleRepeatTimer();

			int direction = 0;

			if (GetKeyDownWithRepeat(KeyCode.UpArrow)) direction = -1;
			else if (GetKeyDownWithRepeat(KeyCode.DownArrow)) direction = +1;
			else if (Input.GetKeyDown(KeyCode.Home)) { SetValue(0); return; }
			else if (Input.GetKeyDown(KeyCode.End)) { SetValue(dropdown.options.Count - 1); return; }

			if (direction != 0)
			{
				int count = dropdown.options.Count;
				if (count == 0) return;

				int next = currentHighlightedIndex + direction;

				if (wrapAround)
				{
					if (next < 0) next = count - 1;
					else if (next >= count) next = 0;
				}
				else
				{
					next = Mathf.Clamp(next, 0, count - 1);
				}

				currentHighlightedIndex = next;
				PreviewValue(next);
			}
			else if (Input.GetKeyDown(KeyCode.Return) ||
					 Input.GetKeyDown(KeyCode.KeypadEnter) ||
					 Input.GetKeyDown(KeyCode.Space))
			{
				ConfirmSelection();
			}
		}

		// ──────────────────────────────────────────────────────────────
		// Pointer / Selection events
		// ──────────────────────────────────────────────────────────────

		public void OnPointerEnter(PointerEventData eventData)
		{
			isHovered = true;
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			isHovered = false;
		}

		public void OnSelect(BaseEventData eventData)
		{
			isKeyboardSelected = true;
		}

		public void OnDeselect(BaseEventData eventData)
		{
			isKeyboardSelected = false;
		}

		// ──────────────────────────────────────────────────────────────
		// Value control
		// ──────────────────────────────────────────────────────────────

		private void PreviewValue(int index)
		{
			if (index < 0 || index >= dropdown.options.Count) return;

			dropdown.value = index;
			dropdown.RefreshShownValue();
			// If you do NOT want to preview changes when the dropdown is closed,
			// add this condition:
			// if (!dropdown.IsExpanded) return;
		}

		private void ConfirmSelection()
		{
			if (currentHighlightedIndex >= 0 && currentHighlightedIndex < dropdown.options.Count)
			{
				dropdown.value = currentHighlightedIndex;
				dropdown.RefreshShownValue();
			}
			dropdown.Hide();
		}

		private void SetValue(int index)
		{
			if (index < 0 || index >= dropdown.options.Count) return;
			dropdown.value = index;
			dropdown.RefreshShownValue();
			dropdown.Hide();
		}

		// ──────────────────────────────────────────────────────────────
		// Key repeat logic
		// ──────────────────────────────────────────────────────────────

		private bool GetKeyDownWithRepeat(KeyCode key)
		{
			if (Input.GetKeyDown(key))
			{
				repeatTimer = repeatDelay;
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
			if (isRepeating && (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow)))
			{
				repeatTimer -= Time.deltaTime;
			}
			else
			{
				isRepeating = false;
			}
		}
	}
}