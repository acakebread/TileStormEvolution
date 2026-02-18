using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MassiveHadronLtd.UI;

namespace MassiveHadronLtd
{
	[RequireComponent(typeof(Selectable))]
	public class DropdownKeyboardNavigator : MonoBehaviour
	{
		[Header("Navigation Settings")]
		[SerializeField] private bool wrapAround = true;
		[SerializeField] private float repeatDelay = 0.4f;
		[SerializeField] private float repeatRate = 0.08f;

		// State
		private Dropdown legacyDropdown;
		private TMP_Dropdown tmpDropdown;
		private int currentHighlightedIndex = -1;
		private float repeatTimer;
		private bool isRepeating;

		private void Awake()
		{
			legacyDropdown = GetComponent<Dropdown>();
			tmpDropdown = GetComponent<TMP_Dropdown>();

			if (legacyDropdown == null && tmpDropdown == null)
			{
				Debug.LogWarning($"No Dropdown or TMP_Dropdown found on {gameObject.name}", this);
				enabled = false;
				return;
			}
		}

		private void Update()
		{
			if (!UIFocusManager.IsInFocus(gameObject))
			{
				// reset state
				currentHighlightedIndex = -1;
				return;
			}

			// Just gained focus → start from current value
			if (currentHighlightedIndex < 0)
			{
				currentHighlightedIndex = GetCurrentValue();
				// Debug.Log($"Dropdown gained focus: {gameObject.name}");
			}

			HandleRepeatTimer();

			int direction = 0;
			if (GetKeyDownWithRepeat(KeyCode.UpArrow)) direction = -1;
			else if (GetKeyDownWithRepeat(KeyCode.DownArrow)) direction = +1;
			else if (InputX.GetKeyDown(KeyCode.Home)) { SetValue(0); return; }
			else if (InputX.GetKeyDown(KeyCode.End)) { SetValue(GetOptionCount() - 1); return; }

			if (direction != 0)
			{
				int count = GetOptionCount();
				if (count <= 0) return;

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
				SetPreviewValue(next);
			}
			else if (InputX.GetKeyDown(KeyCode.Return) || InputX.GetKeyDown(KeyCode.KeypadEnter) || InputX.GetKeyDown(KeyCode.Space))
			{
				ConfirmSelection();
			}
		}

		// ── Helpers ──
		private int GetCurrentValue()
		{
			if (tmpDropdown != null) return tmpDropdown.value;
			if (legacyDropdown != null) return legacyDropdown.value;
			return 0;
		}

		private int GetOptionCount()
		{
			if (tmpDropdown != null) return tmpDropdown.options.Count;
			if (legacyDropdown != null) return legacyDropdown.options.Count;
			return 0;
		}

		private void SetPreviewValue(int index)
		{
			if (index < 0 || index >= GetOptionCount()) return;

			if (tmpDropdown != null)
			{
				tmpDropdown.value = index;
				tmpDropdown.RefreshShownValue();
			}
			else if (legacyDropdown != null)
			{
				legacyDropdown.value = index;
				legacyDropdown.RefreshShownValue();
			}
		}

		private void ConfirmSelection()
		{
			if (currentHighlightedIndex >= 0 && currentHighlightedIndex < GetOptionCount())
			{
				if (tmpDropdown != null) tmpDropdown.value = currentHighlightedIndex;
				else if (legacyDropdown != null) legacyDropdown.value = currentHighlightedIndex;

				RefreshShownValue();
			}
			HideDropdown();
		}

		private void SetValue(int index)
		{
			if (index < 0 || index >= GetOptionCount()) return;

			if (tmpDropdown != null) tmpDropdown.value = index;
			else if (legacyDropdown != null) legacyDropdown.value = index;

			RefreshShownValue();
			HideDropdown();
		}

		private void RefreshShownValue()
		{
			if (tmpDropdown != null) tmpDropdown.RefreshShownValue();
			else if (legacyDropdown != null) legacyDropdown.RefreshShownValue();
		}

		private void HideDropdown()
		{
			if (tmpDropdown != null) tmpDropdown.Hide();
			else if (legacyDropdown != null) legacyDropdown.Hide();
		}

		private bool GetKeyDownWithRepeat(KeyCode key)
		{
			if (InputX.GetKeyDown(key))
			{
				repeatTimer = repeatDelay;
				isRepeating = true;
				return true;
			}
			if (InputX.GetKey(key) && isRepeating && repeatTimer <= 0f)
			{
				repeatTimer = repeatRate;
				return true;
			}
			return false;
		}

		private void HandleRepeatTimer()
		{
			if (isRepeating && (InputX.GetKey(KeyCode.UpArrow) || InputX.GetKey(KeyCode.DownArrow)))
				repeatTimer -= Time.deltaTime;
			else
				isRepeating = false;
		}
	}
}