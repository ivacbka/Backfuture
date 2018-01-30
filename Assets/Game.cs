using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Runtime.Serialization;


public static class Vector3Utils
{
	public static Vector3 ToVector(this Vector3Wrapper wr)
	{
		return new Vector3(wr.x, wr.y, wr.z);
	}
	
	public static Vector3Wrapper ToWrapper(this Vector3 v)
	{
		return new Vector3Wrapper(v);
	}
}

[Serializable]
public class Vector3Wrapper
{
	public Vector3Wrapper()
	{
	}
	
	public Vector3Wrapper(float _x, float _y, float _z)
	{
		x = _x;
		y = _y;
		z = _z;
	}

	public Vector3Wrapper(Vector3 v)
	{
		x = v.x;
		y = v.y;
		z = v.z;
	}

	public float x;
	public float y;
	public float z;
}

public class Game : MonoBehaviour
{
	enum GameState
	{
		Move,
		Attack,
		Play,
		Flashback,
		SliderSimulate
	}
	
	[Serializable]
	public class EmptyAction : PlayerAction
	{
	}
	
	[Serializable]
	public class SpawnAction : PlayerAction
	{
		public Vector3Wrapper From;
	}
	
	[Serializable]
	public class PlayerAction
	{
		public int PlayerId = 0;
		public int CopyId = 0;
		public int ActionPointsPrice = 0;
		public int ActionPointsInitial = 0;
	}
	
	[Serializable]
	public class DiedAction : PlayerAction
	{
		
	}
	
	[Serializable]
	public class MoveAction : PlayerAction
	{
		//Data
		public Vector3Wrapper Direction;
		
		//SimulationShit
		[NonSerialized]
		public bool CanMove = true;
		[NonSerialized]
		public Vector3 TargetPosition;
	}
	
	[Serializable]
	public class AttackAction : PlayerAction
	{
		public Vector3Wrapper direction;
	}
	
	[Serializable]
	public class PlayerTurn
	{
		public List<PlayerAction> Actions = new List<PlayerAction>();
	}

	[Serializable]
	public class GameStateData
	{
		public List<PlayerTurn> PlayerTurns = new List<PlayerTurn>();
		public int CurrentTurn = -1;
		public int CurrentPlayerId;
		public int CurrentCopyId;
		public int CurrentActionPointsLeft;
		public Vector3Wrapper StartPosition;
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
	public int FlashbackZoneSize = 2;
	public Slider TimeSlider;
	private Dictionary<int, Player> _playerGenerations = new Dictionary<int, Player>();
	
	private List<LineRenderer> _trails = new List<LineRenderer>();
	
	private int _trailsTurn = -1;
	
	public ActionMarker _currentActionTarget;
	private GameState _state;
	
	
	private int _currentActionPoints
	{
		get { return _gameStateData.CurrentActionPointsLeft; }
		set
		{
			ActionPointsLeftText.text = value.ToString();
			_gameStateData.CurrentActionPointsLeft = value;
			TimeSlider.maxValue = (_gameStateData.CurrentTurn + 1) * ActionPointsPerTurn - _gameStateData.CurrentActionPointsLeft;
		}
	}
	
	private int _currentPlayPosition = 0;
	private int playSpeed = 1;
	private List<ActionMarker> _markers = new List<ActionMarker>();
	private GameStateData _gameStateData = new GameStateData();

	public void PrepareSimulation()
	{
		var positions = new List<Vector3>();
		for (int i = 0; i <= _gameStateData.CurrentCopyId; i++)
		{
			positions.Add(Vector3.zero);
		}
		
		foreach (var turn in _gameStateData.PlayerTurns)
		{
			foreach (var action in turn.Actions)
			{
				if (action is MoveAction)
				{
					var move = action as MoveAction;
					positions[action.CopyId] += move.Direction.ToVector();
					move.TargetPosition = positions[action.CopyId];
				}
				else if (action is SpawnAction)
				{
					positions[action.CopyId] = (action as SpawnAction).From.ToVector();
				}
			}
		}
	}

	private void OnActionsChanged()
	{
		PrepareSimulation();
	}
	
	private LineRenderer GenerateTrail(int APFrom, int APTo, int gen, Color col)
	{
		var trail = Instantiate(Trail, Trail.transform.parent);
		var positions = new List<Vector3>();
		CalculateSimulation(APFrom);
		
		for (int i = APFrom; i < APTo;i++)
		{
			for (int j = 0; j <= _gameStateData.CurrentCopyId; j++)
			{
				CalculateSimulationStep(i / (ActionPointsPerTurn), i % (ActionPointsPerTurn), j);
			}
			
			if(positions.Count == 0 || (RoundVector3ToFraction(positions[positions.Count - 1]) - RoundVector3ToFraction(_playerGenerations[gen].transform.position)).magnitude >= (GridStep - 0.05f))
				positions.Add(RoundVector3ToFraction(_playerGenerations[gen].transform.position));
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
			for (int i = 0; i <= _gameStateData.CurrentCopyId; i++)
			{
				if (turn > 0)
				{
					_trails.Add(GenerateTrail(0, turn * ActionPointsPerTurn, i, Color.white));
				}
			
				if (turn + 1 < _gameStateData.CurrentTurn)
				{
					_trails.Add(GenerateTrail((turn + 1) * ActionPointsPerTurn,
						(_gameStateData.CurrentTurn + 1) * ActionPointsPerTurn - _currentActionPoints, i, Color.black));
				}
				_trails.Add(GenerateTrail(turn * ActionPointsPerTurn, (turn + 1) * ActionPointsPerTurn, i, Color.green));
			}
		}
	}
	
	public void UpdatePossibleActions()
	{
		LayoutRebuilder.MarkLayoutForRebuild(transform as RectTransform);
		BuildTrails(_gameStateData.CurrentTurn, true);
		
		Vector3 pos = GetLastPlayerPosition(_gameStateData.CurrentPlayerId, _gameStateData.CurrentCopyId);
		if (_state != GameState.Flashback)
		{
			_markers[0].gameObject.SetActive(true);
			_markers[0].transform.localPosition = pos + Vector3.right * GridStep;
			_markers[1].transform.localPosition = pos + Vector3.left * GridStep;
			_markers[2].transform.localPosition = pos + Vector3.up * GridStep;
			_markers[3].transform.localPosition = pos + Vector3.down * GridStep;
			for (int i = 0; i < _markers.Count; i++)
			{
				_markers[i].gameObject.SetActive(_gameStateData.CurrentActionPointsLeft > 0 && i < 4);
			}
		}
		else
		{
			int index = 0;
			for (int i = -FlashbackZoneSize; i <= FlashbackZoneSize; i++)
			{
				for (int j = -FlashbackZoneSize; j <= FlashbackZoneSize; j++)
				{
					if(_markers.Count == index)
						AddMarker();

					_markers[index].gameObject.SetActive(true);
					(_markers[index].transform as RectTransform).localPosition = pos + Vector3.right * GridStep * i + Vector3.up * GridStep * j;
					index++;
				}
			}
		}
		
		_markers.ForEach(m =>
		{
			Color col = Color.white;
			switch (_state)
			{
				case GameState.Move:col = Color.green;break;
				case GameState.Attack: col = Color.red;break;
				case GameState.Flashback: col = Color.blue;break;
			}
			m.GetComponent<Image>().color = col;
		});
		
		CalculateSimulation((int)TimeSlider.maxValue);
	}

	private void AddFlashback(Vector3 pos)
	{
		FinalizeTurn();
		_gameStateData.CurrentCopyId++;
		var pg = Instantiate(player, player.transform.parent);
		_playerGenerations[_gameStateData.CurrentCopyId] = pg;
		pg.gameObject.SetActive(true);
		pg.transform.position = pos;
		pg.text.text = _gameStateData.CurrentCopyId.ToString();
		_gameStateData.CurrentTurn = Mathf.Max(0, _gameStateData.CurrentTurn - FlashBackTurnsCount);
		_currentActionPoints = ActionPointsPerTurn;
		OnSpawnAction(_gameStateData.CurrentPlayerId, _gameStateData.CurrentCopyId, pos);
		OnSliderValueChanged(_gameStateData.CurrentTurn * ActionPointsPerTurn);
	}

	private void AddMarker()
	{
		var newMark = Instantiate(_currentActionTarget, _currentActionTarget.transform.parent);
		newMark.gameObject.SetActive(true);
		_markers.Add(newMark);
		newMark.OnClick += () =>
		{
			OnClickOnAction(newMark);
		};
	}
	
	private void Start()
	{
		player.transform.position = RoundVector3ToFraction(StartPosition.position);
		player.SetColor(Color.green);
		player.text.text = _gameStateData.CurrentCopyId.ToString();
		_playerGenerations[0] = Instantiate(player, player.transform.parent);
		
		player.gameObject.SetActive(false);
		TimeSlider.minValue = 0;
		TimeSlider.maxValue = 0;
		_currentActionTarget.gameObject.SetActive(false);
		for (int i = 0; i < 4; i++)
		{
			AddMarker();
		}
		//Place player on board
		_gameStateData.CurrentTurn++;
		_gameStateData.PlayerTurns.Add(new PlayerTurn());
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
		if (_gameStateData.CurrentActionPointsLeft > 0)
		{
			AddEmptyAction(_gameStateData.CurrentPlayerId, _gameStateData.CurrentCopyId, _gameStateData.CurrentActionPointsLeft);
		}

		for (int i = 0; i < _gameStateData.CurrentCopyId; i++)
		{
			AddEmptyAction(_gameStateData.CurrentPlayerId, i, ActionPointsPerTurn);
		}
		
		FindObjectsOfType<ActionMarker>().ToList().ForEach(m => m.gameObject.SetActive(false));
		TimeSlider.value = ((_gameStateData.CurrentTurn + 1) * ActionPointsPerTurn);
		BuildTrails(_gameStateData.CurrentTurn, true);
	}
	
	public void OnNextTurn()
	{
		FinalizeTurn();
		_gameStateData.CurrentTurn++;
		_gameStateData.PlayerTurns.Add(new PlayerTurn());
		_currentActionPoints = ActionPointsPerTurn;
		UpdatePossibleActions();
		for (int i = 0; i < _gameStateData.CurrentCopyId; i++)
		{
			AddEmptyAction(_gameStateData.CurrentPlayerId, i, ActionPointsPerTurn);
		}
	}

	public Vector3 GetLastPlayerPosition(int playerId, int copyId)
	{
		CalculateSimulation((ActionPointsPerTurn - _currentActionPoints) + _gameStateData.CurrentTurn * ActionPointsPerTurn);
		return _playerGenerations[copyId].transform.position;
	}

	public void OnSpawnAction(int playerId, int copyId, Vector3 playerPos)
	{
		Vector3 from = playerPos;
		_gameStateData.PlayerTurns[_gameStateData.CurrentTurn].Actions.Add(new SpawnAction()
		{
			From = RoundVector3ToFraction(from).ToWrapper(), 
			CopyId = copyId, 
			PlayerId = playerId,
			ActionPointsPrice = 0,
			ActionPointsInitial = 0,
		});
		OnActionsChanged();
	}
	
	public void AddEmptyAction(int playerId, int copyId, int AP)
	{
		Vector3 from = GetLastPlayerPosition(playerId, copyId);
		_gameStateData.PlayerTurns[_gameStateData.CurrentTurn].Actions.Add(new EmptyAction()
		{
			CopyId = copyId, 
			PlayerId = playerId,
			ActionPointsPrice = AP,
			ActionPointsInitial = copyId == _gameStateData.CurrentCopyId ? ActionPointsPerTurn - _currentActionPoints : 0,
		});
		
		if(copyId == _gameStateData.CurrentCopyId)
			_currentActionPoints -= AP;
		
		OnActionsChanged();
	}
	
	public void AddMotion(int playerId, int copyId, int AP, Vector3 TargetPosition, ActionMarker marker)
	{
		Vector3 from = GetLastPlayerPosition(playerId, copyId);
		
		_gameStateData.PlayerTurns[_gameStateData.CurrentTurn].Actions.Add(new MoveAction()
		{
			Direction = RoundVector3ToFraction((TargetPosition - from)).ToWrapper(), 
			CopyId = copyId, 
			PlayerId = playerId,
			ActionPointsPrice = AP,
			ActionPointsInitial = ActionPointsPerTurn - _currentActionPoints,
		});
		
		_currentActionPoints -= AP;
		OnActionsChanged();
	}
	
	public void AddAttack(int playerId, int copyId, int AP, Vector3 TargetPosition, ActionMarker marker)
	{
		Vector3 from = GetLastPlayerPosition(playerId, copyId);
		
		_gameStateData.PlayerTurns[_gameStateData.CurrentTurn].Actions.Add(new AttackAction()
		{
			CopyId = copyId, 
			PlayerId = playerId,
			direction = (TargetPosition - from).normalized.ToWrapper(),
			ActionPointsPrice = ActionPointsPerAttack,
			ActionPointsInitial = ActionPointsPerTurn - _currentActionPoints,
		});
		
		_currentActionPoints -= AP;
		OnActionsChanged();
	}

	public void OnCalculateFlashbackAction(Vector2 position)
	{
		var pointPos = RoundVector3ToFraction(new Vector3(position.x, position.y, 0));
		_currentActionTarget.transform.position = pointPos;
		AddFlashback(pointPos);
	}
	public void OnCalculateMoveAction(Vector2 position)
	{
		var pointPos = RoundVector3ToFraction(new Vector3(position.x, position.y, 0));
		_currentActionTarget.transform.position = pointPos;
		int AP = ActionPointsPerMove;
		AddMotion(_gameStateData.CurrentPlayerId, _gameStateData.CurrentCopyId, AP, pointPos, null);
	}
	
	public void OnCalculateAttackAction(Vector2 position)
	{
		var pointPos = RoundVector3ToFraction(new Vector3(position.x, position.y, 0));
		_currentActionTarget.transform.position = pointPos;
		int AP = ActionPointsPerAttack;
		AddAttack(_gameStateData.CurrentPlayerId, _gameStateData.CurrentCopyId, AP, pointPos, null);
		
	}

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
		
		if (_state == GameState.Flashback)
		{
			OnCalculateFlashbackAction(button.transform.position);
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
	
	public void OnFlashback()
	{
		_state = GameState.Flashback;
		UpdatePossibleActions();
	}

	public void ResetTurn()
	{
		_currentActionPoints = ActionPointsPerTurn;
		_gameStateData.PlayerTurns[_gameStateData.CurrentTurn].Actions.RemoveAll(act =>
		{
			bool res = (!(act is SpawnAction)) 
			           && act.CopyId == _gameStateData.CurrentCopyId 
			           && act.PlayerId == _gameStateData.CurrentPlayerId;
			return res;
		});
		OnActionsChanged();
		UpdatePossibleActions();
	}

	public void OnSliderValueChanged(float val)
	{
		BuildTrails(_currentPlayPosition / (ActionPointsPerTurn));
		//if (_state == GameState.Play)
		//	return;
		
		_currentPlayPosition = Mathf.RoundToInt(val);
		CalculateSimulation(Mathf.RoundToInt(val));
	}

	private void CalculateSimulation(int stepsCount)
	{
		int simulateTurn = 0;
		int simulatePosition = 0;
		
		for (int i = 0; i < stepsCount; i++)
		{
			if (_gameStateData.PlayerTurns.Count == simulateTurn)
			{
				return;
			}
			
			for (int j = 0; j <= _gameStateData.CurrentCopyId; j++)
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
		if (_gameStateData.PlayerTurns.Count <= turnNum)
			return;
		
		var action = _gameStateData.PlayerTurns[turnNum].Actions.Find(turn =>
			(turn.CopyId == gen) &&
			(((turn.ActionPointsPrice > 0) &&
			(turn.ActionPointsInitial <= position) &&
			(turn.ActionPointsInitial + turn.ActionPointsPrice) >= position)
			|| (turn.ActionPointsPrice == 0 && turn.ActionPointsInitial == position)));

		_playerGenerations[gen].Arrow.SetActive(action is AttackAction);
		_playerGenerations[gen].gameObject.SetActive(action != null);
		if (action is MoveAction)
		{
			var move = action as MoveAction;
			float t = (float)(position - move.ActionPointsInitial + 1) / move.ActionPointsPrice;
			Vector3 from = move.TargetPosition - move.Direction.ToVector();
			var pos = Vector3.Lerp(from, move.TargetPosition, t);
			_playerGenerations[gen].transform.localPosition = pos;
		}
		else if(action is AttackAction)
		{
			_playerGenerations[gen].Arrow.transform.rotation = Quaternion.FromToRotation(Vector3.up, (action as AttackAction).direction.ToVector());
			_playerGenerations[gen].Arrow.transform.hasChanged = true;
		}
		else if (action is SpawnAction)
		{
			_playerGenerations[gen].transform.localPosition = (action as SpawnAction).From.ToVector();
		}

		if ((action == null) && _playerGenerations[gen].gameObject.activeSelf)
		{
			Debug.LogFormat("Playerdisabled Turn {0} Pos {1} Gen {2}", turnNum, position, gen);
		}
		
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

		if (Input.GetKeyDown(KeyCode.Space))
		{
			Type[] typeArray = new[] {typeof(EmptyAction), typeof(SpawnAction), typeof(PlayerAction), typeof(MoveAction), 
				typeof(AttackAction), typeof(PlayerTurn)};
			
			var f = File.CreateText(Application.streamingAssetsPath + "/debug");
			f.Write(SerializationUtils<GameStateData>.SerializeXml(_gameStateData, typeArray));
			f.Flush();
			f.Close();
		}
		
		if (Input.GetKeyDown(KeyCode.R))
		{
			Type[] typeArray = new[] {typeof(GameStateData), typeof(EmptyAction), typeof(SpawnAction), typeof(PlayerAction), typeof(MoveAction), 
				typeof(AttackAction), typeof(PlayerTurn)};
			
			var f = File.OpenText(Application.streamingAssetsPath + "/debug");
			_gameStateData = SerializationUtils<GameStateData>.DeserializeXmlString(f.ReadToEnd(), typeArray);
			foreach (var pl in _playerGenerations)
			{
				if (pl.Value.gameObject != null)
				{
					Destroy(pl.Value.gameObject);
				}
			}
			_playerGenerations.Clear();
			
			for (int i = 0; i <= _gameStateData.CurrentCopyId;i++)
			{
				var pg = Instantiate(player, player.transform.parent);
				_playerGenerations[i] = pg;
				pg.gameObject.SetActive(true);
				pg.transform.position = Vector3.zero;
				pg.text.text = i.ToString();
			}

			_currentActionPoints = _currentActionPoints;
			f.Close();
			UpdatePossibleActions();
		}
	}
}
