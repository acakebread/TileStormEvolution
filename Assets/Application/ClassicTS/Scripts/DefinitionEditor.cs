using UnityEngine;
using UnityEngine.UI;

namespace ClassicTilestorm
{
	public class DefinitionEditor : MonoBehaviour
	{
		// Assign this in the Inspector (the Canvas you created above)
		private Canvas editorCanvas => FindAnyObjectByType<Canvas>();
		//[SerializeField] private Canvas editorCanvas;

		private GameObject modalRoot;
		private bool isOpen = false;

		private const float ModalWidth = 900f;
		private const float ModalHeight = 600f;
		private const float Margin = 20f;

		public void Open()
		{
			if (isOpen) return;

			editorCanvas.gameObject.SetActive(true);
			BuildModal();
			isOpen = true;
		}

		public void Close()
		{
			if (!isOpen) return;

			if (modalRoot != null)
				Destroy(modalRoot);

			editorCanvas.gameObject.SetActive(false);
			isOpen = false;
		}

		private void BuildModal()
		{
			modalRoot = new GameObject("DefinitionEditor_Modal");
			modalRoot.transform.SetParent(editorCanvas.transform, false);

			// ----- Background dimmer -----
			var dimmer = new GameObject("Dimmer").AddComponent<Image>();
			dimmer.transform.SetParent(modalRoot.transform, false);
			dimmer.color = new Color(0f, 0f, 0f, 0.65f);

			var dimmerRt = dimmer.rectTransform;
			dimmerRt.anchorMin = Vector2.zero;
			dimmerRt.anchorMax = Vector2.one;
			dimmerRt.sizeDelta = Vector2.zero;

			// Make dimmer clickable to close (optional – remove if you don't want this)
			var dimmerButton = dimmer.gameObject.AddComponent<Button>();
			dimmerButton.onClick.AddListener(Close);

			// ----- Modal panel -----
			var panel = new GameObject("Panel").AddComponent<Image>();
			panel.transform.SetParent(modalRoot.transform, false);
			panel.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

			var panelRt = panel.rectTransform;
			panelRt.anchorMin = new Vector2(0.5f, 0.5f);
			panelRt.anchorMax = new Vector2(0.5f, 0.5f);
			panelRt.sizeDelta = new Vector2(ModalWidth, ModalHeight);
			panelRt.anchoredPosition = Vector2.zero;

			// Optional subtle border
			var border = new GameObject("Border").AddComponent<Image>();
			border.transform.SetParent(panel.transform, false);
			border.color = new Color(0.3f, 0.3f, 0.4f, 1f);
			var borderRt = border.rectTransform;
			borderRt.anchorMin = Vector2.zero;
			borderRt.anchorMax = Vector2.one;
			borderRt.sizeDelta = new Vector2(-8, -8); // inset

			// ----- Title bar -----
			var titleBar = new GameObject("TitleBar").AddComponent<Image>();
			titleBar.transform.SetParent(panel.transform, false);
			titleBar.color = new Color(0.2f, 0.35f, 0.5f, 1f);

			var titleBarRt = titleBar.rectTransform;
			titleBarRt.anchorMin = new Vector2(0, 1);
			titleBarRt.anchorMax = new Vector2(1, 1);
			titleBarRt.pivot = new Vector2(0.5f, 1f);
			titleBarRt.anchoredPosition = Vector2.zero;
			titleBarRt.sizeDelta = new Vector2(0, 40);

			var titleTextGo = new GameObject("TitleText");
			titleTextGo.transform.SetParent(titleBar.transform, false);
			var titleText = titleTextGo.AddComponent<Text>();
			titleText.text = "Definition Editor";
			titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			titleText.fontSize = 24;
			titleText.alignment = TextAnchor.MiddleCenter;
			titleText.color = Color.white;

			var titleTextRt = titleText.rectTransform;
			titleTextRt.anchorMin = Vector2.zero;
			titleTextRt.anchorMax = Vector2.one;
			titleTextRt.sizeDelta = Vector2.zero;

			// ----- Close button -----
			var closeBtnGo = new GameObject("CloseButton").AddComponent<Button>();
			closeBtnGo.transform.SetParent(titleBar.transform, false);

			var closeImg = closeBtnGo.gameObject.AddComponent<Image>();
			closeImg.color = new Color(0.8f, 0.2f, 0.2f, 1f);

			var closeTextGo = new GameObject("Text");
			closeTextGo.transform.SetParent(closeBtnGo.transform, false);
			var closeText = closeTextGo.AddComponent<Text>();
			closeText.text = "X";
			closeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			closeText.fontSize = 20;
			closeText.alignment = TextAnchor.MiddleCenter;
			closeText.color = Color.white;

			var closeRt = closeText.rectTransform;
			closeRt.anchorMin = new Vector2(1, 0.5f);
			closeRt.anchorMax = new Vector2(1, 0.5f);
			closeRt.sizeDelta = new Vector2(40, 30);
			closeRt.anchoredPosition = new Vector2(-20, 0);

			closeBtnGo.onClick.AddListener(Close);

			// ----- Content area (empty for now – this is where you'll later add list + properties) -----
			var content = new GameObject("Content");
			content.transform.SetParent(panel.transform, false);

			var contentRt = content.AddComponent<RectTransform>();
			contentRt.anchorMin = new Vector2(0, 0);
			contentRt.anchorMax = new Vector2(1, 1);
			contentRt.offsetMin = new Vector2(Margin, Margin + 50); // leave space for title bar
			contentRt.offsetMax = new Vector2(-Margin, -Margin);

			// Optional: add a Vertical Layout Group to make adding future controls easier
			var vlg = content.AddComponent<VerticalLayoutGroup>();
			vlg.spacing = 15;
			vlg.padding = new RectOffset(20, 20, 20, 20);
			vlg.childAlignment = TextAnchor.UpperLeft;
			vlg.childControlWidth = true;
			vlg.childForceExpandWidth = true;
			vlg.childForceExpandHeight = false;
		}

		// Helper to clean up if needed
		private void OnDestroy()
		{
			Close();
		}
	}
}