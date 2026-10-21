using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 麻将对对碰核心规则（Godot game_board.gd 的 C# 移植）。
/// 自由牌 = 上方无牌压住，且左右两侧不同时被占（至少一侧开放）。
/// </summary>
public class GameBoard
{
    private Dictionary<string, TileGeo> _geo = new Dictionary<string, TileGeo>();
    private Dictionary<string, int> _faces = new Dictionary<string, int>();
    private Dictionary<string, bool> _present = new Dictionary<string, bool>();
    private string _selected = "";

    public event Action<string, bool> OnSelectionChanged;
    public event Action<string, string> OnMatched;
    public event Action<string> OnBlocked;
    public event Action OnWin;
    public event Action OnDeadlock;

    public void Setup(Dictionary<string, TileGeo> geometry, Dictionary<string, int> faces)
    {
        _geo = geometry;
        _faces = new Dictionary<string, int>(faces);
        _present = new Dictionary<string, bool>();
        foreach (var id in faces.Keys) _present[id] = true;
        _selected = "";
    }

    public bool IsFree(string id)
    {
        if (!_present.ContainsKey(id) || !_present[id]) return false;
        var g = _geo[id];
        foreach (var a in g.above) if (_present[a]) return false;
        bool lb = false, rb = false;
        foreach (var l in g.left) if (_present[l]) { lb = true; break; }
        foreach (var r in g.right) if (_present[r]) { rb = true; break; }
        return !(lb && rb);
    }

    public void Click(string id)
    {
        if (!_present.ContainsKey(id) || !_present[id]) return;
        if (!IsFree(id)) { OnBlocked?.Invoke(id); return; }

        if (_selected == "")
        {
            _selected = id;
            OnSelectionChanged?.Invoke(id, true);
            return;
        }
        if (_selected == id)
        {
            _selected = "";
            OnSelectionChanged?.Invoke(id, false);
            return;
        }
        if (_faces[_selected] == _faces[id])
        {
            string a = _selected;
            _selected = "";
            OnSelectionChanged?.Invoke(a, false);
            _present[a] = false;
            _present[id] = false;
            OnMatched?.Invoke(a, id);
            if (IsWin()) OnWin?.Invoke();
            else if (!HasValidMove()) OnDeadlock?.Invoke();
        }
        else
        {
            OnSelectionChanged?.Invoke(_selected, false);
            _selected = id;
            OnSelectionChanged?.Invoke(id, true);
        }
    }

    public bool IsWin()
    {
        foreach (var kv in _present) if (kv.Value) return false;
        return true;
    }

    public bool HasValidMove()
    {
        var seen = new HashSet<int>();
        foreach (var id in _present.Keys)
        {
            if (!_present[id]) continue;
            if (!IsFree(id)) continue;
            int f = _faces[id];
            if (seen.Contains(f)) return true;
            seen.Add(f);
        }
        return false;
    }

    public int Remaining()
    {
        int n = 0;
        foreach (var kv in _present) if (kv.Value) n++;
        return n;
    }

    public List<string> PresentIds()
    {
        var r = new List<string>();
        foreach (var id in _present.Keys) if (_present[id]) r.Add(id);
        return r;
    }

    public int GetFace(string id) => _faces.ContainsKey(id) ? _faces[id] : -1;
    public void SetFace(string id, int f) { if (_faces.ContainsKey(id)) _faces[id] = f; }
    public void ClearSelection()
    {
        if (_selected != "")
        {
            string s = _selected;
            _selected = "";
            OnSelectionChanged?.Invoke(s, false);
        }
    }
}
