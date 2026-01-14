using UnityEngine;

namespace MassiveHadronLtd
{
	public class TestCommandCamera
	{
		public Matrix4x4 viewMatrix = Matrix4x4.identity;
		public Matrix4x4 projectionMatrix = Matrix4x4.identity;
		public RenderTexture targetTexture;
		public Vector3 position = Vector3.zero;
		public Quaternion rotation = Quaternion.identity;
		public float aspect = 16f / 9f;
		public float nearClipPlane = 0.1f;
		public float farClipPlane = 100f;
		public bool orthographic = true;
		public float orthographicSize = 5f;
		public float fieldOfView = 60f;

		public void RecalculateMatrices()
		{
			if (orthographic)
			{
				projectionMatrix = Matrix4x4.Ortho(
					-aspect * orthographicSize,
					 aspect * orthographicSize,
					-orthographicSize,
					 orthographicSize,
					nearClipPlane, farClipPlane);
			}
			else
			{
				projectionMatrix = Matrix4x4.Perspective(
					fieldOfView, aspect, nearClipPlane, farClipPlane);
			}

			viewMatrix = Matrix4x4.TRS(position, rotation, Vector3.one).inverse;
		}
	}
}