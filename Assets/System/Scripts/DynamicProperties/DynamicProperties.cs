using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using System; // For Serializable

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MassiveHadronLtd
{
	// Updated PropertyType enum to match test code expectations
	public enum PropertyType
	{
		FLOAT,
		INT,
		STRING,
		BOOL
	}

	[DisallowMultipleComponent]
	public class DynamicProperties : MonoBehaviour
	{
		private Text textComponent;
		private DynamicPropertiesDataManager dataManager;

		public DynamicPropertiesData Data => dataManager?.data;

		private void Awake()
		{
			InitializeTextComponent();
			LoadProperties();
		}

		private void OnValidate()
		{
			if (!Application.isPlaying && textComponent == null)
			{
				textComponent = gameObject.GetComponent<Text>();
				if (textComponent != null && string.IsNullOrEmpty(textComponent.text))
				{
#if UNITY_EDITOR
					EditorApplication.delayCall += () =>
					{
						if (textComponent != null && string.IsNullOrEmpty(textComponent.text))
						{
							textComponent.text = "{\"Properties\":[]}";
						}
					};
#endif
				}
			}
		}

		public void InitializeTextComponent()
		{
			bool needsDirty = false;

			if (textComponent == null)
			{
				textComponent = gameObject.GetComponent<Text>();
				if (textComponent == null)
				{
#if UNITY_EDITOR
					Undo.RegisterCompleteObjectUndo(gameObject, "Add Text Component");
#endif
					textComponent = gameObject.AddComponent<Text>();
					if (textComponent == null)
					{
						return;
					}
					needsDirty = true;
				}
			}

			if (textComponent.enabled)
			{
				textComponent.enabled = false;
				needsDirty = true;
			}
			if (string.IsNullOrEmpty(textComponent.text))
			{
				textComponent.text = "{\"Properties\":[]}";
				needsDirty = true;
			}
			if (textComponent.hideFlags != (HideFlags.HideInInspector | HideFlags.NotEditable))
			{
				textComponent.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
				needsDirty = true;
			}

			Canvas canvas = gameObject.GetComponent<Canvas>();
			if (canvas != null)
			{
				if (canvas.enabled)
				{
					canvas.enabled = false;
					needsDirty = true;
				}
				if (canvas.hideFlags != (HideFlags.HideInInspector | HideFlags.NotEditable))
				{
					canvas.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
					needsDirty = true;
				}
				if (canvas.GetComponent<CanvasScaler>() == null && canvas.GetComponent<GraphicRaycaster>() == null)
				{
#if UNITY_EDITOR
					Undo.RegisterCompleteObjectUndo(canvas, "Remove Canvas Component");
#endif
					DestroyImmediate(canvas);
					needsDirty = true;
				}
			}

			CanvasRenderer canvasRenderer = gameObject.GetComponent<CanvasRenderer>();
			if (canvasRenderer != null)
			{
				if (canvasRenderer.hideFlags != (HideFlags.HideInInspector | HideFlags.NotEditable))
				{
					canvasRenderer.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
					needsDirty = true;
				}
			}

			if (needsDirty)
			{
#if UNITY_EDITOR
				EditorUtility.SetDirty(this);
				if (textComponent != null)
				{
					EditorUtility.SetDirty(textComponent);
				}
#endif
			}
		}

		public void LoadProperties()
		{
			if (textComponent == null)
			{
				InitializeTextComponent();
				if (textComponent == null)
				{
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
				InitializeTextComponent();
				if (textComponent == null)
				{
					return;
				}
			}
			if (dataManager == null)
			{
				LoadProperties();
				if (dataManager == null)
				{
					return;
				}
			}
			string newJson = dataManager.SaveToJson();
			if (textComponent.text != newJson)
			{
				textComponent.text = newJson;
#if UNITY_EDITOR
				EditorUtility.SetDirty(textComponent);
				EditorUtility.SetDirty(this);
#endif
			}
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
				newData = new DynamicPropertiesData();
			}
			dataManager = new DynamicPropertiesDataManager(JsonUtility.ToJson(newData));
			SaveProperties();
		}

		public IReadOnlyList<DynamicProperty> Properties => dataManager?.Properties ?? new List<DynamicProperty>().AsReadOnly();
		public IEnumerable<DynamicProperty> GetPropertiesByType(string type) => dataManager?.GetPropertiesByType(type) ?? Enumerable.Empty<DynamicProperty>();

		public void AddFloat(string name, float value)
		{
			dataManager?.AddFloat(name, value);
		}
		public void AddInt(string name, int value)
		{
			dataManager?.AddInt(name, value);
		}
		public void AddString(string name, string value)
		{
			dataManager?.AddString(name, value);
		}
		public void AddBool(string name, bool value)
		{
			dataManager?.AddBool(name, value);
		}
		public bool RemoveProperty(string name)
		{
			return dataManager?.RemoveProperty(name) ?? false;
		}

		public bool HasFloat(string name) => dataManager?.HasFloat(name) ?? false;
		public bool TryGetFloat(string name, out float value) => dataManager?.TryGetFloat(name, out value) ?? ((value = default) == default);
		public float GetFloat(string name) => dataManager?.GetFloat(name) ?? throw new KeyNotFoundException($"Float property '{name}' not found.");

		public bool HasInt(string name) => dataManager?.HasInt(name) ?? false;
		public bool TryGetInt(string name, out int value) => dataManager?.TryGetInt(name, out value) ?? ((value = default) == default);
		public int GetInt(string name) => dataManager?.GetInt(name) ?? throw new KeyNotFoundException($"Int property '{name}' not found.");

		public bool HasString(string name) => dataManager?.HasString(name) ?? false;
		public bool TryGetString(string name, out string value) => dataManager?.TryGetString(name, out value) ?? ((value = default) == default);
		public string GetString(string name) => dataManager?.GetString(name) ?? throw new KeyNotFoundException($"String property '{name}' not found.");

		public bool HasBool(string name) => dataManager?.HasBool(name) ?? false;
		public bool TryGetBool(string name, out bool value) => dataManager?.TryGetBool(name, out value) ?? ((value = default) == default);
		public bool GetBool(string name) => dataManager?.GetBool(name) ?? throw new KeyNotFoundException($"Bool property '{name}' not found.");
	}
}