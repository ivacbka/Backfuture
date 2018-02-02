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

public class Game : MonoBehaviour
{
	enum GameState
	{
		Move,
		Push,
		Attack,
		Play,
		Flashback,
		SliderSimulate
	}

	public LineRenderer Trail;
	public Transform StartPosition;
	public Player player;
	public Text ActionPointsLeftText;
	public Text FlashbacksLeftText;
	public int ActionPointsPerMove = 10;
	public int ActionPointsPerAttack = 10;
	public int ActionPointsPerTurn = 100;
	public int FlashBackTurnsCount = 3;
	public float GridStep = 10;
	public int FlashbackZoneSize = 2;
	public int TurnsPerFlashback = 3;
	public Slider TimeSlider;
	private Dictionary<int, Player> _playerGenerations = new Dictionary<int, Player>();
	
	private List<LineRenderer> _trails = new List<LineRenderer>();
	
	private int _trailsTurn = -1;
	
	public ActionMarker _currentActionTarget;
	private GameState _state;

	public class GameEvent
	{
		public Vector3 Position;
		public int APFrom;
		public int APTo;
		public List<Player> _playersInvolved;
	}

	public class BlockageEvent: GameEvent
	{
		
	}
	
	public List<GameEvent> Events;
	
	private int _currentActionPoints
	{
		get { return _localgameStateData.CurrentActionPointsLeft; }
		set
		{
			ActionPointsLeftText.text = value.ToString();
			_localgameStateData.CurrentActionPointsLeft = value;
			TimeSlider.maxValue = (_localgameStateData.CurrentTurn + 1) * ActionPointsPerTurn - _localgameStateData.CurrentActionPointsLeft;
		}
	}
	
	private int _currentPlayPosition = 0;
	private int playSpeed = 1;
	private List<ActionMarker> _markers = new List<ActionMarker>();
	private GameStateData _localgameStateData = new GameStateData();
	private GameStateData _simGameStateData = new GameStateData();

	public void PrepareSimulation()
	{
		var positions = new List<Vector3>();
		for (int i = 0; i <= _simGameStateData.CurrentCopyId; i++)
		{
			positions.Add(Vector3.zero);
		}
		
		foreach (var turn in _simGameStateData.PlayerTurns)
		{
			foreach (var action in turn.Actions)
			{
				if (action is MoveAction)
				{
					var move = action as MoveAction;
					positions[action.CopyId] += move.Direction.ToVector();
					move.TargetPosition = new Vector3Wrapper(positions[action.CopyId]);
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
		CalculateEvents();
	}
	
	private LineRenderer GenerateTrail(int APFrom, int APTo, int gen, Color col)
	{
		var trail = Instantiate(Trail, Trail.transform.parent);
		var positions = new List<Vector3>();

		int APFRomTurn = APFrom / (ActionPointsPerTurn);
		int SpawnedTurn = _simGameStateData.PlayerTurns.FindIndex(turn =>
			turn.Actions.Find(act => (act is SpawnAction) && ((act as SpawnAction).CopyId == gen)) != null);
		
		if (SpawnedTurn > APFRomTurn)
			APFrom = SpawnedTurn * ActionPointsPerTurn;
		
		CalculateSimulation(APFrom);
		
		for (int i = APFrom; i < APTo;i++)
		{
			for (int j = 0; j <= _simGameStateData.CurrentCopyId; j++)
			{
				CalculateSimulationStep(i / (ActionPointsPerTurn), i % (ActionPointsPerTurn), j);
			}
			
			if(positions.Count == 0 || (RoundVector3ToFraction(positions[positions.Count - 1]) - RoundVector3ToFraction(_playerGenerations[gen].Position)).magnitude >= (GridStep - 0.05f))
				positions.Add(RoundVector3ToFraction(_playerGenerations[gen].Position));
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
			for (int i = 0; i <= _simGameStateData.CurrentCopyId; i++)
			{
				if (turn > 0)
				{
					_trails.Add(GenerateTrail(0, turn * ActionPointsPerTurn, i, Color.white));
				}
			
				if (turn + 1 < _simGameStateData.CurrentTurn)
				{
					_trails.Add(GenerateTrail((turn + 1) * ActionPointsPerTurn,
						(_simGameStateData.CurrentTurn + 1) * ActionPointsPerTurn - _currentActionPoints, i, Color.black));
				}
				_trails.Add(GenerateTrail(turn * ActionPointsPerTurn, (turn + 1) * ActionPointsPerTurn, i, Color.green));
			}
		}
	}
	
	public void UpdatePossibleActions()
	{
		CalculateSimulation((int)TimeSlider.maxValue);
		FlashbacksLeftText.text = _simGameStateData.CurrentFlashbacksCount.ToString();
		LayoutRebuilder.MarkLayoutForRebuild(transform as RectTransform);
		BuildTrails(_simGameStateData.CurrentTurn, true);
		
		Vector3 pos = GetLastPlayerPosition(_simGameStateData.CurrentPlayerId, _simGameStateData.CurrentCopyId);
		if (_state != GameState.Flashback)
		{
			_markers[0].gameObject.SetActive(true);
			_markers[0].Position = pos + Vector3.right * GridStep;
			_markers[1].Position = pos + Vector3.left * GridStep;
			_markers[2].Position = pos + Vector3.up * GridStep;
			_markers[3].Position = pos + Vector3.down * GridStep;
			for (int i = 0; i < _markers.Count; i++)
			{
				_markers[i].gameObject.SetActive(_simGameStateData.CurrentActionPointsLeft > 0 && i < 4);
			}
		}
		else
		{
			int index = 0;
			for (int i = -FlashbackZoneSize; i <= FlashbackZoneSize; i++)
			{
				for (int j = -FlashbackZoneSize; j <= FlashbackZoneSize; j++)
				{
					if (_markers.Count == index)
					{
						AddMarker();
					}

					_markers[index].gameObject.SetActive(_simGameStateData.CurrentFlashbacksCount > 0);
					_markers[index].Position = pos + Vector3.right * GridStep * i + Vector3.up * GridStep * j;
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
	}

	private void AddPush(int playerId, int copyId, int AP, Vector3 TargetPosition, ActionMarker marker)
	{
		Vector3 from = GetLastPlayerPosition(playerId, copyId);
		
		_localgameStateData.PlayerTurns[_localgameStateData.CurrentTurn].Actions.Add(new PushAction()
		{
			CopyId = copyId, 
			PlayerId = playerId,
			Direction = (TargetPosition - from).ToWrapper(),
			ActionPointsPrice = ActionPointsPerAttack,
			ActionPointsInitial = ActionPointsPerTurn - _currentActionPoints,
		});
		
		_currentActionPoints -= AP;
		OnActionsChanged();
	}
	
	private void AddFlashback(Vector3 pos)
	{
		_localgameStateData.CurrentFlashbacksCount--;
		FinalizeTurn();
		_localgameStateData.CurrentCopyId++;
		var pg = Instantiate(player, player.transform.parent);
		_playerGenerations[_localgameStateData.CurrentCopyId] = pg;
		pg.gameObject.SetActive(true);
		pg.Position = pos;
		pg.text.text = _localgameStateData.CurrentCopyId.ToString();
		_localgameStateData.CurrentTurn = Mathf.Max(0, _localgameStateData.CurrentTurn - FlashBackTurnsCount);
		_currentActionPoints = ActionPointsPerTurn;
		OnSpawnAction(_localgameStateData.CurrentPlayerId, _localgameStateData.CurrentCopyId, pos);
		OnSliderValueChanged(_localgameStateData.CurrentTurn * ActionPointsPerTurn);
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
		player.Position = RoundVector3ToFraction(StartPosition.position);
		player.SetColor(Color.green);
		player.text.text = _localgameStateData.CurrentCopyId.ToString();
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
		_localgameStateData.CurrentTurn++;
		_localgameStateData.PlayerTurns.Add(new PlayerTurn());
		_currentActionPoints = ActionPointsPerTurn;
		OnSpawnAction(0, 0, player.Position);
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
		while(_localgameStateData.CurrentActionPointsLeft > 0)
		{
			AddEmptyAction(_localgameStateData.CurrentPlayerId, _localgameStateData.CurrentCopyId, ActionPointsPerMove, 0);
		}

		for (int i = 0; i < _localgameStateData.CurrentCopyId; i++)
		{
			for (int j = 0; j < ActionPointsPerTurn / ActionPointsPerMove; j++)
			{
				AddEmptyAction(_localgameStateData.CurrentPlayerId, i, ActionPointsPerMove, j * ActionPointsPerMove);
			}
		}
		
		FindObjectsOfType<ActionMarker>().ToList().ForEach(m => m.gameObject.SetActive(false));
		TimeSlider.value = ((_localgameStateData.CurrentTurn + 1) * ActionPointsPerTurn);
		BuildTrails(_localgameStateData.CurrentTurn, true);
	}
	
	public void OnNextTurn()
	{
		FinalizeTurn();
		
		_localgameStateData.CurrentTurn++;
		if (_localgameStateData.CurrentTopTurn < _localgameStateData.CurrentTurn)
		{
			_localgameStateData.CurrentTopTurn++;
			// new top turn add flashback charge
			if (_localgameStateData.CurrentTopTurn % TurnsPerFlashback == 0)
			{
				_localgameStateData.CurrentFlashbacksCount++;
			}
		}
		
		if (_localgameStateData.PlayerTurns.Count == _localgameStateData.CurrentTurn)
		{
			_localgameStateData.PlayerTurns.Add(new PlayerTurn());
			
		}

		_currentActionPoints = ActionPointsPerTurn;
		UpdatePossibleActions();
		for (int i = 0; i < _localgameStateData.CurrentCopyId; i++)
		{
			for (int j = 0; j < ActionPointsPerTurn / ActionPointsPerMove; j++)
			{
				AddEmptyAction(_localgameStateData.CurrentPlayerId, i, ActionPointsPerMove, j * ActionPointsPerMove);
			}
		}
	}

	public Vector3 GetLastPlayerPosition(int playerId, int copyId)
	{
		CalculateSimulation((ActionPointsPerTurn - _currentActionPoints) + _localgameStateData.CurrentTurn * ActionPointsPerTurn);
		return _playerGenerations[copyId].Position;
	}

	public void OnSpawnAction(int playerId, int copyId, Vector3 playerPos)
	{
		Vector3 from = playerPos;
		_localgameStateData.PlayerTurns[_localgameStateData.CurrentTurn].Actions.Add(new SpawnAction()
		{
			From = RoundVector3ToFraction(from).ToWrapper(), 
			CopyId = copyId, 
			PlayerId = playerId,
			ActionPointsPrice = 0,
			ActionPointsInitial = 0,
		});
		OnActionsChanged();
	}
	
	public void AddEmptyAction(int playerId, int copyId, int AP, int APInit)
	{
		Vector3 from = GetLastPlayerPosition(playerId, copyId);
		_localgameStateData.PlayerTurns[_localgameStateData.CurrentTurn].Actions.Add(new EmptyAction()
		{
			CopyId = copyId, 
			PlayerId = playerId,
			ActionPointsPrice = AP,
			ActionPointsInitial = copyId == _localgameStateData.CurrentCopyId ? ActionPointsPerTurn - _currentActionPoints : APInit,
		});
		
		if(copyId == _localgameStateData.CurrentCopyId)
			_currentActionPoints -= AP;
		
		OnActionsChanged();
	}
	
	public void AddMotion(int playerId, int copyId, int AP, Vector3 TargetPosition, ActionMarker marker)
	{
		Vector3 from = GetLastPlayerPosition(playerId, copyId);
		
		_localgameStateData.PlayerTurns[_localgameStateData.CurrentTurn].Actions.Add(new MoveAction()
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
		
		_localgameStateData.PlayerTurns[_localgameStateData.CurrentTurn].Actions.Add(new AttackAction()
		{
			CopyId = copyId, 
			PlayerId = playerId,
			Direction = (TargetPosition - from).normalized.ToWrapper(),
			ActionPointsPrice = ActionPointsPerAttack,
			ActionPointsInitial = ActionPointsPerTurn - _currentActionPoints,
		});
		
		_currentActionPoints -= AP;
		OnActionsChanged();
	}

	public void OnCalculatePushAction(Vector2 position)
	{
		var pointPos = RoundVector3ToFraction(new Vector3(position.x, position.y, 0));
		_currentActionTarget.Position = pointPos;
		AddPush(_localgameStateData.CurrentPlayerId, _localgameStateData.CurrentCopyId, ActionPointsPerAttack, pointPos, null);
	}
	
	public void OnCalculateFlashbackAction(Vector2 position)
	{
		var pointPos = RoundVector3ToFraction(new Vector3(position.x, position.y, 0));
		_currentActionTarget.Position = pointPos;
		AddFlashback(pointPos);
	}
	
	public void OnCalculateMoveAction(Vector2 position)
	{
		var pointPos = RoundVector3ToFraction(new Vector3(position.x, position.y, 0));
		_currentActionTarget.Position = pointPos;
		int AP = ActionPointsPerMove;
		AddMotion(_localgameStateData.CurrentPlayerId, _localgameStateData.CurrentCopyId, AP, pointPos, null);
	}
	
	public void OnCalculateAttackAction(Vector2 position)
	{
		var pointPos = RoundVector3ToFraction(new Vector3(position.x, position.y, 0));
		_currentActionTarget.Position = pointPos;
		int AP = ActionPointsPerAttack;
		AddAttack(_localgameStateData.CurrentPlayerId, _localgameStateData.CurrentCopyId, AP, pointPos, null);
		
	}

	public void OnClickOnAction(ActionMarker button)
	{
		if (_state == GameState.Move)
		{
			OnCalculateMoveAction(button.Position);
		}

		if (_state == GameState.Attack)
		{
			OnCalculateAttackAction(button.Position);
		}
		
		if (_state == GameState.Flashback)
		{
			OnCalculateFlashbackAction(button.Position);
		}
		
		if (_state == GameState.Push)
		{
			OnCalculatePushAction(button.Position);
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
	
	public void OnPushMode()
	{
		_state = GameState.Push;
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
		_localgameStateData.PlayerTurns[_localgameStateData.CurrentTurn].Actions.RemoveAll(act =>
		{
			bool res = (!(act is SpawnAction)) 
			           && act.CopyId == _localgameStateData.CurrentCopyId 
			           && act.PlayerId == _localgameStateData.CurrentPlayerId;
			return res;
		});
		OnActionsChanged();
		UpdatePossibleActions();
	}

	public void OnSliderValueChanged(float val)
	{
		BuildTrails(_currentPlayPosition / (ActionPointsPerTurn));
		_currentPlayPosition = Mathf.RoundToInt(val);
		CalculateSimulation(Mathf.RoundToInt(val));
	}

	public void CalculateEvents()
	{
		SerializeGameState(_localgameStateData, "temp");
		_simGameStateData = DeserializeGameState("temp");
		//merge prepare simulation with this
		int actionsPerTurn = ActionPointsPerTurn / ActionPointsPerMove;
		
		int simulateTurn = 0;
		int simulatePosition = 0;
		
		_simGameStateData.PlayerTurns.ForEach(tn => tn.Actions.ForEach(
			act =>
			{
				if (act is SpawnAction)
				{
					var sp = act as SpawnAction;
					_playerGenerations[sp.CopyId].Position = sp.From.ToVector();
				}
			}));

		int currentStep = _simGameStateData.CurrentTopTurn * actionsPerTurn;
		
		if (_simGameStateData.CurrentTopTurn == _simGameStateData.CurrentTurn)
			currentStep += (ActionPointsPerTurn - _simGameStateData.CurrentActionPointsLeft) / ActionPointsPerMove;
		
		for (int step = 0; step < currentStep; step++)
		{
			//get all simultaneous actions and calculate events
			int stepFraction = (step % actionsPerTurn) * ActionPointsPerMove;
			int turn = step / (ActionPointsPerTurn / ActionPointsPerMove);
			
			var pushes = _simGameStateData.PlayerTurns[turn].Actions.FindAll(
				act => (act is PushAction) && act.ActionPointsInitial == stepFraction);

			//players to push
			var pushesPairs = new List<KeyValuePair<int, Vector3>>();
			foreach (var push in pushes)
			{
				var pus = push as PushAction;
				var pushTarget = RoundVector3ToFraction(_playerGenerations[pus.CopyId].Position + pus.Direction.ToVector());
				foreach (var vals in _playerGenerations.Keys)
				{
					if ((_playerGenerations[vals].Position - pushTarget).magnitude < 0.5f)
					{
						pushesPairs.Add(new KeyValuePair<int, Vector3>
							(vals, RoundVector3ToFraction(pus.Direction.ToVector())));
					}
				}
			}
			
			//all pushes calculated apply
			foreach (var pushPair in pushesPairs)
			{
				var actionToChange = _simGameStateData.PlayerTurns[turn].Actions.FindIndex(
					act => (act.CopyId == pushPair.Key) && (act.ActionPointsInitial == stepFraction));
				if (actionToChange >= 0)
				{
					var prevAction = _simGameStateData.PlayerTurns[turn].Actions[actionToChange];
					_simGameStateData.PlayerTurns[turn].Actions[actionToChange] = new MoveAction()
					{
						Direction = new Vector3Wrapper(pushPair.Value),
						Canceled = false,
						CopyId =  prevAction.CopyId,
						PlayerId =  prevAction.PlayerId,
						ActionPointsInitial = prevAction.ActionPointsInitial,
						ActionPointsPrice = prevAction.ActionPointsPrice
					};
				}
			}
			
			var motions = _simGameStateData.PlayerTurns[turn].Actions.FindAll(
				act => (act is MoveAction) && act.ActionPointsInitial == stepFraction);
			motions.ForEach(m => (m as MoveAction).Canceled = false);
			
			//calc target direction
			foreach (var mot in motions)
			{
				var move = mot as MoveAction;
				var wr = new Vector3Wrapper(_playerGenerations[move.CopyId].Position + move.Direction.ToVector());
				move.TargetPosition = wr;
			}

			//check duplicates and cancel
			foreach (var mot in motions)
			{
				var duplicates = motions.FindAll(mot1 => 
					((mot as MoveAction).TargetPosition.ToVector() - (mot1 as MoveAction).TargetPosition.ToVector()).magnitude < 0.05f);
				
				if (duplicates.Count > 1)
				{
					foreach (var dup in duplicates)
					{
						(dup as MoveAction).Canceled = true;
					}
				}
			}
			
			//calculate simulation according to events
			for (int i = step * ActionPointsPerMove; i < (step + 1) * ActionPointsPerMove; i++)
			{
				for (int j = 0; j <= _simGameStateData.CurrentCopyId; j++)
				{
					CalculateSimulationStep(i / ActionPointsPerTurn, i % ActionPointsPerTurn, j);
				}
			}
		}
		SerializeGameState(_localgameStateData, "temp");
	}
	
	private void CalculateSimulation(int stepsCount)
	{
		int simulateTurn = 0;
		int simulatePosition = 0;
		
		CalculateEvents();
		
		for (int i = 0; i < stepsCount; i++)
		{
			if (_simGameStateData.PlayerTurns.Count == simulateTurn)
			{
				return;
			}
			
			for (int j = 0; j <= _simGameStateData.CurrentCopyId; j++)
			{
				CalculateSimulationStep(simulateTurn, simulatePosition, j);
			}
			
			if (simulatePosition == ActionPointsPerTurn)
			{
				simulatePosition = 0;
				simulateTurn++;
			}
			
			simulatePosition++;
		}
	}

	private void SetArrowColor(GameObject arr, Color col)
	{
		arr.GetComponentsInChildren<Image>().ToList().ForEach(el => el.color = col);
	}
	private void CalculateSimulationStep(int turnNum, int position, int gen)
	{
		if (_simGameStateData.PlayerTurns.Count <= turnNum)
			return;
		
		var action = _simGameStateData.PlayerTurns[turnNum].Actions.Find(turn =>
			(turn.CopyId == gen) &&
			(((turn.ActionPointsPrice > 0) &&
			(turn.ActionPointsInitial <= position) &&
			(turn.ActionPointsInitial + turn.ActionPointsPrice) >= position)
			|| (turn.ActionPointsPrice == 0 && turn.ActionPointsInitial == position)));

		_playerGenerations[gen].Arrow.SetActive(false);
		_playerGenerations[gen].gameObject.SetActive(action != null);
		if (action is MoveAction)
		{
			var move = action as MoveAction;
			if (!move.Canceled)
			{
				float t = (float)(position - move.ActionPointsInitial + 1) / move.ActionPointsPrice;
				Vector3 from = move.TargetPosition.ToVector() - move.Direction.ToVector();
				var pos = Vector3.Lerp(from, move.TargetPosition.ToVector(), t);
				_playerGenerations[gen].Position = pos;
			}
		}
		else if(action is AttackAction)
		{
			_playerGenerations[gen].Arrow.SetActive(true);
			SetArrowColor(_playerGenerations[gen].Arrow, Color.red);
			_playerGenerations[gen].Arrow.transform.rotation = Quaternion.FromToRotation(Vector3.up, (action as AttackAction).Direction.ToVector().normalized);
		}
		else if (action is PushAction)
		{
			_playerGenerations[gen].Arrow.SetActive(true);
			SetArrowColor(_playerGenerations[gen].Arrow, Color.gray);
			_playerGenerations[gen].Arrow.transform.rotation = Quaternion.FromToRotation(Vector3.up, (action as PushAction).Direction.ToVector().normalized);
		}
		else if(action is SpawnAction)
		{
			_playerGenerations[gen].Position = (action as SpawnAction).From.ToVector();
		}

		if ((action == null) && _playerGenerations[gen].gameObject.activeSelf)
		{
			Debug.LogFormat("Playerdisabled Turn {0} Pos {1} Gen {2}", turnNum, position, gen);
		}
	}

	private void SerializeGameState(GameStateData data, string filename)
	{
		Type[] typeArray = {typeof(EmptyAction), typeof(SpawnAction), typeof(PlayerAction), typeof(MoveAction), 
			typeof(AttackAction), typeof(PlayerTurn), typeof(PushAction)};
			
		var f = File.CreateText(Application.streamingAssetsPath + "/" + filename);
		f.Write(SerializationUtils<GameStateData>.SerializeXml(data, typeArray));
		f.Flush();
		f.Close();
	}

	private GameStateData DeserializeGameState(string filename)
	{
		Type[] typeArray = {typeof(GameStateData), typeof(EmptyAction), typeof(SpawnAction), typeof(PlayerAction), typeof(MoveAction), 
			typeof(AttackAction), typeof(PlayerTurn), typeof(PushAction)};
			
		var f = File.OpenText(Application.streamingAssetsPath + "/" + filename);
		var result = SerializationUtils<GameStateData>.DeserializeXmlString(f.ReadToEnd(), typeArray);
		f.Close();
		return result;
	}
	
	public void Update()
	{
		if (_state == GameState.Play)
		{
			if (Mathf.RoundToInt(TimeSlider.maxValue) > _currentPlayPosition++)
			{
				TimeSlider.value = _currentPlayPosition;
			}
			else
			{
				_state = GameState.Move;
			}
		}

		if (Input.GetKeyDown(KeyCode.Space))
		{
			SerializeGameState(_localgameStateData, "debug");
		}
		
		if (Input.GetKeyDown(KeyCode.R))
		{
			_localgameStateData = DeserializeGameState("debug");
			foreach (var pl in _playerGenerations)
			{
				if (pl.Value.gameObject != null)
				{
					Destroy(pl.Value.gameObject);
				}
			}
			_playerGenerations.Clear();
			
			for (int i = 0; i <= _localgameStateData.CurrentCopyId;i++)
			{
				var pg = Instantiate(player, player.transform.parent);
				_playerGenerations[i] = pg;
				pg.gameObject.SetActive(true);
				pg.Position = Vector3.zero;
				pg.text.text = i.ToString();
			}

			_currentActionPoints = _currentActionPoints;
		
			UpdatePossibleActions();
		}
	}
}
