using UnityEngine;

namespace ClassicTilestorm
{
	public class TextureSetAnimator : MonoBehaviour
	{
		private TextureFrame[] frames;
		private MeshRenderer targetRenderer;
		private int currentFrame = 0;
		private float timer = 0f;

		public delegate void TextureChangedHandler(Texture2D newTexture);
		public event TextureChangedHandler OnTextureChanged;

		public void Initialize(TextureFrame[] runtimeFrames)
		{
			targetRenderer = GetComponentInChildren<MeshRenderer>(true);
			if (targetRenderer == null || runtimeFrames == null || runtimeFrames.Length == 0)
			{
				Destroy(this);
				return;
			}

			frames = runtimeFrames;
			currentFrame = 0;
			timer = 0f;
			ApplyFrame(0); // Apply first frame immediately
		}

		public void ApplyFrame(int index)
		{
			if (targetRenderer.material == null)
				targetRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));

			targetRenderer.material.mainTexture = frames[index].texture;
			OnTextureChanged?.Invoke(frames[index].texture);
		}

		void Update()
		{
			if (frames == null || frames.Length <= 1) return;

			timer += Time.deltaTime;
			if (timer >= frames[currentFrame].Duration)  // ← clean, safe, read-only
			{
				timer -= frames[currentFrame].Duration;
				currentFrame = (currentFrame + 1) % frames.Length;
				ApplyFrame(currentFrame);
			}
		}

		void OnDestroy()
		{
			if (targetRenderer != null && targetRenderer.material != null)
				Destroy(targetRenderer.material);
		}
	}
}