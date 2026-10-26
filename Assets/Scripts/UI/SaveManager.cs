using System.IO;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 进度持久化：最高分 / 已解锁关卡 / 每关最佳分，写入 Application.persistentDataPath/save.json。
/// </summary>
public static class SaveManager
{
    private const string FILE = "save.json";

    [System.Serializable]
    private class ProgressData
    {
        public int best = 0;
        public int maxUnlocked = 0;
        public List<int> levelBest = new List<int>();
        public int[] codex; // 每种花色收集数（按 faceIndex），图鉴用
        public SettingsData settings = new SettingsData();
    }

    /// <summary>
    /// 设置界面数据：音乐/音效/震动开关、画面质量档位、语言选择。
    /// </summary>
    [System.Serializable]
    public class SettingsData
    {
        public bool music = true;
        public bool sfx = true;
        public bool vibration = true;
        public int quality = 1;   // 0=低 1=中 2=高
        public int language = 0;  // 0=简体中文 1=English
    }

    public static SettingsData LoadSettings()
    {
        var d = Read();
        if (d.settings == null) d.settings = new SettingsData();
        return d.settings;
    }

    public static void SaveSettings(SettingsData s)
    {
        var d = Read();
        d.settings = s;
        Write(d);
    }

    private static ProgressData Read()
    {
        string p = Path.Combine(Application.persistentDataPath, FILE);
        if (!File.Exists(p)) return new ProgressData();
        try
        {
            var v = JsonUtility.FromJson<ProgressData>(File.ReadAllText(p));
            return v ?? new ProgressData();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("SaveManager.Read 失败: " + e.Message);
            return new ProgressData();
        }
    }

    private static void Write(ProgressData d)
    {
        string p = Path.Combine(Application.persistentDataPath, FILE);
        try { File.WriteAllText(p, JsonUtility.ToJson(d)); }
        catch (System.Exception e) { Debug.LogWarning("SaveManager.Write 失败: " + e.Message); }
    }

    // 兼容旧接口
    public static int LoadBest() { return Read().best; }
    public static void SaveBest(int best)
    {
        var d = Read();
        if (best > d.best) d.best = best;
        Write(d);
    }

    // 关卡进度
    public static int LoadMaxUnlocked() { return Read().maxUnlocked; }
    public static void UnlockLevel(int idx)
    {
        var d = Read();
        if (idx > d.maxUnlocked) d.maxUnlocked = idx;
        Write(d);
    }

    public static int LoadLevelBest(int idx)
    {
        var d = Read();
        return (d.levelBest != null && idx < d.levelBest.Count) ? d.levelBest[idx] : 0;
    }
    public static void SaveLevelBest(int idx, int score)
    {
        var d = Read();
        while (d.levelBest.Count <= idx) d.levelBest.Add(0);
        if (score > d.levelBest[idx]) d.levelBest[idx] = score;
        Write(d);
    }

    // 图鉴：每种花色收集数（按 faceIndex）
    public static int[] LoadCodex()
    {
        var d = Read();
        return (d.codex != null) ? d.codex : new int[0];
    }
    public static void SaveCodex(int[] c)
    {
        var d = Read();
        d.codex = c;
        Write(d);
    }
}
