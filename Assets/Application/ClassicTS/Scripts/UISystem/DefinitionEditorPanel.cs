using UnityEngine;
using UnityEngine.UI;

namespace ClassicTilestorm
{
	public class DefinitionEditorPanel : UIPanel
	{
		// No need to write OnDisable / OnDestroy at all!

		[SerializeField] private Button closeButton; // if you want code wiring

		private void Awake()
		{
			// Optional - only if you prefer code over inspector wiring
			if (closeButton != null)
			{
				closeButton.onClick.AddListener(() => gameObject.SetActive(false));
			}
		}

		public override void OnPanelOpened()
		{
			Debug.Log("Definition Editor opened - load data, etc.");
		}

		public override void OnPanelClosed()
		{
			Debug.Log("Definition Editor closed - optional cleanup");
		}
	}
}