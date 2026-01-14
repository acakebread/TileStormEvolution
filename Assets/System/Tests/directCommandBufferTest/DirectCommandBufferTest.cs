using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace MassiveHadronLtd
{
	public class DirectCommandBufferTest : MonoBehaviour
	{
		[Header("Required Material")]
		public Material testMaterial;

		[Header("Output UI")]
		public RawImage outputRawImage;

		[Header("Projection Camera (assign external Camera here)")]
		public Camera projectionCamera;

		//[Header("Virtual Command Camera")]
		//public TestCommandCamera customCam = new();

		private RenderTexture rt;
		private Mesh quadMesh;

		private void Awake()
		{
			if (testMaterial == null)
			{
				Debug.LogError("testMaterial is required!", this);
				return;
			}

			if (projectionCamera == null)
			{
				Debug.LogError("Assign a Projection Camera in Inspector!", this);
				return;
			}

			CreateQuadMesh();

			//customCam.RecalculateMatrices();

			var provider = projectionCamera.gameObject.AddComponent<DirectCommandProvider>();
			if (provider == null)
			{
				Debug.LogError("DirectCommandBufferTest: Failed to add DirectCommandProvider to projectionCamera", this);
				enabled = false;
				return;
			}

			provider.RegisterCommand(RenderPassEvent.BeforeRenderingOpaques, (cmd, cam) =>
			{
				cmd.ClearRenderTarget(true, true, new Color(cam.backgroundColor.r, cam.backgroundColor.g, cam.backgroundColor.b, 1f), 1f);
				DrawTestQuad(cmd);
			});
		}

		private void Start()
		{
			SetupRenderTexture();
		}

		private void Update()
		{
			//customCam.position = transform.position;
			//customCam.rotation = transform.rotation;
			//customCam.RecalculateMatrices();

			projectionCamera.transform.position = transform.position;
			projectionCamera.transform.rotation = transform.rotation;
		}

		private void LateUpdate()
		{
			if (projectionCamera)
			{
				projectionCamera.Render();
				UpdateUI();
			}
		}

		private void SetupRenderTexture()
		{
			if (projectionCamera == null) return;

			projectionCamera.enabled = false;

			rt = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.DefaultHDR)
			{
				name = "DirectTest_RT",
				antiAliasing = 1,
				filterMode = FilterMode.Bilinear
			};
			rt.Create();

			projectionCamera.targetTexture = rt;
			projectionCamera.aspect = rt.width / (float)rt.height;

			UpdateUI();
		}

		private void UpdateUI()
		{
			if (outputRawImage && rt && rt.IsCreated())
			{
				outputRawImage.texture = rt;
				outputRawImage.color = Color.white;
			}
		}

		private void CreateQuadMesh()
		{
			quadMesh = new Mesh
			{
				vertices = new[]
				{
					new Vector3(-1f, -1f, 0f),
					new Vector3(-1f,  1f, 0f),
					new Vector3( 1f,  1f, 0f),
					new Vector3( 1f, -1f, 0f)
				},
				uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right },
				triangles = new[] { 0, 1, 2, 0, 2, 3 }
			};
			quadMesh.RecalculateBounds();
		}

		private void DrawTestQuad(RasterCommandBuffer cmd)
		{
			if (!testMaterial) return;
			//Matrix4x4 objectMatrix = Matrix4x4.TRS(customCam.position, customCam.rotation, Vector3.one).inverse;
			//cmd.DrawMesh(quadMesh, objectMatrix, testMaterial, 0, 0);
			cmd.DrawMesh(quadMesh, Matrix4x4.identity, testMaterial, 0, 0);
		}

		private void OnDestroy()
		{
			if (rt != null) rt.Release();
			if (quadMesh != null) Destroy(quadMesh);
		}
	}

	internal class DirectCommandProvider : MonoBehaviour, IDirectCommandProvider
	{
		private readonly Dictionary<RenderPassEvent, Action<RasterCommandBuffer, Camera>> commands = new();

		public void RegisterCommand(RenderPassEvent evt, Action<RasterCommandBuffer, Camera> command) => commands[evt] = command;

		public bool HasCommands(RenderPassEvent evt) => commands.ContainsKey(evt) && commands[evt] != null;

		public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer commandBuffer, Camera camera)
		{
			if (commands.ContainsKey(evt) && commands[evt] != null)
			{
				try { commands[evt].Invoke(commandBuffer, camera); }
				catch (Exception e) { Debug.LogError($"DirectCommandProvider: Error executing command for event {evt}, camera {camera.name}: {e.Message}"); }
			}
		}

		void OnDestroy() => commands.Clear();
	}
}