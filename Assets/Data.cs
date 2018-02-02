using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

[Serializable]
public class GameStateData
{
	public List<PlayerTurn> PlayerTurns = new List<PlayerTurn>();
	public int CurrentTurn = -1;
	public int CurrentTopTurn = 0;
	public int CurrentPlayerId;
	public int CurrentCopyId;
	public int CurrentActionPointsLeft;
	public Vector3Wrapper StartPosition;
	public int CurrentFlashbacksCount = 0;
}

[Serializable]
public class PlayerTurn
{
	public List<PlayerAction> Actions = new List<PlayerAction>();
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
	//[NonSerialized]
	public bool CanMove = true;
	[NonSerialized]
	public Vector3Wrapper TargetPosition;
	//[NonSerialized]
	public bool Canceled = false;
}
	
[Serializable]
public class AttackAction : PlayerAction
{
	public Vector3Wrapper Direction;
}

[Serializable]
public class PushAction : PlayerAction
{
	public Vector3Wrapper Direction;
}