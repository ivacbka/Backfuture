using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SliderText : MonoBehaviour
{
	public Text text;
	public void OnValueChanged (float val)
	{
		text.text = Mathf.RoundToInt(val).ToString();
	}
}
