using UnityEngine;

namespace ClassicTilestorm
{
	public abstract class UIPanel : MonoBehaviour
	{
		protected virtual void OnDisable()
		{
			// Called automatically when SetActive(false) happens
			// (Inspector button, code, or any other reason)

			var controller = UIController.Instance;
			if (controller != null && controller.IsThisPanelCurrent(this))
			{
				controller.NotifyPanelDeactivated(this);
			}
		}

		protected virtual void OnDestroy()
		{
			var controller = UIController.Instance;
			if (controller != null)
			{
				controller.NotifyPanelDestroyed(this);
			}
		}

		// Optional: you can still override these in derived classes if needed
		public virtual void OnPanelOpened() { }
		public virtual void OnPanelClosed() { }
	}
}