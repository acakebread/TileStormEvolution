using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MassiveHadronLtd
{
	// ========================================
	// CommandRenderCamera.cs  – fixed version
	// ========================================

	public class CommandRenderCamera
	{
		private Camera Camera { get; set; }
		private GameObject cameraGameObject;
		private CommandCameraHook hook;

		// Quick direct accessors (unchanged)
		public Transform transform => Camera.transform;
		public Vector3 position { get => Camera.transform.position; set => Camera.transform.position = value; }
		public Quaternion rotation { get => Camera.transform.rotation; set => Camera.transform.rotation = value; }
		public float fieldOfView { get => Camera.fieldOfView; set => Camera.fieldOfView = value; }
		public Color backgroundColor { get => Camera.backgroundColor; set => Camera.backgroundColor = value; }
		public float aspect { get => Camera.aspect; set => Camera.aspect = value; }
		public RenderTexture targetTexture { get => Camera.targetTexture; set => Camera.targetTexture = value; }
		//private CameraRenderSettingsOverride cameraRenderSettingsOverride => Camera.gameObject.GetComponent<CameraRenderSettingsOverride>();
		public UnityRenderSettings overrideSettings
		{
			set
			{
				var overrideComp = Camera.gameObject.GetOrAddComponent<CameraRenderSettingsOverride>();
				if (null == overrideComp) return;
				overrideComp.OverrideSettings = value;
			}
		}

		public CommandRenderCamera(
			string name,
			RenderTexture targetRT,
			Color background,
			float fov = 60f,
			Transform desiredParent = null)//,//UnityRenderSettings overrideSettings = default)
		{
			cameraGameObject = new GameObject(name ?? "CommandRenderCamera");

			Camera = cameraGameObject.AddComponent<Camera>();
			Camera.enabled = false;
			Camera.cullingMask = 0;
			Camera.clearFlags = CameraClearFlags.SolidColor;
			Camera.backgroundColor = background;
			Camera.fieldOfView = fov;
			Camera.nearClipPlane = 0.03f;
			Camera.farClipPlane = 50f;
			Camera.targetTexture = targetRT;

			if (targetRT != null)
				Camera.aspect = (float)targetRT.width / targetRT.height;

			cameraGameObject.AddComponent<UniversalAdditionalCameraData>();

			// Just create the hook – do NOT assign provider here!
			hook = cameraGameObject.AddComponent<CommandCameraHook>();

			if (desiredParent != null)
				cameraGameObject.transform.SetParent(desiredParent, false);
		}

		public void Render() => Camera?.Render();

		// This method is now used by the scene
		public void AssignCommandProvider(ICommandBufferProvider provider)
		{
			if (hook != null)
				hook.Provider = provider;
		}

		public void Destroy()
		{
			if (cameraGameObject != null)
			{
				Object.Destroy(cameraGameObject);
				cameraGameObject = null;
				Camera = null;
				hook = null;
			}
		}

		public void SetUnityCameraParent(Transform newParent)
		{
			if (cameraGameObject != null)
				cameraGameObject.transform.SetParent(newParent, false);
		}
	}

	// Hook stays the same
	internal class CommandCameraHook : MonoBehaviour, ICommandBufferProvider
	{
		public ICommandBufferProvider Provider { get; set; }

		public bool HasCommands(RenderPassEvent evt) => Provider?.HasCommands(evt) ?? false;

		public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer cmd, Camera camera)
			=> Provider?.ExecuteCommands(evt, cmd, camera);
	}
}