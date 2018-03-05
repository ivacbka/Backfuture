using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using UnityEngine;
using UnityEngine.UI;

public enum WallType
{
	Window,
	Wall
}

public enum CellType
{
	Wall,
	Spawn1,
	Spawn2
}

[Serializable]
public class Wall
{
	public WallType WallType;
	public int PositionX;
	public int PositionY;
	public int PositionX1;
	public int PositionY1;
	[XmlIgnore]
	public GameObject View;
}

[Serializable]
public class IntVector
{
	public int Y;
	public int X;

	public IntVector()
	{
	}
	
	public IntVector(int x, int y)
	{
		X = x;
		Y = y;
	}
}

[Serializable]
public class CellElement
{
	public IntVector Position = new IntVector(0, 0);
	public CellType CellType;
	[XmlIgnore]
	public GameObject View;
}

[Serializable]
public class LevelData
{
	public List<CellElement> CellElements = new List<CellElement>();
	public List<Wall> Walls = new List<Wall>();
}

public class MapEditor : MonoBehaviour
{
	public float MapSizeX = 1;
	public float MapSizeY = 1;
	public float CellSize = 10;
	public float WallWidth = 0.1f;
	public float WindowWidth = 0.1f;
	public Image BG;
	public Image Obstacle;
	public Image Wall;
	public Image Window;
	public Image SpawnPoint1;
	public Image SpawnPoint2;
	private LevelData _levelData = new LevelData();
	
	private MapClickListener _listener;
	
	private enum EditMode
	{
		Obstacle,
		Wall,
		Window,
		SpawnPoint1,
		SpawnPoint2,
	}

	private EditMode _mode;

	private void SerializeMap(LevelData data, string filename)
	{
		Type[] typeArray = {typeof(LevelData), typeof(CellElement), typeof(IntVector), typeof(Wall)};
		var f = File.CreateText(Application.streamingAssetsPath + "/" + filename);
		f.Write(SerializationUtils<LevelData>.SerializeXml(data, typeArray));
		f.Flush();
		f.Close();
	}

	private LevelData DeserializeMap(string filename)
	{
		Type[] typeArray = {typeof(LevelData), typeof(CellElement), typeof(IntVector), typeof(Wall)};	
		var f = File.OpenText(Application.streamingAssetsPath + "/" + filename);
		var result = SerializationUtils<LevelData>.DeserializeXmlString(f.ReadToEnd(), typeArray);
		f.Close();
		return result;
	}
	
	void Start()
	{
		_listener = BG.GetComponent<MapClickListener>();
		_listener.OnCellClicked += CellClicked;
		_listener.OnEdgeClicked += EdgeClicked;
	}

	void EdgeClicked(int x, int y, int x1, int y1)
	{
		var obs = _levelData.Walls.Find((obst) => obst.PositionX == x && obst.PositionY == y && obst.PositionX1 == x1 && obst.PositionY1 == y1);
		if (obs != null)
		{
			Destroy(obs.View);
			_levelData.Walls.Remove(obs);
		}
		else
		{
			var newObstacle = new Wall()
			{
				PositionX = x, 
				PositionY = y, 
				PositionX1 = x1, 
				PositionY1 = y1,
				WallType = _mode == EditMode.Wall ? WallType.Wall : WallType.Window,
				View = new GameObject("WallView" + x + "_" + y)
			};
			
			var image = newObstacle.View.AddComponent<Image>();
			if (newObstacle.WallType == WallType.Wall)
			{
				image.color = Color.gray;
			}
			else if (newObstacle.WallType == WallType.Window)
			{
				image.color = Color.cyan;
			}
			image.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, x == x1 ? CellSize * WallWidth : CellSize);
			image.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, y == y1 ? CellSize * WallWidth : CellSize);
			image.rectTransform.SetParent(BG.transform, false);
			image.rectTransform.anchorMin = Vector2.zero;
			image.rectTransform.anchorMax = Vector2.zero;
			image.rectTransform.anchoredPosition = new Vector2((x + x1) * CellSize / 2, (y + y1) * CellSize / 2);
			_levelData.Walls.Add(newObstacle);
		}
	}
	
	void CellClicked(int x, int y)
	{
		var obs = _levelData.CellElements.Find((obst) => obst.Position.X == x && obst.Position.Y == y);
		if (obs != null)
		{
			Destroy(obs.View);
			_levelData.CellElements.Remove(obs);
			return;
		}
		
		if (_mode == EditMode.Obstacle)
		{
			var newObstacle = new CellElement()
			{
				Position = new IntVector(x, y), 
				CellType = CellType.Wall, 
				View = new GameObject("ObsView" + x + "_" + y)
			};
			
			var image = newObstacle.View.AddComponent<Image>();
			image.color = Color.gray;
			image.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, CellSize);
			image.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, CellSize);
			image.rectTransform.SetParent(BG.transform, false);
			image.rectTransform.anchorMin = Vector2.zero;
			image.rectTransform.anchorMax = Vector2.zero;
			image.rectTransform.pivot = Vector2.zero;
			image.rectTransform.anchoredPosition = new Vector2(x * CellSize, y * CellSize);
			_levelData.CellElements.Add(newObstacle);
		}
		else if (_mode == EditMode.SpawnPoint1 || _mode == EditMode.SpawnPoint2)
		{
			var prevSpawn = _levelData.CellElements.Find(cell =>
				cell.CellType == (_mode == EditMode.SpawnPoint1 ? CellType.Spawn1 : CellType.Spawn2));
			
			if (prevSpawn != null)
			{
				Destroy(prevSpawn.View);
				_levelData.CellElements.Remove(prevSpawn);
			}
			
			var newObstacle = new CellElement()
			{
				Position = new IntVector(x, y), 
				CellType = _mode == EditMode.SpawnPoint1 ? CellType.Spawn1 : CellType.Spawn2, 
				View = new GameObject("ObsView" + x + "_" + y)
			};
			
			var image = newObstacle.View.AddComponent<Image>();
			image.color = _mode == EditMode.SpawnPoint1 ? Color.blue : Color.red;
			image.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, CellSize);
			image.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, CellSize);
			image.rectTransform.SetParent(BG.transform, false);
			image.rectTransform.anchorMin = Vector2.zero;
			image.rectTransform.anchorMax = Vector2.zero;
			image.rectTransform.pivot = Vector2.zero;
			image.rectTransform.anchoredPosition = new Vector2(x * CellSize, y * CellSize);
			_levelData.CellElements.Add(newObstacle);
		}
	}
	
	void Update ()
	{
		BG.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, MapSizeX * CellSize);
		BG.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, MapSizeY * CellSize);
		BG.material.SetVector("_CellsCount", new Vector4(MapSizeX, MapSizeY));
	}

	private void UnselectAll()
	{
		Obstacle.color = Color.white;
		Wall.color = Color.white;
		Window.color = Color.white;
		SpawnPoint1.color = Color.white;
		SpawnPoint2.color = Color.white;
	}
	
	public void OnWallClicked()
	{
		UnselectAll();
		Wall.color = Color.green;
		_mode = EditMode.Wall;
		_listener.SetSnap(MapClickListener.SnapStyle.Edge);
	}
	
	public void OnWindowClicked()
	{
		UnselectAll();
		Window.color = Color.green;
		_mode = EditMode.Window;
		_listener.SetSnap(MapClickListener.SnapStyle.Edge);
	}
	
	public void OnObstacleClicked()
	{
		UnselectAll();
		Obstacle.color = Color.green;
		_mode = EditMode.Obstacle;
		_listener.SetSnap(MapClickListener.SnapStyle.Cells);
	}
	
	public void OnSpawnPoint1Clicked()
	{
		UnselectAll();
		SpawnPoint1.color = Color.green;
		_mode = EditMode.SpawnPoint1;
		_listener.SetSnap(MapClickListener.SnapStyle.Cells);
	}
	
	public void OnSpawnPoint2Clicked()
	{
		UnselectAll();
		SpawnPoint2.color = Color.green;
		_mode = EditMode.SpawnPoint2;
		_listener.SetSnap(MapClickListener.SnapStyle.Cells);
	}

	public void OnSaveClicked()
	{
		SerializeMap(_levelData, "map");
	}
	
	public void OnLoadClicked()
	{
		foreach (var cell in _levelData.CellElements)
		{
			Destroy(cell.View);
		}
		
		foreach (var wall in _levelData.Walls)
		{
			Destroy(wall.View);
		}
		
		_levelData = DeserializeMap("map");
		
		foreach (var wall in _levelData.Walls)
		{
			wall.View = new GameObject("WallView" + wall.PositionX + "_" + wall.PositionY);
			
			var image = wall.View.AddComponent<Image>();
			if (wall.WallType == WallType.Wall)
			{
				image.color = Color.gray;
			}
			else if (wall.WallType == WallType.Window)
			{
				image.color = Color.cyan;
			}
			
			image.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, wall.PositionX == wall.PositionX1 ? CellSize * WallWidth : CellSize);
			image.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, wall.PositionY == wall.PositionY1 ? CellSize * WallWidth : CellSize);
			image.rectTransform.SetParent(BG.transform, false);
			image.rectTransform.anchorMin = Vector2.zero;
			image.rectTransform.anchorMax = Vector2.zero;
			image.rectTransform.anchoredPosition = new Vector2((wall.PositionX + wall.PositionX1) * CellSize / 2, (wall.PositionY + wall.PositionY1) * CellSize / 2);
		}
		
		foreach (var cell in _levelData.CellElements)
		{
			cell.View = new GameObject("ObsView" + cell.Position.X + "_" + cell.Position.Y);
			
			var image = cell.View.AddComponent<Image>();
			Color col = Color.black;
			switch (cell.CellType)
			{
				case CellType.Spawn1:col = Color.blue;break;
				case CellType.Spawn2:col = Color.red;break;
				case CellType.Wall:col = Color.gray;break;
			}
			
			image.color = col;
			image.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, CellSize);
			image.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, CellSize);
			image.rectTransform.SetParent(BG.transform, false);
			image.rectTransform.anchorMin = Vector2.zero;
			image.rectTransform.anchorMax = Vector2.zero;
			image.rectTransform.pivot = Vector2.zero;
			image.rectTransform.anchoredPosition = new Vector2(cell.Position.X * CellSize, cell.Position.Y * CellSize);
		}
	}
}
