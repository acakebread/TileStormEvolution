using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class DynamicProperty
{
	public string Name;
	public PropertyType Type;
	public float FloatValue;
	public int IntValue;
	public string StringValue;
	public bool BoolValue;
}

public enum PropertyType
{
	Float,
	Int,
	String,
	Bool
}

[System.Serializable]
public class DynamicPropertiesData
{
	public List<DynamicProperty> Properties = new List<DynamicProperty>();
}

[DisallowMultipleComponent]
public class DynamicProperties : MonoBehaviour
{
	private Text textComponent;
	private DynamicPropertiesData data; // Runtime deserialized data
	private Dictionary<string, DynamicProperty> propertyMap = new Dictionary<string, DynamicProperty>(StringComparer.OrdinalIgnoreCase); // Case-insensitive

	private void Awake()
	{
		InitializeTextComponent();
		LoadProperties();
	}

	public void InitializeTextComponent()
	{
		if (textComponent == null)
		{
			textComponent = gameObject.GetComponent<Text>();
			if (textComponent == null)
			{
				textComponent = gameObject.AddComponent<Text>();
				textComponent.enabled = false; // Disable to avoid rendering
				textComponent.text = "{\"Properties\":[]}"; // Initialize with empty JSON
				textComponent.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable; // Hide and make read-only

				// Hide and disable Canvas and CanvasRenderer
				Canvas canvas = gameObject.GetComponent<Canvas>();
				if (canvas != null)
				{
					canvas.enabled = false;
					canvas.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
				}
				CanvasRenderer canvasRenderer = gameObject.GetComponent<CanvasRenderer>();
				if (canvasRenderer != null)
				{
					canvasRenderer.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
				}
			}
		}
	}

	private void LoadProperties()
	{
		if (textComponent == null)
		{
			InitializeTextComponent();
			if (textComponent == null)
			{
				Debug.LogWarning($"Text component could not be initialized for {gameObject.name}.");
				data = new DynamicPropertiesData();
				return;
			}
		}

		try
		{
			data = JsonUtility.FromJson<DynamicPropertiesData>(textComponent.text);
			if (data == null || data.Properties == null)
			{
				Debug.LogWarning($"Invalid or empty JSON data in Text component for {gameObject.name}. Initializing empty data.");
				data = new DynamicPropertiesData();
				textComponent.text = JsonUtility.ToJson(data);
			}
			propertyMap.Clear();
			foreach (var prop in data.Properties)
			{
				if (!string.IsNullOrEmpty(prop.Name) && !propertyMap.ContainsKey(prop.Name))
				{
					propertyMap[prop.Name] = prop;
				}
				else
				{
					Debug.LogWarning($"Duplicate or invalid property name '{prop.Name}' ignored in {gameObject.name}.");
				}
			}
		}
		catch (Exception e)
		{
			Debug.LogWarning($"Failed to parse properties JSON for {gameObject.name}: {e.Message}. Initializing empty data.");
			data = new DynamicPropertiesData();
			textComponent.text = JsonUtility.ToJson(data);
		}
	}

	public void SaveProperties()
	{
		if (textComponent == null)
		{
			Debug.LogWarning($"Text component is missing in SaveProperties for {gameObject.name}. Reinitializing...");
			InitializeTextComponent();
		}
		textComponent.text = JsonUtility.ToJson(data);
#if UNITY_EDITOR
		UnityEditor.EditorUtility.SetDirty(textComponent);
		UnityEditor.EditorUtility.SetDirty(this);
#endif
	}

	// Expose data for Editor syncing
	public DynamicPropertiesData GetData()
	{
		if (data == null)
		{
			LoadProperties();
		}
		return data;
	}

	public void SetData(DynamicPropertiesData newData)
	{
		data = newData ?? new DynamicPropertiesData();
		propertyMap.Clear();
		foreach (var prop in data.Properties)
		{
			if (!string.IsNullOrEmpty(prop.Name) && !propertyMap.ContainsKey(prop.Name))
			{
				propertyMap[prop.Name] = prop;
			}
		}
		SaveProperties();
	}

	// Enumeration
	public IReadOnlyList<DynamicProperty> Properties => data.Properties.AsReadOnly();
	public IEnumerable<DynamicProperty> GetPropertiesByType(PropertyType type) => data.Properties.Where(p => p.Type == type);

	// Programmatic Add Methods
	public void AddFloat(string name, float value)
	{
		if (string.IsNullOrEmpty(name)) throw new ArgumentException("Property name cannot be empty.");
		if (propertyMap.ContainsKey(name)) throw new ArgumentException($"Property '{name}' already exists.");
		var prop = new DynamicProperty { Name = name, Type = PropertyType.Float, FloatValue = value };
		data.Properties.Add(prop);
		propertyMap[name] = prop;
		SaveProperties();
	}

	public void AddInt(string name, int value)
	{
		if (string.IsNullOrEmpty(name)) throw new ArgumentException("Property name cannot be empty.");
		if (propertyMap.ContainsKey(name)) throw new ArgumentException($"Property '{name}' already exists.");
		var prop = new DynamicProperty { Name = name, Type = PropertyType.Int, IntValue = value };
		data.Properties.Add(prop);
		propertyMap[name] = prop;
		SaveProperties();
	}

	public void AddString(string name, string value)
	{
		if (string.IsNullOrEmpty(name)) throw new ArgumentException("Property name cannot be empty.");
		if (propertyMap.ContainsKey(name)) throw new ArgumentException($"Property '{name}' already exists.");
		var prop = new DynamicProperty { Name = name, Type = PropertyType.String, StringValue = value };
		data.Properties.Add(prop);
		propertyMap[name] = prop;
		SaveProperties();
	}

	public void AddBool(string name, bool value)
	{
		if (string.IsNullOrEmpty(name)) throw new ArgumentException("Property name cannot be empty.");
		if (propertyMap.ContainsKey(name)) throw new ArgumentException($"Property '{name}' already exists.");
		var prop = new DynamicProperty { Name = name, Type = PropertyType.Bool, BoolValue = value };
		data.Properties.Add(prop);
		propertyMap[name] = prop;
		SaveProperties();
	}

	// Programmatic Remove Method
	public bool RemoveProperty(string name)
	{
		if (propertyMap.TryGetValue(name, out var prop))
		{
			data.Properties.Remove(prop);
			propertyMap.Remove(name);
			SaveProperties();
			return true;
		}
		Debug.LogWarning($"Failed to remove property '{name}' from {gameObject.name}: not found.");
		return false;
	}

	// Helper to validate property existence and type
	private bool TryGetProperty(string name, PropertyType type, out DynamicProperty property)
	{
		if (propertyMap.TryGetValue(name, out property) && property.Type == type)
		{
			return true;
		}
		// Comment out for production; re-enable for debugging if needed
		// Debug.LogWarning($"Property '{name}' not found or type mismatch (expected {type}) in {gameObject.name}. Available properties: {string.Join(", ", propertyMap.Keys)}");
		property = null;
		return false;
	}

	// Float methods
	public bool HasFloat(string name) => propertyMap.ContainsKey(name) && propertyMap[name].Type == PropertyType.Float;
	public bool TryGetFloat(string name, out float value)
	{
		if (TryGetProperty(name, PropertyType.Float, out var prop))
		{
			value = prop.FloatValue;
			return true;
		}
		value = default;
		return false;
	}
	public float GetFloat(string name)
	{
		if (TryGetProperty(name, PropertyType.Float, out var prop))
		{
			return prop.FloatValue;
		}
		throw new KeyNotFoundException($"Float property '{name}' not found or type mismatch.");
	}

	// Int methods
	public bool HasInt(string name) => propertyMap.ContainsKey(name) && propertyMap[name].Type == PropertyType.Int;
	public bool TryGetInt(string name, out int value)
	{
		if (TryGetProperty(name, PropertyType.Int, out var prop))
		{
			value = prop.IntValue;
			return true;
		}
		value = default;
		return false;
	}
	public int GetInt(string name)
	{
		if (TryGetProperty(name, PropertyType.Int, out var prop))
		{
			return prop.IntValue;
		}
		throw new KeyNotFoundException($"Int property '{name}' not found or type mismatch.");
	}

	// String methods
	public bool HasString(string name) => propertyMap.ContainsKey(name) && propertyMap[name].Type == PropertyType.String;
	public bool TryGetString(string name, out string value)
	{
		if (TryGetProperty(name, PropertyType.String, out var prop))
		{
			value = prop.StringValue;
			return true;
		}
		value = default;
		return false;
	}
	public string GetString(string name)
	{
		if (TryGetProperty(name, PropertyType.String, out var prop))
		{
			return prop.StringValue;
		}
		throw new KeyNotFoundException($"String property '{name}' not found or type mismatch.");
	}

	// Bool methods
	public bool HasBool(string name) => propertyMap.ContainsKey(name) && propertyMap[name].Type == PropertyType.Bool;
	public bool TryGetBool(string name, out bool value)
	{
		if (TryGetProperty(name, PropertyType.Bool, out var prop))
		{
			value = prop.BoolValue;
			return true;
		}
		value = default;
		return false;
	}
	public bool GetBool(string name)
	{
		if (TryGetProperty(name, PropertyType.Bool, out var prop))
		{
			return prop.BoolValue;
		}
		throw new KeyNotFoundException($"Bool property '{name}' not found or type mismatch.");
	}
}