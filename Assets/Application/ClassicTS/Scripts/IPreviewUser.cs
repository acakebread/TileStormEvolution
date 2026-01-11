// IPreviewUser.cs
namespace ClassicTilestorm.Editor
{
	public interface IPreviewUser
	{
		void OnPreviewDrag(UnityEngine.Vector2 delta);
		void OnPreviewScroll(float scrollDelta);
	}
}