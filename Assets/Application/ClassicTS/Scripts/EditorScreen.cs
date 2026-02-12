using TMPro;
using UnityEngine;

namespace ClassicTilestorm
{
    public class EditorScreen : MonoBehaviour
    {
		[SerializeField] private RectTransform tileSelector;
		public static RectTransform TileSelector { get => instance?.tileSelector; set => instance.tileSelector = value; }

		[SerializeField] private UnityEngine.UI.Image panelTarget;
		public static UnityEngine.UI.Image PanelTarget { get => instance?.panelTarget; set => instance.panelTarget = value; }

		[SerializeField] private UnityEngine.UI.RawImage gridTarget;
		public static UnityEngine.UI.RawImage GridTarget { get => instance?.gridTarget; set => instance.gridTarget = value; }

		[SerializeField] private UnityEngine.UI.RawImage focusTarget;
		public static UnityEngine.UI.RawImage FocusTarget { get => instance?.focusTarget; set => instance.focusTarget = value; }

		[SerializeField] private TMP_Text statusText;
		public static TMP_Text StatusText { get => instance?.statusText; set => instance.statusText = value; }

		private static EditorScreen instance;

		private void Awake() => instance = this;

		private void Start()
		{
			panelTarget.enabled = false;
			gridTarget.enabled = false;
			focusTarget.enabled = false;
			statusText.enabled = false;
		}
	}
}