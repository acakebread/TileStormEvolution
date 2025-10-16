using UnityEngine;

namespace MassiveHadronLtd
{
	[RequireComponent(typeof(Camera))]
	public class InvertCamera : MonoBehaviour
	{
		private Camera _camera;
		private RenderTexture _renderTexture;
		private Material _flipMaterial;

		private void Start()
		{
			_camera = GetComponent<Camera>();
			_renderTexture = new RenderTexture(_camera.pixelWidth, _camera.pixelHeight, 24, RenderTextureFormat.ARGB32);
			_camera.targetTexture = _renderTexture;

			_flipMaterial = new Material(Shader.Find("Unlit/Texture"));
			_flipMaterial.SetVector("_MainTex_ST", new Vector4(1, -1, 0, 1)); // Flip vertically
		}

		private void OnPostRender()
		{
			_flipMaterial.SetTexture("_MainTex", _renderTexture);
			Graphics.Blit(_renderTexture, null as RenderTexture, _flipMaterial);
		}

		private void OnDestroy()
		{
			if (_renderTexture != null)
			{
				_renderTexture.Release();
			}
			if (_flipMaterial != null)
			{
				Object.Destroy(_flipMaterial);
			}
		}
	}
}