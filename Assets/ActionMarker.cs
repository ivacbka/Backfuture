using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ActionMarker : MonoBehaviour
{
	public event Action OnClick;

	public Vector3 Position
	{
		get { return _position; }
		set
		{
			_position = value;
			transform.localPosition = value;
		}
	}

	private Vector3 _position;
	
	public void Clicked()
	{
		if(OnClick != null)
			OnClick();
	}
	public Text text;
}
