using UnityEngine;
using UnityEngine.UI;

namespace ClassicTilestorm
{
	public class EffectEditorPanel : UIPanel
	{
		[Header("UI References (optional for testing)")]
		[SerializeField] private Button closeButton;

		private void Awake()
		{
			// Optional: connect close button in code (or do it in Inspector)
			if (closeButton != null)
			{
				closeButton.onClick.AddListener(() => gameObject.SetActive(false));
			}
		}

		public override void OnPanelOpened()
		{
			Debug.Log("Effect Editor panel opened");
			// You can add test content here later (e.g. particle previews, effect lists, etc.)
		}

		public override void OnPanelClosed()
		{
			Debug.Log("Effect Editor panel closed");
		}
	}
}