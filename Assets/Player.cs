using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class Player : MonoBehaviour
{
	public GameObject ColoredChilds;
	public Text text;
	public GameObject Arrow;

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
	
	public void SetColor(Color col)
	{
		ColoredChilds.GetComponentsInChildren<UnityEngine.UI.Image>().ToList().ForEach(im => im.color = col);
	}
}
