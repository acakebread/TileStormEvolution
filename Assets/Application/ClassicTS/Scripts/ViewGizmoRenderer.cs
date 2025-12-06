// File: Assets/Scripts/ClassicTilestorm/ViewGizmoRenderer.cs
using UnityEngine;
using System.Collections.Generic;

namespace ClassicTilestorm
{
	[ExecuteAlways]
	public class ViewGizmoRenderer : MonoBehaviour
	{
		public static ViewGizmoRenderer Instance { get; private set; }

		private Mesh frustumMesh;
		private Material frustumMaterial;
		private Matrix4x4 currentMatrix = Matrix4x4.identity;
		private bool showFrustum = false;

		private const float GameFOV = 20f;
		private const float NearPlane = 0.5f;
		private const float FarPlane = 30f;

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				DestroyImmediate(this);
				return;
			}
			Instance = this;

			if (Application.isPlaying)
				DontDestroyOnLoad(gameObject);

			CreateResources();
		}

		private void OnDestroy()
		{
			if (Instance == this) Instance = null;
			if (frustumMesh) DestroyImmediate(frustumMesh);
			if (frustumMaterial) DestroyImmediate(frustumMaterial);
		}

		private void CreateResources()
		{
			if (frustumMesh != null) return;

			frustumMesh = CreateFrustumMesh();
			frustumMaterial = CreateFrustumMaterial();
		}

		private Mesh CreateFrustumMesh()
		{
			var mesh = new Mesh { name = "ViewFrustum_Selected" };

			float aspect = 16f / 9f;
			float halfFov = GameFOV * 0.5f * Mathf.Deg2Rad;
			float t = Mathf.Tan(halfFov);

			float nh = NearPlane * t * 2f;
			float nw = nh * aspect;
			float fh = FarPlane * t * 2f;
			float fw = fh * aspect;

			Vector3[] near = {
				new(-nw/2, -nh/2, NearPlane),
				new( nw/2, -nh/2, NearPlane),
				new( nw/2,  nh/2, NearPlane),
				new(-nw/2,  nh/2, NearPlane)
			};

			Vector3[] far = {
				new(-fw/2, -fh/2, FarPlane),
				new( fw/2, -fh/2, FarPlane),
				new( fw/2,  fh/2, FarPlane),
				new(-fw/2,  fh/2, FarPlane)
			};

			var verts = new List<Vector3>();
			verts.AddRange(near);
			verts.AddRange(far);

			var tris = new List<int>();
			for (int i = 0; i < 4; i++)
			{
				int a = i, b = (i + 1) % 4, c = b + 4, d = a + 4;
				tris.AddRange(new[] { a, b, c, a, c, d });
				tris.AddRange(new[] { a, d, c, a, c, b });
			}

			mesh.SetVertices(verts);
			mesh.SetIndices(tris, MeshTopology.Triangles, 0);
			mesh.RecalculateBounds();
			return mesh;
		}

		private Material CreateFrustumMaterial()
		{
			var shader = Shader.Find("Unlit/Color") ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended");
			var mat = new Material(shader)
			{
				hideFlags = HideFlags.HideAndDontSave,
				color = new Color(0.15f, 0.65f, 1f, 0.3f)
			};
			mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
			mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
			mat.SetInt("_ZWrite", 0);
			mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
			return mat;
		}

		public void ShowForView(View view, int tileIndex)
		{
			if (view?.data == null || view.data.Length < 7 || view.Distance < 0.02f)
			{
				Hide();
				return;
			}

			var mapManager = Object.FindFirstObjectByType<MapManager>();
			if (!mapManager)
			{
				Hide();
				return;
			}

			Vector3 worldPos = mapManager.TileWorldPosition(tileIndex) + view.Position;
			Vector3 forward = (view.LookAt - view.Position).normalized;
			if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;

			Quaternion rot = view.Rotation;
			Vector3 up = Vector3.ProjectOnPlane(rot * Vector3.up, forward);
			if (up.sqrMagnitude < 0.01f) up = Vector3.up;
			else up = up.normalized;

			currentMatrix = Matrix4x4.TRS(worldPos, Quaternion.LookRotation(forward, up), Vector3.one);
			showFrustum = true;
		}

		public void Hide() => showFrustum = false;

		// THIS IS THE ONLY METHOD THAT WORKS WITH YOUR REFLECTION CAMERA
		private void OnRenderObject()
		{
			if (!showFrustum || frustumMesh == null || frustumMaterial == null) return;

			// This works in Scene View AND Game View, even with URP custom renderers
			frustumMaterial.SetPass(0);
			Graphics.DrawMeshNow(frustumMesh, currentMatrix);
		}
	}
}