// Copyright 2019 massivehadron.com ltd. created 17/05/2019 by Andrew Cakebread

using UnityEngine;

namespace MassiveHadron
{
	public class TouchInterpreter : MonoBehaviour
	{
		protected virtual bool TouchOverUI(Vector2 position) { return false; }
		protected virtual Transform TouchToTarget(Vector2 position) { return null; }

		protected virtual bool MoveableTarget(Transform target) { return false; }

		protected virtual void OnTapped(Transform target, Vector2 position) { }
		protected virtual void OnSelect(Transform target) { }
		protected virtual void OnPicked(Transform target) { }
		protected virtual void OnModified(Transform target) { }

		protected virtual void OnDrag(Vector2 position, Vector2 delta) { }
		protected virtual void OnLook(Vector2 position, Vector2 delta) { }
		protected virtual void OnPinch(float delta) { }
		protected virtual void OnScroll(float delta) { }
		protected virtual void OnRotate(float delta) { }

		private float mark;//timestamp
		private Touch[] stored;
		private Transform target;
		private Transform picked;
		private bool modified;

		//constants
		private const float time_threshold = 0.2f;
		private const float size_threshold = 0.01f;

		private int last_touch_ct = 0;

		void Update()
		{
			if (0 != Input.mouseScrollDelta.y)
				OnScroll(Input.mouseScrollDelta.y * 1);//delta magnitude

			Touch[] touches = (true == Application.isMobilePlatform) ? Input.touches : MultiTouchEmulator.touches;

			if (0 < touches.Length && null == stored)
			{
				Vector2 test = touches[0].position;
				for (int n = 1; n < touches.Length; ++n)
					test += touches[n].position;

				if (true == TouchOverUI(test / touches.Length))
					return;
			}

			//quick fix for jumping zoom on mobile
			if (touches.Length < last_touch_ct)
			{
				if (0 == touches.Length) last_touch_ct = 0;
				return;
			}
			last_touch_ct = touches.Length;

			float scalar = Application.isMobilePlatform ? -0.1f : 1;

			float size = Mathf.Min(Screen.width, Screen.height);// * (Application.isMobilePlatform ? 10f : 1f);

			switch (touches.Length)
			{
				case 0://idle
					stored = null;
					modified = false;
					break;

				case 1:
					switch (touches[0].phase)
					{
						case TouchPhase.Began:
							modified = false;
							stored = touches;
							picked = TouchToTarget(touches[0].position);
							if (target != picked)
							{
								target = picked;
								OnSelect(null);
								mark = Time.time;
							}
							else
								mark = 0;
							OnTapped(picked, touches[0].position);
							break;

						case TouchPhase.Stationary:
							UpdateState(false);
							break;

						case TouchPhase.Moved:
							UpdateState(null != stored && (touches[0].position - stored[0].position).magnitude > size * size_threshold);
							if (mark > 0)
								break;

							if (true == MoveableTarget(target))
								OnDrag(touches[0].position, touches[0].deltaPosition * scalar);
							else
								OnLook(touches[0].position, touches[0].deltaPosition * scalar);
							break;

						case TouchPhase.Ended:
							OnSelect(target);
							if (false == modified)
								OnPicked(picked);
							if (true == MoveableTarget(target) && true == modified)
								OnModified(target);
							break;
					}
					break;

				case 2:

					bool skip = false;

					if (TouchPhase.Began == touches[0].phase || TouchPhase.Began == touches[1].phase)
					{
						skip = true;

						stored = touches;
						if (TouchPhase.Began == touches[0].phase && TouchPhase.Began == touches[1].phase)
						{
							bool found = false;
							for (int i = 0; i < 2 && false == found; ++i)
								found |= target == TouchToTarget(touches[i].position);

							if (true == found)
								mark = 0;
							else
							{
								mark = Time.time;
								target = null;
							}
						}

						if (false == modified && false == MoveableTarget(target))
						{
							target = null;

							for (int i = 0; i < 2; ++i)
							{
								if (true == MoveableTarget(picked = TouchToTarget(touches[i].position)))
								{
									mark = Time.time;
									target = picked;
								}
							}
						}

						picked = target ?? picked;
						OnTapped(picked, (touches[0].position + touches[1].position) * 0.5f);
					}

					if (TouchPhase.Stationary == touches[0].phase && TouchPhase.Stationary == touches[1].phase)
					{
						UpdateState(false);
						break;
					}

					if (false == skip && (TouchPhase.Moved == touches[0].phase || TouchPhase.Moved == touches[1].phase))
					{
						UpdateState(null != stored && 2 == stored.Length && ((touches[0].position - stored[0].position).magnitude > size * size_threshold || (touches[1].position - stored[1].position).magnitude > size * size_threshold));
						if (mark > 0)
							break;

						Vector2 new01 = touches[1].position - touches[0].position;
						Vector2 old01 = new01 - touches[1].deltaPosition + touches[0].deltaPosition;

						OnDrag((touches[0].position + touches[1].position) * 0.5f, (touches[0].deltaPosition + touches[1].deltaPosition) * scalar * 0.5f);//delta position
						OnPinch((new01.magnitude - old01.magnitude) / size * scalar);//delta magnitude
						OnRotate(Mathf.Repeat(Mathf.Atan2(new01.y, new01.x) - Mathf.Atan2(old01.y, old01.x) + Mathf.PI, Mathf.PI * 2) - Mathf.PI);//delta angle
						break;
					}

					if (TouchPhase.Ended == touches[0].phase || TouchPhase.Ended == touches[1].phase)
					{
						OnSelect(target);
						if (false == modified && TouchPhase.Ended == touches[0].phase && TouchPhase.Ended == touches[1].phase)
							OnPicked(picked);
						if (true == modified)
							OnModified(target);
					}
					break;
			}

			//local function
			void UpdateState(bool moved)
			{
				if (true == moved && false == modified)
				{
					modified = true;
					if (null != picked)
						OnPicked(picked = null);
				}

				if (mark > 0)
				{
					if (Time.time - mark < time_threshold)
					{
						if (false == moved)
							return;
						target = null;
					}
					mark = 0;
					OnPicked(null);
					OnSelect(target);
				}
			}
		}

		//debug visual aid
		public bool showTouches = false;
		public void OnGUI() { if (true == showTouches) MultiTouchEmulator.OnGUI(); }
	}
}


