using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 按骨架 JSON 生成有牌布局（Godot lv_fill.gd 的 C# 移植）。
/// 算法：先只依赖几何用 DFS 搜一条「每步两张牌同时自由」的合法消除路径，
/// 再把牌面种类按路径配对序分配下去，天然保证可解。
/// </summary>
public static class LevelGenerator
{
    const int DEFAULT_SEARCH_BUDGET = 500000;
    const int MAX_ATTEMPTS = 16;
    const int DEFAULT_MAX_FACE_TYPES = 36; // 默认牌面种类上限（受限于 art/card/ 下的实际牌面图数量）

    private class PathResult
    {
        public bool ok;
        public string error = "";
        public string code = "";
    }

    // 工作态（单线程，每次 Generate 重新初始化）。
    private static System.Random _rng;
    private static List<TileDef> _tiles;
    private static int _n;
    private static List<int>[] _above;
    private static List<int>[] _left;
    private static List<int>[] _right;
    private static bool[] _present;
    private static List<int[]> _path;
    private static int _visited;
    private static int _budget;
    private static bool _budgetHit;
    private static HashSet<string> _failed;

    public static GenResult Generate(LayoutData data, int seedValue = -1, int searchBudget = DEFAULT_SEARCH_BUDGET, int maxFaceTypes = DEFAULT_MAX_FACE_TYPES)
    {
        _rng = (seedValue >= 0) ? new System.Random(seedValue) : new System.Random();
        _tiles = new List<TileDef>(data.tiles);
        var v = Validate(_tiles);
        if (!v.ok) return new GenResult { ok = false, error = v.error };

        _n = _tiles.Count;
        _budget = searchBudget;

        var geo = BuildGeometry(_tiles);
        _above = geo.above;
        _left = geo.left;
        _right = geo.right;

        GenResult lastErr = null;
        for (int attempt = 1; attempt <= MAX_ATTEMPTS; attempt++)
        {
            if (seedValue >= 0) _rng = new System.Random((seedValue + attempt * 1000003) & 0x7fffffff);
            else _rng = new System.Random();

            PathResult res = FindPath();
            if (!res.ok)
            {
                lastErr = new GenResult { ok = false, error = res.error };
                if (res.code == "SEARCH_LIMIT_REACHED") continue;
                break;
            }

            int pairCount = _n / 2;
            List<int> typeCounts = ChooseTypeCounts(pairCount, maxFaceTypes);
            var pairTypes = new List<int>();
            for (int ti = 0; ti < typeCounts.Count; ti++)
                for (int k = 0; k < typeCounts[ti]; k++) pairTypes.Add(ti);
            Shuffle(pairTypes);

            var assignments = new Dictionary<string, int>();
            for (int k = 0; k < _path.Count; k++)
            {
                int face = pairTypes[k];
                int a = _path[k][0];
                int b = _path[k][1];
                assignments[_tiles[a].id] = face;
                assignments[_tiles[b].id] = face;
            }

            PathResult vr = ValidatePath(assignments);
            if (!vr.ok) { lastErr = new GenResult { ok = false, error = vr.error }; continue; }

            var outTiles = new List<TileOut>();
            foreach (var t in _tiles)
                outTiles.Add(new TileOut { id = t.id, layer = t.layer, x = t.x, y = t.y, width = t.width, height = t.height, tileType = assignments[t.id] });

            var faceCounts = new Dictionary<int, int>();
            foreach (var val in assignments.Values)
            {
                if (!faceCounts.ContainsKey(val)) faceCounts[val] = 0;
                faceCounts[val]++;
            }

            return new GenResult
            {
                ok = true,
                attempt = attempt,
                tileCount = _n,
                pairCount = pairCount,
                tiles = outTiles,
                assignments = assignments,
                geometry = GeometryById(),
                faceCounts = faceCounts
            };
        }

        return lastErr ?? new GenResult { ok = false, error = "多次尝试仍未能生成有效牌局" };
    }

    // ---------- 几何 ----------

    private static (List<int>[] above, List<int>[] left, List<int>[] right) BuildGeometry(List<TileDef> tiles)
    {
        int n = tiles.Count;
        var above = new List<int>[n];
        var left = new List<int>[n];
        var right = new List<int>[n];
        for (int i = 0; i < n; i++) { above[i] = new List<int>(); left[i] = new List<int>(); right[i] = new List<int>(); }

        for (int i = 0; i < n; i++)
        {
            TileDef a = tiles[i];
            for (int j = 0; j < n; j++)
            {
                if (i == j) continue;
                TileDef b = tiles[j];
                if (b.layer > a.layer && Intersects(a, b)) above[i].Add(j);
                if (b.layer == a.layer && VOverlap(a, b))
                {
                    if (b.x + b.width == a.x) left[i].Add(j);
                    if (a.x + a.width == b.x) right[i].Add(j);
                }
            }
        }
        return (above, left, right);
    }

    private static bool Intersects(TileDef a, TileDef b)
    {
        return a.x < b.x + b.width && a.x + a.width > b.x && a.y < b.y + b.height && a.y + a.height > b.y;
    }

    private static bool VOverlap(TileDef a, TileDef b)
    {
        return a.y < b.y + b.height && a.y + a.height > b.y;
    }

    private static bool IsFreeIdx(int i, bool[] arr)
    {
        for (int j = 0; j < _above[i].Count; j++) if (arr[_above[i][j]]) return false;
        bool lb = false, rb = false;
        for (int j = 0; j < _left[i].Count; j++) if (arr[_left[i][j]]) { lb = true; break; }
        for (int j = 0; j < _right[i].Count; j++) if (arr[_right[i][j]]) { rb = true; break; }
        return !(lb && rb);
    }

    // ---------- 搜索一条可解消除路径 ----------

    private static PathResult FindPath()
    {
        _present = new bool[_n];
        for (int i = 0; i < _n; i++) _present[i] = true;
        _path = new List<int[]>();
        _visited = 0;
        _budgetHit = false;
        _failed = new HashSet<string>();

        if (Dfs(_n)) return new PathResult { ok = true };
        if (_budgetHit) return new PathResult { ok = false, error = "搜索预算耗尽", code = "SEARCH_LIMIT_REACHED" };
        return new PathResult { ok = false, error = "骨架几何规则下不可解", code = "UNSOLVABLE_SKELETON" };
    }

    private static bool Dfs(int remaining)
    {
        if (remaining == 0) return true;
        _visited++;
        if (_visited > _budget) { _budgetHit = true; return false; }
        string key = StateKey(_present);
        if (_failed.Contains(key)) return false;

        var free = new List<int>();
        for (int i = 0; i < _n; i++) if (_present[i] && IsFreeIdx(i, _present)) free.Add(i);
        if (free.Count < 2) { _failed.Add(key); return false; }

        var ranked = new List<(int i, int urgency, float score)>();
        foreach (var i in free)
        {
            bool lb = false, rb = false;
            for (int j = 0; j < _left[i].Count; j++) if (_present[_left[i][j]]) { lb = true; break; }
            for (int j = 0; j < _right[i].Count; j++) if (_present[_right[i][j]]) { rb = true; break; }
            int urgency = (lb ? 1 : 0) + (rb ? 1 : 0);
            float score = urgency + ((float)_rng.NextDouble() - 0.5f) * 0.25f;
            ranked.Add((i, urgency, score));
        }
        ranked.Sort((p, q) => q.score.CompareTo(p.score));

        var pairs = new List<(int i, int j, float reward, float tie)>();
        for (int a = 0; a < ranked.Count; a++)
            for (int b = a + 1; b < ranked.Count; b++)
            {
                float reward = ranked[a].urgency + ranked[b].urgency;
                float tie = (float)_rng.NextDouble();
                pairs.Add((ranked[a].i, ranked[b].i, reward, tie));
            }
        pairs.Sort((p, q) => q.reward.CompareTo(p.reward) != 0 ? q.reward.CompareTo(p.reward) : q.tie.CompareTo(p.tie));

        foreach (var p in pairs)
        {
            int i = p.i, j = p.j;
            _present[i] = false;
            _present[j] = false;
            _path.Add(new int[] { i, j });
            if (Dfs(remaining - 2)) return true;
            _path.RemoveAt(_path.Count - 1);
            _present[i] = true;
            _present[j] = true;
            if (_budgetHit) return false;
        }
        _failed.Add(key);
        return false;
    }

    private static string StateKey(bool[] arr)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < arr.Length; i++) sb.Append(arr[i] ? '1' : '0');
        return sb.ToString();
    }

    // ---------- 校验 ----------

    private static PathResult Validate(List<TileDef> tiles)
    {
        if (tiles.Count == 0) return new PathResult { ok = false, error = "tiles 为空" };
        if (tiles.Count % 2 != 0) return new PathResult { ok = false, error = "牌位数为奇数" };
        var ids = new HashSet<string>();
        foreach (var t in tiles)
        {
            if (string.IsNullOrEmpty(t.id)) return new PathResult { ok = false, error = "牌位缺少 id" };
            if (t.width <= 0 || t.height <= 0) return new PathResult { ok = false, error = "牌位宽高无效" };
            if (ids.Contains(t.id)) return new PathResult { ok = false, error = "牌位 ID 重复: " + t.id };
            ids.Add(t.id);
        }
        for (int i = 0; i < tiles.Count; i++)
            for (int j = i + 1; j < tiles.Count; j++)
            {
                var a = tiles[i];
                var b = tiles[j];
                if (a.layer == b.layer && Intersects(a, b))
                    return new PathResult { ok = false, error = "同层面积重叠: " + a.id + " 与 " + b.id };
            }
        return new PathResult { ok = true };
    }

    private static PathResult ValidatePath(Dictionary<string, int> assignments)
    {
        var present = new bool[_n];
        for (int i = 0; i < _n; i++) present[i] = true;
        for (int step = 0; step < _path.Count; step++)
        {
            int i = _path[step][0];
            int j = _path[step][1];
            if (i == j || !present[i] || !present[j]) return new PathResult { ok = false, error = "引用无效或已移除的牌位" };
            if (!IsFreeIdx(i, present) || !IsFreeIdx(j, present)) return new PathResult { ok = false, error = "存在被遮挡或两侧均封闭的牌" };
            if (assignments[_tiles[i].id] != assignments[_tiles[j].id]) return new PathResult { ok = false, error = "牌面不同" };
            present[i] = false;
            present[j] = false;
        }
        for (int i = 0; i < _n; i++) if (present[i]) return new PathResult { ok = false, error = "消除路径结束后仍有牌位未消" };
        return new PathResult { ok = true };
    }

    private static List<int> ChooseTypeCounts(int pairCount, int maxFaceTypes)
    {
        int maxKinds = Math.Min(pairCount, maxFaceTypes);
        int kinds = _rng.Next(1, maxKinds + 1); // [1, maxKinds]
        var counts = new List<int>();
        for (int i = 0; i < kinds; i++) counts.Add(1);
        int extra = pairCount - kinds;
        while (extra > 0) { counts[_rng.Next(kinds)]++; extra--; }
        Shuffle(counts);
        return counts;
    }

    private static void Shuffle<T>(List<T> arr)
    {
        for (int i = arr.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            T t = arr[i];
            arr[i] = arr[j];
            arr[j] = t;
        }
    }

    private static Dictionary<string, TileGeo> GeometryById()
    {
        var idxToId = new List<string>();
        foreach (var t in _tiles) idxToId.Add(t.id);
        var outGeo = new Dictionary<string, TileGeo>();
        for (int i = 0; i < _n; i++)
        {
            outGeo[idxToId[i]] = new TileGeo
            {
                above = MapIds(_above[i], idxToId),
                left = MapIds(_left[i], idxToId),
                right = MapIds(_right[i], idxToId)
            };
        }
        return outGeo;
    }

    private static List<string> MapIds(List<int> idxs, List<string> idxToId)
    {
        var r = new List<string>();
        foreach (var i in idxs) r.Add(idxToId[i]);
        return r;
    }
}
