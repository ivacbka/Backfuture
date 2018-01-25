using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ActionMarker : MonoBehaviour
{
	public event Action OnClick;

	public void Clicked()
	{
		if(OnClick != null)
			OnClick();
	}
	public Text text;
}
