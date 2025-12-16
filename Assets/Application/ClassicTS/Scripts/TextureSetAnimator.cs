using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class TextureSetAnimator : MonoBehaviour
	{
		private TextureFrame[] _frames;
		private MeshRenderer _targetRenderer;
		private Material _baseMaterial;  // Store reference to original material
		private int _currentFrame = 0;
		private float _timer = 0f;

		public delegate void TextureChangedHandler(Texture2D newTexture);
		public event TextureChangedHandler OnTextureChanged;

		[HideInInspector] public bool IsEmissive { get; private set; }

		public void Initialize(TextureSequence sequence, Material baseMaterial = null)
		{
			_targetRenderer = GetComponentInChildren<MeshRenderer>(true);
			if (_targetRenderer == null || sequence == null || sequence.ResolvedFrames.Length == 0)
			{
				Destroy(this);
				return;
			}

			_frames = sequence.ResolvedFrames;
			_baseMaterial = baseMaterial;  // Store for emissive case
			_currentFrame = 0;
			_timer = 0f;

			ApplyFrame(0);
		}

		public void ApplyFrame(int index)
		{
			if (_targetRenderer.material == null)
				_targetRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));

			if (_frames == null || index < 0 || index >= _frames.Length) return;

			var tex = _frames[index].texture;

			if (IsEmissive && _baseMaterial != null)
			{
				// Emissive: preserve original albedo, use animated texture as emission map
				_targetRenderer.material.mainTexture = _baseMaterial.mainTexture;
				_targetRenderer.material.mainTextureOffset = _baseMaterial.mainTextureOffset;
				_targetRenderer.material.mainTextureScale = _baseMaterial.mainTextureScale;
				_targetRenderer.material.SetTexture("_EmissionMap", tex);
				_targetRenderer.material.EnableKeyword("_EMISSION");
			}
			else
			{
				// Standard: direct replace main texture
				_targetRenderer.material.mainTexture = tex;
			}

			OnTextureChanged?.Invoke(tex);
		}

		void Update()
		{
			if (_frames.Length <= 1) return;

			_timer += Time.deltaTime;
			if (_timer >= _frames[_currentFrame].duration)
			{
				_timer -= _frames[_currentFrame].duration;
				_currentFrame = (_currentFrame + 1) % _frames.Length;
				ApplyFrame(_currentFrame);
			}
		}

		void OnDestroy()
		{
			if (_targetRenderer != null && _targetRenderer.material != null && _targetRenderer.material != _baseMaterial)
				Destroy(_targetRenderer.material);  // Only destroy if we instanced it
		}

		public static TextureSetAnimator SetupAnimation(
			GameObject gameObject,
			TextureSequence sequence,
			Material baseMaterial)
		{
			if (gameObject == null || baseMaterial == null) return null;

			var renderer = gameObject.GetComponentInChildren<MeshRenderer>(true);
			if (renderer == null) return null;

			bool hasFrames = sequence != null && sequence.ResolvedFrames.Length > 0;
			bool isEmissive = MaterialUtils.isEmissive(baseMaterial);

			if (!hasFrames)
			{
				renderer.material = baseMaterial;
				return null;
			}

			// Instance material if we're going to modify it (always for safety when texture exists)
			var instanceMat = new Material(baseMaterial);
			renderer.material = instanceMat;

			var animator = gameObject.AddComponent<TextureSetAnimator>();
			animator.IsEmissive = isEmissive;

			// Pass baseMaterial so ApplyFrame knows the original albedo
			animator.Initialize(sequence, baseMaterial);

			return animator;
		}
	}
}