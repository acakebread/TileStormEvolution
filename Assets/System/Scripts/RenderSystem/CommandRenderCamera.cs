using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MassiveHadronLtd
{
	/// <summary>
	/// Camera that renders arrays of CommandRenderModelData using URP command buffers.
	/// </summary>
	public sealed class CommandRenderCamera : MonoBehaviour, ICommandBufferProvider
	{
		private static readonly int MainLightPositionID = Shader.PropertyToID("_MainLightPosition");
		private static readonly int MainLightColorID = Shader.PropertyToID("_MainLightColor");

		private Camera cam;
		private CommandRenderModelData[] models;

		private Vector3 lightDir = new Vector3(0.5f, 1f, -0.3f).normalized;
		private Color lightColor = new Color(0.75f, 0.75f, 0.75f);

		public Camera Camera => cam;

		/// <summary>Initialize the camera with a target RenderTexture, background color, and FOV.</summary>
		public void Initialize(RenderTexture rt, Color background, float fov)
		{
			var camGO = new GameObject("CommandRenderCamera_Internal");
			camGO.transform.SetParent(transform, false);

			cam = camGO.AddComponent<Camera>();
			cam.enabled = false;
			cam.cullingMask = 0; // Render everything manually
			cam.clearFlags = CameraClearFlags.SolidColor;
			cam.backgroundColor = background;
			cam.fieldOfView = fov;
			cam.nearClipPlane = 0.03f;
			cam.farClipPlane = 50f;
			cam.targetTexture = rt;

			camGO.AddComponent<UniversalAdditionalCameraData>();
			camGO.AddComponent<CommandCameraHook>().Owner = this;
		}

		public void SetModels(CommandRenderModelData[] data) => models = data;

		public void SetLight(Vector3 dir, Color color)
		{
			lightDir = dir.normalized;
			lightColor = color;
		}

		public void Render() => cam?.Render();

		// ─────────────── ICommandBufferProvider ───────────────

		public bool HasCommands(RenderPassEvent evt)
			=> evt == RenderPassEvent.BeforeRenderingOpaques;

		public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer cmd, Camera camera)
		{
			if (evt != RenderPassEvent.BeforeRenderingOpaques || models == null) return;

			// Set up fake directional light
			Vector4 mainLightPos = new Vector4(lightDir.x, lightDir.y, lightDir.z, 1f);
			cmd.SetGlobalVector(MainLightPositionID, mainLightPos);
			cmd.SetGlobalVector(MainLightColorID, lightColor.linear);

			// Draw all models
			foreach (var model in models)
			{
				if (model == null) continue;

				foreach (var inst in model.meshInstances)
				{
					if (inst.mesh == null) continue;

					for (int s = 0; s < inst.subMeshCount; s++)
					{
						var mat = s < inst.materials.Length ? inst.materials[s] : inst.materials[0];
						if (mat == null) continue;

						cmd.DrawMesh(inst.mesh, inst.localToWorld, mat, s, 0);
					}
				}
			}
		}

		// ─────────────── Hook for URP to forward to this provider ───────────────
		private class CommandCameraHook : MonoBehaviour, ICommandBufferProvider
		{
			public CommandRenderCamera Owner;

			public bool HasCommands(RenderPassEvent evt) => Owner != null && Owner.HasCommands(evt);

			public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer cmd, Camera cam)
				=> Owner?.ExecuteCommands(evt, cmd, cam);
		}
	}
}
