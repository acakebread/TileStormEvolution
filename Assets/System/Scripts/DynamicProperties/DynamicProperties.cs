using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

[DisallowMultipleComponent]
public class DynamicProperties : MonoBehaviour
{
	private Text textComponent;
	private DynamicPropertiesDataManager dataManager;

	// Exposes the internal DynamicPropertiesData for external read-only access
	public DynamicPropertiesData Data => dataManager?.data;

	private void Awake()
	{
		InitializeTextComponent();
		LoadProperties();
	}

	private void OnValidate()
	{
		if (!Application.isPlaying)
		{
			InitializeTextComponent();
			LoadProperties();
		}
	}

	public void InitializeTextComponent()
	{
		if (textComponent == null)
		{
			textComponent = gameObject.GetComponent<Text>();
			if (textComponent == null)
			{
				textComponent = gameObject.AddComponent<Text>();
				if (textComponent == null)
				{
					Debug.LogWarning($"Failed to add Text component to {gameObject.name}.");
					return;
				}
				textComponent.enabled = false;
				textComponent.text = "{\"Properties\":[]}";
				textComponent.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
				// Reset RectTransform to mimic Transform properties
				RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
				if (rectTransform != null)
				{
					rectTransform.localPosition = Vector3.zero;
					rectTransform.localScale = Vector3.one;
					rectTransform.localRotation = Quaternion.identity;
					rectTransform.anchorMin = Vector2.zero;
					rectTransform.anchorMax = Vector2.one;
					rectTransform.anchoredPosition = Vector2.zero;
					rectTransform.sizeDelta = Vector2.zero;
				}
			}
			else
			{
				// Ensure existing Text component is properly configured
				textComponent.enabled = false;
				if (string.IsNullOrEmpty(textComponent.text))
				{
					textComponent.text = "{\"Properties\":[]}";
				}
				textComponent.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
				// Reset existing RectTransform
				RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
				if (rectTransform != null)
				{
					rectTransform.localPosition = Vector3.zero;
					rectTransform.localScale = Vector3.one;
					rectTransform.localRotation = Quaternion.identity;
					rectTransform.anchorMin = Vector2.zero;
					rectTransform.anchorMax = Vector2.one;
					rectTransform.anchoredPosition = Vector2.zero;
					rectTransform.sizeDelta = Vector2.zero;
				}
			}

			// Ensure Canvas is disabled and hidden
			Canvas canvas = gameObject.GetComponent<Canvas>();
			if (canvas != null)
			{
				canvas.enabled = false;
				canvas.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
				// Prevent Canvas from being added unnecessarily
				if (canvas.GetComponent<CanvasScaler>() == null && canvas.GetComponent<GraphicRaycaster>() == null)
				{
					DestroyImmediate(canvas);
				}
			}

			// Ensure CanvasRenderer is hidden
			CanvasRenderer canvasRenderer = gameObject.GetComponent<CanvasRenderer>();
			if (canvasRenderer != null)
			{
				canvasRenderer.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
			}
		}
	}

	public void LoadProperties()
	{
		if (textComponent == null)
		{
			InitializeTextComponent();
			if (textComponent == null)
			{
				Debug.LogWarning($"Text component could not be initialized for {gameObject.name}.");
				dataManager = new DynamicPropertiesDataManager("{\"Properties\":[]}");
				return;
			}
		}
		dataManager = new DynamicPropertiesDataManager(textComponent.text);
	}

	public void SaveProperties()
	{
		if (textComponent == null)
		{
			Debug.LogWarning($"Text component is missing in SaveProperties for {gameObject.name}. Reinitializing...");
			InitializeTextComponent();
			if (textComponent == null)
			{
				Debug.LogWarning($"Failed to reinitialize Text component for {gameObject.name}. Cannot save properties.");
				return;
			}
		}
		if (dataManager == null)
		{
			Debug.LogWarning($"DataManager is null in SaveProperties for {gameObject.name}. Reinitializing...");
			LoadProperties();
			if (dataManager == null)
			{
				Debug.LogWarning($"Failed to initialize DataManager for {gameObject.name}. Cannot save properties.");
				return;
			}
		}
		textComponent.text = dataManager.SaveToJson();
#if UNITY_EDITOR
		UnityEditor.EditorUtility.SetDirty(textComponent);
		UnityEditor.EditorUtility.SetDirty(this);
#endif
	}

	public DynamicPropertiesData GetData()
	{
		if (dataManager == null)
		{
			LoadProperties();
		}
		return dataManager?.data ?? new DynamicPropertiesData();
	}

	public void SetData(DynamicPropertiesData newData)
	{
		if (newData == null)
		{
			Debug.LogWarning($"SetData received null data for {gameObject.name}. Initializing empty data.");
			newData = new DynamicPropertiesData();
		}
		dataManager = new DynamicPropertiesDataManager(JsonUtility.ToJson(newData));
		SaveProperties();
	}

	// Delegate to dataManager
	public IReadOnlyList<DynamicProperty> Properties => dataManager?.Properties ?? new List<DynamicProperty>().AsReadOnly();
	public IEnumerable<DynamicProperty> GetPropertiesByType(PropertyType type) => dataManager?.GetPropertiesByType(type) ?? Enumerable.Empty<DynamicProperty>();

	public void AddFloat(string name, float value) => dataManager?.AddFloat(name, value);
	public void AddInt(string name, int value) => dataManager?.AddInt(name, value);
	public void AddString(string name, string value) => dataManager?.AddString(name, value);
	public void AddBool(string name, bool value) => dataManager?.AddBool(name, value);
	public bool RemoveProperty(string name) => dataManager?.RemoveProperty(name) ?? false;

	public bool HasFloat(string name) => dataManager?.HasFloat(name) ?? false;
	public bool TryGetFloat(string name, out float value) => dataManager?.TryGetFloat(name, out value) ?? (value = default) == default;
	public float GetFloat(string name) => dataManager?.GetFloat(name) ?? throw new KeyNotFoundException($"Float property '{name}' not found.");

	public bool HasInt(string name) => dataManager?.HasInt(name) ?? false;
	public bool TryGetInt(string name, out int value) => dataManager?.TryGetInt(name, out value) ?? (value = default) == default;
	public int GetInt(string name) => dataManager?.GetInt(name) ?? throw new KeyNotFoundException($"Int property '{name}' not found.");

	public bool HasString(string name) => dataManager?.HasString(name) ?? false;
	public bool TryGetString(string name, out string value) => dataManager?.TryGetString(name, out value) ?? (value = default) == default;
	public string GetString(string name) => dataManager?.GetString(name) ?? throw new KeyNotFoundException($"String property '{name}' not found.");

	public bool HasBool(string name) => dataManager?.HasBool(name) ?? false;
	public bool TryGetBool(string name, out bool value) => dataManager?.TryGetBool(name, out value) ?? (value = default) == default;
	public bool GetBool(string name) => dataManager?.GetBool(name) ?? throw new KeyNotFoundException($"Bool property '{name}' not found.");
}