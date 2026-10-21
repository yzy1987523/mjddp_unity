using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 无头（headless）冒烟测试：在 Unity 批处理模式下验证逻辑层与资源加载，
/// 不依赖渲染。运行：Unity -batchmode -executeMethod HeadlessTest.Run
/// </summary>
public static class HeadlessTest
{
    public static void Run()
    {
        Debug.Log("[HT] === start ===");

        // 1) 资源加载：Card/ 下是多精灵图集，必须用 LoadAll<Sprite>
        var cf = Resources.LoadAll<Sprite>("Card/card_front");
        Debug.Log("[HT] card_front sub-sprites=" + (cf != null ? cf.Length : 0));
        bool miss = false;
        for (int i = 1; i <= 36; i++)
        {
            var s = Resources.LoadAll<Sprite>("Card/icon_" + i.ToString("D3"));
            if (s == null || s.Length == 0) { Debug.LogError("[HT] MISSING icon_" + i); miss = true; }
        }
        Debug.Log("[HT] face sprites " + (miss ? "HAS MISSING" : "all 36 OK"));

        // 2) 布局解析 + 关卡生成
        var layout = Resources.Load<TextAsset>("mahjong-layout");
        if (layout == null) { Debug.LogError("[HT] MISSING mahjong-layout"); return; }
        var data = JsonUtility.FromJson<LayoutData>(layout.text);
        Debug.Log("[HT] layout tiles parsed=" + (data.tiles != null ? data.tiles.Length : 0));
        var gen = LevelGenerator.Generate(data);
        if (!gen.ok) { Debug.LogError("[HT] GENERATE FAILED: " + gen.error); return; }
        Debug.Log("[HT] generated tileCount=" + gen.tileCount + " pairs=" + gen.pairCount + " attempts=" + gen.attempt);

        // 3) 模拟消除：验证规则与可解性（贪心：每步取任意一对同面且都自由的牌）
        var board = new GameBoard();
        board.Setup(gen.geometry, gen.assignments);
        int steps = 0;
        while (!board.IsWin())
        {
            var freeByFace = new Dictionary<int, string>();
            string a = null, b = null;
            foreach (var id in board.PresentIds())
            {
                if (!board.IsFree(id)) continue;
                int f = board.GetFace(id);
                if (freeByFace.ContainsKey(f)) { a = freeByFace[f]; b = id; break; }
                freeByFace[f] = id;
            }
            if (a == null || b == null)
            {
                Debug.LogError("[HT] NO VALID MOVE at step " + steps + " remaining=" + board.Remaining() + " (贪心死局不一定是关卡不可解)");
                break;
            }
            board.Click(a);
            board.Click(b);
            steps++;
            if (steps > 100000) { Debug.LogError("[HT] too many steps"); break; }
        }
        if (board.IsWin()) Debug.Log("[HT] WIN in " + steps + " steps - 关卡可解/规则正常");
        else Debug.LogError("[HT] 未清空 remaining=" + board.Remaining());

        // 4) 死局检测逻辑健康性：构造一个明显死局（两张不同面且互相封锁）应触发 OnDeadlock 而非异常
        var dead = new GameBoard();
        var g = new Dictionary<string, TileGeo>();
        g["x1"] = new TileGeo(); g["x2"] = new TileGeo();
        g["x1"].right.Add("x2"); g["x2"].left.Add("x1");
        var fc = new Dictionary<string, int> { { "x1", 1 }, { "x2", 2 } };
        dead.Setup(g, fc);
        Debug.Log("[HT] deadlock-has-valid-move=" + dead.HasValidMove() + " (应为 False)");

        // 5) 模拟 GameController 启动：验证 BuildUI/LoadResources/BuildBoard 不抛异常（覆盖 UI 构建层）
        try
        {
            var go = new GameObject("TestGC");
            var gc = go.AddComponent<GameController>();
            var start = typeof(GameController).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance);
            start.Invoke(gc, null);
            Debug.Log("[HT] GameController.Start 执行无异常（BuildUI/LoadResources/BuildBoard 通过）");
            UnityEngine.Object.DestroyImmediate(go);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[HT] GameController.Start threw: " + e);
        }

        Debug.Log("[HT] === done ===");
    }
}
