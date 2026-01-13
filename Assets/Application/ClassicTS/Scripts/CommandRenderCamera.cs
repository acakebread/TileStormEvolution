using MassiveHadronLtd;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ClassicTilestorm
{
	public sealed class CommandRenderCamera : MonoBehaviour, ICommandBufferProvider
	{
		private static readonly int MainLightPositionID = Shader.PropertyToID("_MainLightPosition");
		private static readonly int MainLightColorID = Shader.PropertyToID("_MainLightColor");

		private Camera cam;

		private RenderModelData[] models;

		private Vector3 lightDir = new Vector3(0.5f, 1f, -0.3f).normalized;
		private Color lightColor = new Color(0.75f, 0.75f, 0.75f);

		public Camera Camera => cam;

		public void Initialize(RenderTexture rt, Color background, float fov)
		{
			var camGO = new GameObject("CommandRenderCamera_Internal");
			camGO.transform.SetParent(transform, false);

			cam = camGO.AddComponent<Camera>();
			cam.enabled = false;
			cam.cullingMask = 0;
			cam.clearFlags = CameraClearFlags.SolidColor;
			cam.backgroundColor = background;
			cam.fieldOfView = fov;
			cam.nearClipPlane = 0.03f;
			cam.farClipPlane = 50f;
			cam.targetTexture = rt;

			camGO.AddComponent<UniversalAdditionalCameraData>();
			camGO.AddComponent<CommandCameraHook>().Owner = this;
		}

		public void SetModels(RenderModelData[] data)
		{
			models = data;
		}

		public void SetLight(Vector3 dir, Color color)
		{
			lightDir = dir.normalized;
			lightColor = color;
		}

		public void Render()
		{
			cam?.Render();
		}

		// ───────────────── ICommandBufferProvider ─────────────────

		public bool HasCommands(RenderPassEvent evt)
			=> evt == RenderPassEvent.BeforeRenderingOpaques;

		public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer cmd, Camera camera)
		{
			if (evt != RenderPassEvent.BeforeRenderingOpaques)
				return;

			Vector4 mainLightPos = new Vector4(lightDir.x, lightDir.y, lightDir.z, 1);
			cmd.SetGlobalVector(MainLightPositionID, mainLightPos);
			cmd.SetGlobalVector(MainLightColorID, lightColor.linear);

			if (models == null) return;

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

		// Hook that forwards URP to this provider
		private class CommandCameraHook : MonoBehaviour, ICommandBufferProvider
		{
			public CommandRenderCamera Owner;

			public bool HasCommands(RenderPassEvent evt)
				=> Owner != null && Owner.HasCommands(evt);

			public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer cmd, Camera cam)
				=> Owner?.ExecuteCommands(evt, cmd, cam);
		}
	}
}
