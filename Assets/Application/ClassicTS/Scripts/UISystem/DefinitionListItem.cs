// DefinitionListItem.cs
using UnityEngine;
using UnityEngine.UI;

namespace ClassicTilestorm
{
	public class DefinitionListItem : MonoBehaviour
	{
		[SerializeField] private Button button;
		[SerializeField] private TMPro.TMP_Text label; // optional reference

		public string DefinitionId { get; private set; }

		public void Initialize(string defId, System.Action<string> onClick)
		{
			DefinitionId = defId;

			if (button != null)
			{
				button.onClick.RemoveAllListeners();
				button.onClick.AddListener(() => onClick?.Invoke(defId));
			}

			if (label != null)
			{
				label.text = $"{defId}";// label.text = $"{defId}{ResourceManager.GetDefinition(defId)?.model ?? "—"}";//label.text = $"{defId}\n<size=80%>{ResourceManager.GetDefinition(defId)?.model ?? "—"}</size>";
			}
		}

		public void SetSelected(bool isSelected)
		{
			var img = GetComponent<Image>();
			if (img != null)
			{
				img.color = isSelected
					? new Color(0.3f, 0.6f, 1f, 0.9f)
					: new Color(1f, 1f, 1f, 0.18f);
			}
		}
	}
}