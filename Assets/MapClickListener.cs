using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MapClickListener : MonoBehaviour, IPointerClickHandler
{
	public enum SnapStyle
	{
		Edge,
		Cells
	}

	private SnapStyle _snapStyle;
	public event Action<int, int> OnCellClicked;
	public event Action<int, int, int, int> OnEdgeClicked;
	private MapEditor _map;

	private void Awake()
	{
		_map = FindObjectOfType<MapEditor>();
		_snapStyle = SnapStyle.Edge;
	}

	public void SetSnap(SnapStyle snap)
	{
		_snapStyle = snap;
	}
	
	public void OnPointerClick(PointerEventData eventData)
	{
		if (_snapStyle == SnapStyle.Cells)
		{
			var image = GetComponent<Image>();
			Vector3[] corners = new Vector3[4];
			image.rectTransform.GetWorldCorners(corners);
			var size = corners[2] - corners[0];
			float posX = (eventData.position.x - corners[0].x) / size.x;
			float posY = (eventData.position.y - corners[0].y) / size.y;
			int intPosX = (int)(posX * _map.MapSizeX);
			int intPosY = (int)(posY * _map.MapSizeY);
			Debug.LogFormat("OnClicked Cell {0}, {1}", intPosX, intPosY);
			
			if (OnCellClicked != null)
			{
				OnCellClicked(intPosX, intPosY);
			}
		}

		if (_snapStyle == SnapStyle.Edge)
		{
			var image = GetComponent<Image>();
			Vector3[] corners = new Vector3[4];
			image.rectTransform.GetWorldCorners(corners);
			var size = corners[2] - corners[0];
			float posX = (eventData.position.x - corners[0].x) / size.x;
			float posY = (eventData.position.y - corners[0].y) / size.y;
			float PosXUnrounded = posX * _map.MapSizeX;
			float PosYUnrounded = posY * _map.MapSizeY;
			int intPosX = (int)(posX * _map.MapSizeX);
			int intPosY = (int)(posY * _map.MapSizeY);
			
			float posXLeft = Mathf.Abs(intPosX - PosXUnrounded);
			float posXRight = Mathf.Abs(intPosX + 1 - PosXUnrounded);
			float posYTop = Mathf.Abs(intPosY + 1 - PosYUnrounded);
			float posYBottom = Mathf.Abs(intPosY - PosYUnrounded);
			bool horizontal = Mathf.Min(posXLeft, posXRight) > Mathf.Min(posYTop, posYBottom);
			
			if (horizontal)
			{
				if (posYTop < posYBottom)
					intPosY++;
			}
			else
			{
				if (posXRight < posXLeft)
					intPosX++;
			}
			
			Debug.LogFormat("OnClicked Edge {0}, {1}, {2}, {3}", intPosX, intPosY, horizontal ? intPosX + 1 : intPosX, horizontal ? intPosY : intPosY + 1);
			
			if (OnEdgeClicked != null)
			{
				OnEdgeClicked(intPosX, intPosY, horizontal ? intPosX + 1 : intPosX, horizontal ? intPosY : intPosY + 1);
			}
		}
	}
	
	
}
