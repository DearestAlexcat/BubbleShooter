using UnityEngine;
using System.IO;
using System.Collections.Generic;
using TMPro;

[System.Serializable]
class GameDataSerialize
{
    public GameData[] gameDatas;
}

[System.Serializable]
public class GameData
{
    public int id = -1;
    public int ballType = -1;
   
    public GameData(BallController ballController)
    {
        id = ballController.id;
        ballType = ballController.ballType;
    }

    public static implicit operator GameData(BallController ballController)
    {
        return new GameData(ballController);
    }
}

[System.Serializable]
public class PlayerDataSerialize
{
    public DataScores[] dataScores;
}

public enum FileType
{
    data,      // lead, next, balls
    progress   // Таблица
}

public class DataController
{
    GameEvents gameEvents;

    string[] fileName = { "data.json", "progress.json" };

    // persistentDataPath
    public DataController() { }

    public DataController(GameEvents gameEvents)
    {
        this.gameEvents = gameEvents;
    }

    public void SaveData<T>(T data, FileType fileType)
    {
        string filePath = Path.Combine(Application.persistentDataPath, fileName[(int)fileType]);
        string dataAsJson = JsonUtility.ToJson(data);
        File.WriteAllText(filePath, dataAsJson);
    }

    public T LoadData<T>(FileType fileType) where T : new()
    {
        T data = default;

        string filePath = Path.Combine(Application.persistentDataPath, fileName[(int)fileType]);

        if (File.Exists(filePath))
        {
            string dataAsJson = File.ReadAllText(filePath);
            data = JsonUtility.FromJson<T>(dataAsJson);
        }

        // Чтобы избежать boxing, лучший способ сравнения обобщений на равенство -это EqualityComparer<T>.Default.
        // Это учитывает IEquatable<T>(без boxing), а также object.Equals и обрабатывает все Nullable<T>.
        
        //Это будет соответствовать:
        // null for classes
        // null(empty) for Nullable<T>
        // zero / false / etc for other structs

        // Default - Возвращает средство сравнения равенства по умолчанию для типа, указанного в универсальном аргументе.

        if (EqualityComparer<T>.Default.Equals(data, default))
        {
            data = new T();
        }

        return data;
    }

    public void LoadPlayerProgress()
    {
        if (PlayerPrefs.HasKey("score"))
        {
            gameEvents.score = PlayerPrefs.GetFloat("score");
        }
        else
        {
            gameEvents.score = 0;
        }

        if (PlayerPrefs.HasKey("round"))
        {
            gameEvents.round = PlayerPrefs.GetInt("round");
        }
        else
        {
            gameEvents.round = 1;
        }
    }

    public void SavePlayerProgress()
    {
        PlayerPrefs.SetFloat("score", gameEvents.score);
        PlayerPrefs.SetInt("round", gameEvents.round);
    }
}
