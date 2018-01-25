using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Map : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler, IPointerDownHandler, IPointerUpHandler
{
	private Game _game;
	
	private void Awake()
	{
		_game = FindObjectOfType<Game>();
	}

	public void OnPointerDown(PointerEventData eventData)
	{
		//_game.OnDrag(eventData);
	}
	
	public void OnPointerUp(PointerEventData eventData)
	{
		//_game.OnEndDrag(eventData);
	}
	
	public void OnBeginDrag(PointerEventData eventData)
	{
		//_game.OnDrag(eventData);
	}

	public void OnEndDrag(PointerEventData eventData)
	{
	}
	
	public void OnDrag(PointerEventData eventData)
	{
		//_game.OnDrag(eventData);
	}
}
