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
		public Vector3 From;
	}
	
	private abstract class PlayerAction
	{
		public int PlayerId = 0;
		public int CopyId = 0;
		public int ActionPointsPrice = 0;
		public int ActionPointsInitial = 0;
	}
	
	private class DiedAction : PlayerAction
	{
		
	}
	
	private class MoveAction : PlayerAction
	{
		public Vector3 Direction;
	}
	
	private class AttackAction : PlayerAction
	{
		public Vector3 direction;
	}
	
	private class PlayerTurn
	{
		public List<PlayerAction> Actions = new List<PlayerAction>();
	}

	public LineRenderer Trail;
	public Transform StartPosition;
	public Player player;
	public Text ActionPointsLeftText;
	public int ActionPointsPerMove = 10;
	public int ActionPointsPerAttack = 10;
	public int ActionPointsPerTurn = 100;
	public int FlashBackTurnsCount = 3;
	public float GridStep = 10;
	public Slider TimeSlider;
	private Dictionary<int, Player> _playerGenerations = new Dictionary<int, Player>();
	private List<PlayerTurn> _playerTurns = new List<PlayerTurn>();
	private List<LineRenderer> _trails = new List<LineRenderer>();
	private int CurrentTurn = -1;
	private int _trailsTurn = -1;
	
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
	private int playSpeed = 1;

	private List<ActionMarker> _markers = new List<ActionMarker>();

	private LineRenderer GenerateTrail(int APFrom, int APTo, int gen, Color col)
	{
		var trail = Instantiate(Trail, Trail.transform.parent);
		var positions = new List<Vector3>();
		CalculateSimulation(APFrom);
		positions.Add(_playerGenerations[gen].transform.position);
		
		for (int i = APFrom; i <= APTo;i++)
		{
			for (int j = 0; j <= _currentCopyId; j++)
			{
				CalculateSimulationStep(i / (ActionPointsPerTurn + 1), i % (ActionPointsPerTurn + 1), j);
			}
			positions.Add(_playerGenerations[gen].transform.position);
		}
		trail.positionCount = positions.Count;
		trail.SetPositions(positions.ToArray());
		var gradient = new Gradient();
		gradient.SetKeys(
			(new List<GradientColorKey>{new GradientColorKey(col, 0), new GradientColorKey(col, 1)}).ToArray(),
			(new List<GradientAlphaKey>{new GradientAlphaKey(0.25f, 0), new GradientAlphaKey(0.75f, 1)}).ToArray());
		trail.colorGradient = gradient;
		return trail;
	}
	public void BuildTrails(int turn, bool forceRebuild = false)
	{
		if (_trailsTurn != turn || forceRebuild)
		{
			_trailsTurn = turn;
			_trails.ForEach(tr => Destroy(tr.gameObject));
			_trails.Clear();
			
			if (turn > 0)
			{
				_trails.Add(GenerateTrail(0, turn * ActionPointsPerTurn, 0, Color.white));
			}
			
			if (turn + 1 < CurrentTurn)
			{
				_trails.Add(GenerateTrail((turn + 1) * ActionPointsPerTurn,
					(CurrentTurn + 1) * ActionPointsPerTurn - _currentActionPoints, 0, Color.black));
			}
			_trails.Add(GenerateTrail(turn * ActionPointsPerTurn, (turn + 1) * ActionPointsPerTurn, 0, Color.green));
		}
	}
	
	public void UpdatePossibleActions()
	{
		BuildTrails(CurrentTurn, true);
		_markers.ForEach(m =>
		{
			m.gameObject.SetActive(_currentActionPointsLeft > 0);
			Color col = Color.white;
			switch (_state)
			{
					case GameState.Move:col = Color.green;break;
					case GameState.Attack: col = Color.red;break;
			}
			m.GetComponent<Image>().color = col;
		});
		if (_currentActionPointsLeft > 0)
		{
			//left right up bottom
			Vector3 pos = GetLastPlayerPosition(_currentPlayerId, _currentCopyId);
			_markers[0].transform.position = pos + Vector3.right * GridStep;
			_markers[1].transform.position = pos + Vector3.left * GridStep;
			_markers[2].transform.position = pos + Vector3.up * GridStep;
			_markers[3].transform.position = pos + Vector3.down * GridStep;
		}
		
		CalculateSimulation((int)TimeSlider.maxValue);
		LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
	}
	
	public void OnFlashback()
	{
		FinalizeTurn();
		_currentCopyId++;
		_playerGenerations[_currentCopyId] = Instantiate(player, player.transform.parent);
		_playerGenerations[_currentCopyId].gameObject.SetActive(true);
		_playerGenerations[_currentCopyId].text.text = _currentCopyId.ToString();
		CurrentTurn = Mathf.Max(0, CurrentTurn - FlashBackTurnsCount);
		_currentActionPoints = ActionPointsPerTurn;
		//OnSpawnAction(_currentPlayerId, _currentCopyId, GetPlayerPositionAt(CurrentTurn + 1, 0, _currentCopyId - 1));
		OnSliderValueChanged(CurrentTurn * ActionPointsPerTurn);
		UpdatePossibleActions();
	}

	private void Start()
	{
		player.transform.position = RoundVector3ToFraction(StartPosition.position);
		player.SetColor(Color.green);
		player.text.text = _currentCopyId.ToString();
		_playerGenerations[0] = Instantiate(player, player.transform.parent);
		
		player.gameObject.SetActive(false);
		TimeSlider.minValue = 0;
		TimeSlider.maxValue = 0;
		_currentActionTarget.gameObject.SetActive(false);
		for (int i = 0; i < 4; i++)
		{
			_markers.Add(Instantiate(_currentActionTarget, _currentActionTarget.transform.parent));
			var mark = _markers[i];
			_markers[i].OnClick += () =>
			{
				OnClickOnAction(mark);
			};
		}
		//Place player on board
		CurrentTurn++;
		_playerTurns.Add(new PlayerTurn());
		_currentActionPoints = ActionPointsPerTurn;
		OnSpawnAction(0, 0, player.transform.position);
		UpdatePossibleActions();
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
		BuildTrails(CurrentTurn, true);
	}
	
	public void OnNextTurn()
	{
		FinalizeTurn();
		CurrentTurn++;
		_playerTurns.Add(new PlayerTurn());
		_currentActionPoints = ActionPointsPerTurn;
		UpdatePossibleActions();
	}

	public Vector3 GetLastPlayerPosition(int playerId, int copyId)
	{
		CalculateSimulation((ActionPointsPerTurn - _currentActionPoints) + CurrentTurn * ActionPointsPerTurn);
		return _playerGenerations[copyId].transform.position;
	}

	public void OnSpawnAction(int playerId, int copyId, Vector3 playerPos)
	{
		Vector3 from = playerPos;
		_playerTurns[CurrentTurn].Actions.Add(new SpawnAction()
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
			Direction = TargetPosition - from, 
			CopyId = copyId, 
			PlayerId = playerId,
			ActionPointsPrice = AP,
			ActionPointsInitial = ActionPointsPerTurn - _currentActionPoints,
		});
		
		_currentActionPoints -= AP;
	}
	
	public void AddAttack(int playerId, int copyId, int AP, Vector3 TargetPosition, ActionMarker marker)
	{
		Vector3 from = GetLastPlayerPosition(playerId, copyId);
		
		_playerTurns[CurrentTurn].Actions.Add(new AttackAction()
		{
			CopyId = copyId, 
			PlayerId = playerId,
			direction = (TargetPosition - from).normalized,
			ActionPointsPrice = ActionPointsPerAttack,
			ActionPointsInitial = ActionPointsPerTurn - _currentActionPoints,
		});
		
		_currentActionPoints -= AP;
	}

	/*public void OnDrag(PointerEventData eventData)
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
*/
	public void OnCalculateMoveAction(Vector2 position)
	{
		var pointPos = RoundVector3ToFraction(new Vector3(position.x, position.y, 0));
		_currentActionTarget.transform.position = pointPos;
		int AP = ActionPointsPerMove;
		AddMotion(_currentPlayerId, _currentCopyId, AP, pointPos, null);
	}
	
	public void OnCalculateAttackAction(Vector2 position)
	{
		var pointPos = RoundVector3ToFraction(new Vector3(position.x, position.y, 0));
		_currentActionTarget.transform.position = pointPos;
		int AP = ActionPointsPerMove;
		AddAttack(_currentPlayerId, _currentCopyId, AP, pointPos, null);
		
	}
	
	/*
	if (_state == GameState.Move)
		{
			
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
	}*/

	public void OnClickOnAction(ActionMarker button)
	{
		if (_state == GameState.Move)
		{
			OnCalculateMoveAction(button.transform.position);
		}

		if (_state == GameState.Attack)
		{
			OnCalculateAttackAction(button.transform.position);
		}
		UpdatePossibleActions();
	}

	public void OnMoveMode()
	{
		_state = GameState.Move;
		UpdatePossibleActions();
	}

	public void OnAttackMode()
	{
		_state = GameState.Attack;
		UpdatePossibleActions();
	}

	public void OnPlayMode()
	{
		_state = GameState.Play;
		_currentPlayPosition = -1;
	}

	public void ResetTurn()
	{
		_currentActionPoints = ActionPointsPerTurn;
		_playerTurns[CurrentTurn].Actions.RemoveAll(act =>
		{
			bool res = (!(act is SpawnAction)) && act.CopyId == _currentCopyId && act.PlayerId == _currentPlayerId;
			return res;
		});
		UpdatePossibleActions();
	}

	public void OnSliderValueChanged(float val)
	{
		BuildTrails(_currentPlayPosition / (ActionPointsPerTurn + 1));
		//if (_state == GameState.Play)
		//	return;
		
		_currentPlayPosition = Mathf.RoundToInt(val);
		CalculateSimulation(Mathf.RoundToInt(val));
	}

	private void CalculateSimulation(int stepsCount)
	{
		int simulateTurn = 0;
		int simulatePosition = 0;
		
		for (int i = 0; i <= stepsCount; i++)
		{
			if (_playerTurns.Count == simulateTurn)
			{
				return;
			}
			
			for (int j = 0; j <= _currentCopyId; j++)
			{
				CalculateSimulationStep(simulateTurn, simulatePosition, j);
			}
			
			//Physics2D.Simulate(0.01f);
			
			if (simulatePosition == ActionPointsPerTurn)
			{
				simulatePosition = 0;
				simulateTurn++;
			}
				
			simulatePosition++;
		}
	}

	private void CalculateSimulationStep(int turnNum, int position, int gen)
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
			var posDelta = move.Direction / move.ActionPointsPrice;
			_playerGenerations[gen].transform.position += posDelta;
		}
		else if(action is AttackAction)
		{
			_playerGenerations[gen].Arrow.SetActive(true);
			_playerGenerations[gen].Arrow.transform.rotation = Quaternion.FromToRotation(Vector3.up, (action as AttackAction).direction);
			_playerGenerations[gen].Arrow.transform.hasChanged = true;
		}
		else if (action is SpawnAction)
		{
			_playerGenerations[gen].transform.position = (action as SpawnAction).From;
		}
		
		_playerGenerations[gen].gameObject.SetActive(action != null);
	}
	
	public void Update()
	{
		if (_state == GameState.Play)
		{
			if (Mathf.RoundToInt(TimeSlider.maxValue) > _currentPlayPosition++)
			{
				//for (int j = 0; j <= _currentCopyId; j++)
				//{
				//	CalculateSimulationStep(_currentPlayPosition / (ActionPointsPerTurn + 1), _currentPlayPosition % (ActionPointsPerTurn + 1), j);
				//}
				TimeSlider.value = _currentPlayPosition;
			}
			else
			{
				_state = GameState.Move;
			}
		}
	}
}
