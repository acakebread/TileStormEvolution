using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public class DynamicSceneManager : MonoBehaviour
	{
		private static DynamicSceneManager _instance;
		public static DynamicSceneManager Instance => _instance ??= CreateInstance();

		private const string PreviewSceneName = "PreviewScene_Dynamic";

		private readonly List<DynamicSceneInstance> activePreviews = new();

		private static DynamicSceneManager CreateInstance()
		{
			var go = new GameObject("[PreviewSceneManager]");
			DontDestroyOnLoad(go);
			return go.AddComponent<DynamicSceneManager>();
		}

		private void Awake()
		{
			if (_instance != null && _instance != this)
			{
				Destroy(gameObject);
				return;
			}
			_instance = this;
		}

		public DynamicSceneInstance CreatePreviewInstance(string instanceName = "Preview")
		{
			// Create or get dynamic preview scene
			Scene previewScene = GetOrCreateDynamicPreviewScene();

			var root = new GameObject($"{instanceName} - PreviewRoot");
			DontDestroyOnLoad(root);

			SceneManager.MoveGameObjectToScene(root, previewScene);

			var instance = root.AddComponent<DynamicSceneInstance>();
			instance.Initialize(previewScene);

			activePreviews.Add(instance);
			return instance;
		}

		public void DestroyPreviewInstance(DynamicSceneInstance instance)
		{
			if (instance == null) return;
			activePreviews.Remove(instance);
			if (instance.gameObject) DestroyImmediate(instance.gameObject);
		}

		private Scene GetOrCreateDynamicPreviewScene()
		{
			Scene scene = SceneManager.GetSceneByName(PreviewSceneName);

			if (scene.IsValid())
			{
				if (!scene.isLoaded)
				{
					// Rare case: scene exists but unloaded - reload it
					SceneManager.LoadScene(PreviewSceneName, LoadSceneMode.Additive);
				}
				return scene;
			}

			// Create brand new dynamic scene (no file, no build settings needed)
			Debug.Log($"[Preview] Creating dynamic preview scene '{PreviewSceneName}'");
			return SceneManager.CreateScene(PreviewSceneName);
		}

		private void OnDestroy()
		{
			foreach (var preview in activePreviews.ToArray())
				DestroyPreviewInstance(preview);

			activePreviews.Clear();

			// Optional: clean up dynamic scene on manager destroy
			Scene previewScene = SceneManager.GetSceneByName(PreviewSceneName);
			if (previewScene.IsValid())
			{
				SceneManager.UnloadSceneAsync(previewScene);
			}
		}
	}
}