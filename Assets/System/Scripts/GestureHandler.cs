// Copyright 2019 massivehadron.com ltd. created 25/04/2019 by Andrew Cakebread

using MassiveHadron;
using MassiveHadronLtd;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GestureHandler : TouchInterpreter
{
	public float zoomSpeed = 0.1f;

	private Camera _camera => GetComponent<Camera>();
	private Camera kamera { get { return _camera ?? Camera.main; } }//misspelled to avoid name collision
	private Transform subject;

	protected override bool TouchOverUI(Vector2 pt)
	{
		List<RaycastResult> results = new List<RaycastResult>();
		EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pt }, results);

		int layer = LayerMask.NameToLayer("UI");
		for (int n = 0; n < results.Count; ++n)
		{
			if (results[n].gameObject.layer == layer)
				return true;
		}
		return false;
	}

	protected override void OnSelect(Transform obj)
	{
	}

	protected override void OnLook(Vector2 position, Vector2 delta)
	{
		//Debug.Log("OnLook " + delta);
		GetComponentInParent<GimbleMovement>().Look(delta);
	}

	protected override void OnDrag(Vector2 position, Vector2 delta)
	{
		//Debug.Log("OnDrag " + delta);
	}

	protected override void OnPinch(float delta)
	{
		OnScroll(delta * 100.0f * (true == Application.isMobilePlatform ? 2 : 1));
	}

	protected override void OnScroll(float delta) => kamera.transform.localPosition = new Vector3(0, 0, Mathf.Clamp(kamera.transform.localPosition.z + delta * zoomSpeed, -4.5f, -0.5f));

	protected override void OnRotate(float delta)
	{
	}
}
