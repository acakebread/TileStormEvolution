using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	[RequireComponent(typeof(Camera))]
	public class DirectCommandBufferTest : MonoBehaviour, IDirectCommandProvider
	{
		public Material testMaterial;

		Mesh testMesh;

		readonly Dictionary<RenderPassEvent, Action<RasterCommandBuffer, Camera>> commands = new();

		void Awake()
		{
			if (!testMaterial)
			{
				Debug.LogError("Assign testMaterial!");
				enabled = false;
				return;
			}

			BuildMesh();

			RegisterCommand(RenderPassEvent.AfterRenderingOpaques, DrawTest);
		}

		void BuildMesh()
		{
			testMesh = new Mesh();
			testMesh.vertices = new[]
			{
				new Vector3(-1, -1, 0),
				new Vector3(-1,  1, 0),
				new Vector3( 1,  1, 0),
				new Vector3( 1, -1, 0),
			};

			testMesh.uv = new[]
			{
				new Vector2(0,0),
				new Vector2(0,1),
				new Vector2(1,1),
				new Vector2(1,0),
			};

			testMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
		}

		void DrawTest(RasterCommandBuffer cmd, Camera cam)
		{
			testMaterial.SetPass(0);
			cmd.DrawMesh(testMesh, Matrix4x4.identity, testMaterial, 0, 0);
		}

		public void RegisterCommand(RenderPassEvent evt, Action<RasterCommandBuffer, Camera> command)
		{
			commands[evt] = command;
		}

		public bool HasCommands(RenderPassEvent evt)
		{
			return commands.TryGetValue(evt, out var c) && c != null;
		}

		public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer cmd, Camera cam)
		{
			if (commands.TryGetValue(evt, out var c))
				c(cmd, cam);
		}

		void OnDestroy()
		{
			if (testMesh) Destroy(testMesh);
			commands.Clear();
		}
	}
}
