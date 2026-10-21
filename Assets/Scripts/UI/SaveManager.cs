using System.IO;
using UnityEngine;

/// <summary>
/// 进度持久化：最高分写入 Application.persistentDataPath/save.json。
/// </summary>
public static class SaveManager
{
    private const string FILE = "save.json";

    [System.Serializable]
    private class BestData { public int best; }

    public static int LoadBest()
    {
        string p = Path.Combine(Application.persistentDataPath, FILE);
        if (!File.Exists(p)) return 0;
        try
        {
            var txt = File.ReadAllText(p);
            var v = JsonUtility.FromJson<BestData>(txt);
            return v != null ? v.best : 0;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("SaveManager.LoadBest 失败: " + e.Message);
            return 0;
        }
    }

    public static void SaveBest(int best)
    {
        string p = Path.Combine(Application.persistentDataPath, FILE);
        try
        {
            File.WriteAllText(p, JsonUtility.ToJson(new BestData { best = best }));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("SaveManager.SaveBest 失败: " + e.Message);
        }
    }
}
