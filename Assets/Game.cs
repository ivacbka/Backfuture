using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Game : MonoBehaviour
{
	enum GameState
	{
		Move,
		Attack,
		Play,
		SliderSimulate
	}

	private class EmptyAction : PlayerAction
	{
	}
	
	private class SpawnAction : PlayerAction
	{
	}
	
	private abstract class PlayerAction
	{
		public int PlayerId = 0;
		public int CopyId = 0;
		public Vector3 From;
		public int ActionPointsPrice = 0;
		public int ActionPointsInitial = 0;
		public GameObject ActionMarker;
	}
	
	private class DiedAction : PlayerAction
	{
		
	}
	
	private class MoveAction : PlayerAction
	{
		public Vector3 To;
	}
	
	private class AttackAction : PlayerAction
	{
		public Vector3 direction;
	}
	
	private class PlayerTurn
	{
		public List<PlayerAction> Actions = new List<PlayerAction>();
	}

	public Transform StartPosition;
	public Player player;
	public Text ActionPointsLeftText;
	public int DistancePerActionPoint = 10;
	public int ActionPointsPerAttack = 10;
	public int ActionPointsPerTurn = 100;
	public int FlashBackTurnsCount = 3;
	public float GridStep = 10;
	public Slider TimeSlider;
	private Dictionary<int, Player> _playerGenerations = new Dictionary<int, Player>();
	private List<PlayerTurn> _playerTurns = new List<PlayerTurn>();
	private int CurrentTurn = -1;
	
	public ActionMarker _currentActionTarget;
	private GameState _state;
	
	private int _currentPlayerId;
	private int _currentCopyId;
	private int _currentActionPointsLeft;
	private int _currentActionPoints
	{
		get { return _currentActionPointsLeft; }
		set
		{
			ActionPointsLeftText.text = value.ToString();
			_currentActionPointsLeft = value;
			TimeSlider.maxValue = (CurrentTurn + 1) * ActionPointsPerTurn - _currentActionPointsLeft;
		}
	}
	
	private int _currentPlayPosition = 0;
	private int _currentPlayTurn = 0;
	private int playSpeed = 1;

	public void OnFlashback()
	{
		FinalizeTurn();
		_currentCopyId++;
		_playerGenerations[_currentCopyId] = Instantiate(player, player.transform.parent);
		_playerGenerations[_currentCopyId].gameObject.SetActive(true);
		_playerGenerations[_currentCopyId].text.text = _currentCopyId.ToString();
		CurrentTurn = Mathf.Max(0, CurrentTurn - FlashBackTurnsCount);
		_currentActionPoints = ActionPointsPerTurn;
		OnSpawnAction(_currentPlayerId, _currentCopyId, GetPlayerPositionAt(CurrentTurn + 1, 0, _currentCopyId - 1));
		OnSliderValueChanged(CurrentTurn * ActionPointsPerTurn);
	}

	private void Start()
	{
		player.transform.position = RoundVector3ToFraction(StartPosition.position);
		player.SetColor(Color.green);
		player.text.text = _currentCopyId.ToString();
		_playerGenerations[0] = Instantiate(player, player.transform.parent);
		
		player.gameObject.SetActive(false);
		TimeSlider.minValue = 0;
		TimeSlider.maxValue = _currentPlayTurn * ActionPointsPerTurn + _currentPlayTurn;
		//Place player on board
		OnNextTurn();
		OnSpawnAction(0, 0, player.transform.position);
	}
	
	public Vector3 RoundVector3ToFraction(Vector3 v)
	{
		v.x = Mathf.RoundToInt(v.x / GridStep) * GridStep;
		v.y = Mathf.RoundToInt(v.y / GridStep) * GridStep;
		v.z = Mathf.RoundToInt(v.z / GridStep) * GridStep;
		return v;
	}

	public void FinalizeTurn()
	{
		if (_currentActionPointsLeft > 0)
		{
			AddEmptyAction(_currentPlayerId, _currentCopyId, _currentActionPointsLeft);
		}

		for (int i = 0; i < _currentCopyId; i++)
		{
			AddEmptyAction(_currentPlayerId, i, ActionPointsPerTurn);
		}
		
		FindObjectsOfType<ActionMarker>().ToList().ForEach(m => m.gameObject.SetActive(false));
		TimeSlider.value = ((CurrentTurn + 1) * ActionPointsPerTurn);
	}
	
	public void OnNextTurn()
	{
		FinalizeTurn();
		CurrentTurn++;
		_playerTurns.Add(new PlayerTurn());
		_currentActionPoints = ActionPointsPerTurn;
		
	}

	public Vector3 GetLastPlayerPosition(int playerId, int copyId)
	{
		Vector3 from = RoundVector3ToFraction(StartPosition.position);
		for (int i = 0; i < _playerTurns.Count; i++)
		{
			var action = _playerTurns[i].Actions.FindLast(act => act.CopyId == copyId && act.PlayerId == playerId);
			if (action != null)
			{
				if (action is MoveAction)
				{
					from = (action as MoveAction).To;
				}
				else
				{
					from = action.From;
				}
			}
		}
		return from;
	}

	public void OnSpawnAction(int playerId, int copyId, Vector3 playerPos)
	{
		Vector3 from = playerPos;
		_playerTurns[CurrentTurn].Actions.Add(new EmptyAction()
		{
			From = from, 
			CopyId = copyId, 
			PlayerId = playerId,
			ActionPointsPrice = 0,
			ActionPointsInitial = 0,
		});
	}
	
	public void AddEmptyAction(int playerId, int copyId, int AP)
	{
		Vector3 from = GetLastPlayerPosition(playerId, copyId);
		_playerTurns[CurrentTurn].Actions.Add(new EmptyAction()
		{
			From = from, 
			CopyId = copyId, 
			PlayerId = playerId,
			ActionPointsPrice = AP,
			ActionPointsInitial = ActionPointsPerTurn - _currentActionPoints,
		});
		_currentActionPoints -= AP;
	}
	
	public void AddMotion(int playerId, int copyId, int AP, Vector3 TargetPosition, ActionMarker marker)
	{
		Vector3 from = GetLastPlayerPosition(playerId, copyId);
		
		_playerTurns[CurrentTurn].Actions.Add(new MoveAction()
		{
			From = from, 
			To = TargetPosition, 
			CopyId = copyId, 
			PlayerId = playerId,
			ActionPointsPrice = AP,
			ActionPointsInitial = ActionPointsPerTurn - _currentActionPoints,
			ActionMarker = marker.gameObject
		});
		
		_currentActionPoints -= AP;
	}
	
	public void AddAttack(int playerId, int copyId, int AP, Vector3 TargetPosition, ActionMarker marker)
	{
		Vector3 from = GetLastPlayerPosition(playerId, copyId);
		
		_playerTurns[CurrentTurn].Actions.Add(new AttackAction()
		{
			From = from,
			CopyId = copyId, 
			PlayerId = playerId,
			direction = (TargetPosition - from).normalized,
			ActionPointsPrice = ActionPointsPerAttack,
			ActionPointsInitial = ActionPointsPerTurn - _currentActionPoints,
			ActionMarker = marker.gameObject
		});
		
		_currentActionPoints -= AP;
	}

	public void OnDrag(PointerEventData eventData)
	{
		if (_state == GameState.Move)
		{
			_currentActionTarget.gameObject.SetActive( true );
			var pointPos = RoundVector3ToFraction(new Vector3(eventData.position.x, eventData.position.y, 0));
			_currentActionTarget.transform.position = pointPos;
			_currentActionTarget.text.text = Mathf.RoundToInt((pointPos 
		   		- GetLastPlayerPosition(_currentPlayerId, _currentCopyId)).magnitude 
				/ DistancePerActionPoint).ToString();
		}
		if (_state == GameState.Attack)
		{
			_currentActionTarget.gameObject.SetActive( true );
			var pointPos = RoundVector3ToFraction(new Vector3(eventData.position.x, eventData.position.y, 0));
			_currentActionTarget.transform.position = pointPos;
			_currentActionTarget.text.text = ActionPointsPerAttack.ToString();
		}
	}

	public void OnEndDrag(PointerEventData eventData)
	{
		if (_state == GameState.Move)
		{
			var pointPos = RoundVector3ToFraction(new Vector3(eventData.position.x, eventData.position.y, 0));
			_currentActionTarget.transform.position = pointPos;
			int AP = Mathf.RoundToInt((pointPos 
				- GetLastPlayerPosition(_currentPlayerId, _currentCopyId)).magnitude 
				/ DistancePerActionPoint);
			
			if (AP == 0)
				return;
			
			_currentActionTarget.text.text = AP.ToString();

			if (AP <= _currentActionPoints)
			{
				AddMotion(_currentPlayerId, _currentCopyId, AP, pointPos, Instantiate(_currentActionTarget, _currentActionTarget.transform.parent));
				_currentActionTarget.transform.SetAsLastSibling();
				_currentActionTarget.gameObject.SetActive(false);
			}
			else
			{
				_currentActionTarget.text.text = "NotEnough AP";
			}
		}

		if (_state == GameState.Attack)
		{
			var pointPos = RoundVector3ToFraction(new Vector3(eventData.position.x, eventData.position.y, 0));
			_currentActionTarget.transform.position = pointPos;
			int AP = ActionPointsPerAttack;
			
			_currentActionTarget.text.text = "S:" + AP;

			if (AP <= _currentActionPoints)
			{
				AddAttack(_currentPlayerId, _currentCopyId, AP, pointPos, Instantiate(_currentActionTarget, _currentActionTarget.transform.parent));
				_currentActionTarget.transform.SetAsLastSibling();
				_currentActionTarget.gameObject.SetActive(false);
			}
			else
			{
				_currentActionTarget.text.text = "NotEnough AP";
			}
		}
	}

	public void OnMoveMode()
	{
		_state = GameState.Move;
	}

	public void OnAttackMode()
	{
		_state = GameState.Attack;
	}

	public void OnPlayMode()
	{
		_state = GameState.Play;
		_currentPlayPosition = 0;
		_currentPlayTurn = 0;
	}

	public void ResetTurn()
	{
		_currentActionPoints = ActionPointsPerTurn;
		_playerTurns[CurrentTurn].Actions.RemoveAll(act =>
		{
			bool res = (!(act is SpawnAction)) && act.CopyId == _currentCopyId && act.PlayerId == _currentPlayerId;
			if (res)
			{
				Destroy(act.ActionMarker);
			}
			return res;
		});
	}

	public void OnSliderValueChanged(float val)
	{
		int simulateTurn = 0;
		int simulatePosition = 0;
		_state = GameState.SliderSimulate;
		
		for (int i = 0; i <= Mathf.RoundToInt(val); i++)
		{
			if (_playerTurns.Count == simulateTurn)
			{
				return;
			}
			
			for (int j = 0; j <= _currentCopyId; j++)
			{
				CalculateSimulation(simulateTurn, simulatePosition, j);
			}
			
			if (simulatePosition == ActionPointsPerTurn)
			{
				simulatePosition = 0;
				simulateTurn++;
			}
				
			simulatePosition++;
		}
	}

	private void CalculateSimulation(int turnNum, int position, int gen)
	{
		var action = _playerTurns[turnNum].Actions.Find(turn =>
			(turn.CopyId == gen) &&
			((turn.ActionPointsPrice > 0) &&
			(turn.ActionPointsInitial <= position) &&
			(turn.ActionPointsInitial + turn.ActionPointsPrice) >= position)
			|| (turn.ActionPointsPrice == 0 && turn.ActionPointsInitial == position));

		_playerGenerations[gen].Arrow.SetActive(false);
		if (action is MoveAction)
		{
			var move = action as MoveAction;
			var pos = move.From + (position - move.ActionPointsInitial) * (move.To - move.From) / move.ActionPointsPrice;
			_playerGenerations[gen].transform.position = pos;
			
		}
		else if(action is AttackAction)
		{
			_playerGenerations[gen].Arrow.SetActive(true);
			_playerGenerations[gen].Arrow.transform.rotation = Quaternion.FromToRotation(Vector3.up, (action as AttackAction).direction);
			_playerGenerations[gen].Arrow.transform.hasChanged = true;
			_playerGenerations[gen].transform.position = action.From;
		}
		else if (action != null)
		{
			_playerGenerations[gen].transform.position = action.From;
		}
		
		_playerGenerations[gen].gameObject.SetActive(action != null);
	}

	private Vector3 GetPlayerPositionAt(int turnNum, int position, int gen)
	{
		var action = _playerTurns[turnNum].Actions.Find(turn =>
			(turn.CopyId == gen) &
			((turn.ActionPointsPrice > 0) &&
			 (turn.ActionPointsInitial <= position) &&
			 (turn.ActionPointsInitial + turn.ActionPointsPrice) >= position)
			|| (turn.ActionPointsPrice == 0 && turn.ActionPointsInitial == position));

		if (action is MoveAction)
		{
			var move = action as MoveAction;
			var pos = move.From + (position - move.ActionPointsInitial) * (move.To - move.From) / move.ActionPointsPrice;
			return pos;
		}
		else if (action != null)
		{
			return action.From;
		}
		
		return Vector3.zero;
	}
	
	public void FixedUpdate()
	{
		if (_state == GameState.Play)
		{
			if (Mathf.RoundToInt(TimeSlider.maxValue) > _currentPlayPosition++)
			{
				TimeSlider.value = _currentPlayPosition;
				_state = GameState.Play;
			}
		}
	}
}
