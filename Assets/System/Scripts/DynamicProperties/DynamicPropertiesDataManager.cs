using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DynamicPropertiesDataManager
{
	public DynamicPropertiesData data;

	public DynamicPropertiesDataManager(string json)
	{
		try
		{
			data = JsonUtility.FromJson<DynamicPropertiesData>(json);
			if (data == null || data.Properties == null)
			{
				data = new DynamicPropertiesData();
			}
		}
		catch
		{
			data = new DynamicPropertiesData();
		}
	}

	public IReadOnlyList<DynamicProperty> Properties => data.Properties.AsReadOnly();

	public IEnumerable<DynamicProperty> GetPropertiesByType(PropertyType type)
	{
		return data.Properties.Where(p => p.Type == type);
	}

	public void AddFloat(string name, float value)
	{
		AddProperty(name, PropertyType.Float, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public void AddInt(string name, int value)
	{
		AddProperty(name, PropertyType.Int, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public void AddString(string name, string value)
	{
		AddProperty(name, PropertyType.String, value);
	}

	public void AddBool(string name, bool value)
	{
		AddProperty(name, PropertyType.Bool, value.ToString().ToLowerInvariant());
	}

	private void AddProperty(string name, PropertyType type, string value)
	{
		if (string.IsNullOrEmpty(name))
		{
			return;
		}
		if (data.Properties.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
		{
			return;
		}
		data.Properties.Add(new DynamicProperty { Name = name, Type = type, Value = value });
	}

	public bool RemoveProperty(string name)
	{
		var property = data.Properties.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
		if (property != null)
		{
			return data.Properties.Remove(property);
		}
		return false;
	}

	public bool HasFloat(string name)
	{
		var prop = data.Properties.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
		return prop != null && prop.Type == PropertyType.Float;
	}

	public bool TryGetFloat(string name, out float value)
	{
		value = default;
		var prop = data.Properties.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
		if (prop != null && prop.Type == PropertyType.Float && float.TryParse(prop.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value))
		{
			return true;
		}
		return false;
	}

	public float GetFloat(string name)
	{
		if (TryGetFloat(name, out float value))
		{
			return value;
		}
		throw new KeyNotFoundException($"Float property '{name}' not found.");
	}

	public bool HasInt(string name)
	{
		var prop = data.Properties.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
		return prop != null && prop.Type == PropertyType.Int;
	}

	public bool TryGetInt(string name, out int value)
	{
		value = default;
		var prop = data.Properties.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
		if (prop != null && prop.Type == PropertyType.Int && int.TryParse(prop.Value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out value))
		{
			return true;
		}
		return false;
	}

	public int GetInt(string name)
	{
		if (TryGetInt(name, out int value))
		{
			return value;
		}
		throw new KeyNotFoundException($"Int property '{name}' not found.");
	}

	public bool HasString(string name)
	{
		var prop = data.Properties.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
		return prop != null && prop.Type == PropertyType.String;
	}

	public bool TryGetString(string name, out string value)
	{
		value = default;
		var prop = data.Properties.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
		if (prop != null && prop.Type == PropertyType.String)
		{
			value = prop.Value;
			return true;
		}
		return false;
	}

	public string GetString(string name)
	{
		if (TryGetString(name, out string value))
		{
			return value;
		}
		throw new KeyNotFoundException($"String property '{name}' not found.");
	}

	public bool HasBool(string name)
	{
		var prop = data.Properties.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
		return prop != null && prop.Type == PropertyType.Bool;
	}

	public bool TryGetBool(string name, out bool value)
	{
		value = default;
		var prop = data.Properties.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
		if (prop != null && prop.Type == PropertyType.Bool && bool.TryParse(prop.Value, out value))
		{
			return true;
		}
		return false;
	}

	public bool GetBool(string name)
	{
		if (TryGetBool(name, out bool value))
		{
			return value;
		}
		throw new KeyNotFoundException($"Bool property '{name}' not found.");
	}

	public string SaveToJson()
	{
		string json = JsonUtility.ToJson(data);
		return json;
	}
}

[Serializable]
public class DynamicPropertiesData
{
	public List<DynamicProperty> Properties = new List<DynamicProperty>();
}

[Serializable]
public class DynamicProperty
{
	public string Name;
	public PropertyType Type;
	public string Value;

	public bool TryGetFloat(out float value)
	{
		return float.TryParse(Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
	}

	public float FloatValue
	{
		get
		{
			if (TryGetFloat(out float value))
			{
				return value;
			}
			throw new InvalidOperationException($"Property '{Name}' is not a float or cannot be parsed.");
		}
	}

	public bool TryGetInt(out int value)
	{
		return int.TryParse(Value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out value);
	}

	public int IntValue
	{
		get
		{
			if (TryGetInt(out int value))
			{
				return value;
			}
			throw new InvalidOperationException($"Property '{Name}' is not an int or cannot be parsed.");
		}
	}

	public bool TryGetString(out string value)
	{
		value = Value;
		return Type == PropertyType.String;
	}

	public string StringValue
	{
		get
		{
			if (TryGetString(out string value))
			{
				return value;
			}
			throw new InvalidOperationException($"Property '{Name}' is not a string.");
		}
	}

	public bool TryGetBool(out bool value)
	{
		return bool.TryParse(Value, out value);
	}

	public bool BoolValue
	{
		get
		{
			if (TryGetBool(out bool value))
			{
				return value;
			}
			throw new InvalidOperationException($"Property '{Name}' is not a bool or cannot be parsed.");
		}
	}
}

public enum PropertyType
{
	Float,
	Int,
	String,
	Bool
}