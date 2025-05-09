using UnityEngine;

namespace ClassicTilestorm
{
	public class TextureSetAnimator : MonoBehaviour
	{
		private TextureFrame[] frames = null;
		private MeshRenderer target = null;
		private int frame = 0;
		private float timer = 0f;

		private void ApplyTexture(int index)
		{
			if (target == null || frames[index].texture == null) return;
			if (target.material == null) target.material = new Material(Shader.Find("Standard"));
			target.material.mainTexture = frames[index].texture;
		}

		public void Initialize(TextureFrame[] frames)
		{
			target = GetComponentInChildren<MeshRenderer>();
			if (null == frames || null == target)
			{
				Destroy(this);
				return;
			}

			// Apply first frame
			this.frames = frames;
			frame = 0;
			timer = 0f;
			ApplyTexture(frame);
		}

		void Update()
		{
			if (frames == null || frames.Length <= 1)
				return;

			timer += Time.deltaTime;
			if (timer >= frames[frame].duration)
			{
				timer -= frames[frame].duration;
				frame = (frame + 1) % frames.Length;
				ApplyTexture(frame);
			}
		}

		void OnDestroy()
		{
			if (target != null && target.material != null) Destroy(target.material);
		}
	}
}