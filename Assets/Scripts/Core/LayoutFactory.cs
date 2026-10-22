using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 规则居中塔布局生成：每层比下层缩 1 列/行并居中，形成经典麻将塔。
/// 与现有 mahjong-layout 的“缩进塔”同构，LevelGenerator.FindPath 可解。
/// </summary>
public static class LayoutFactory
{
    public static LayoutData MakeTower(int cols, int rows, int layers)
    {
        var tiles = new List<TileDef>();
        int L = Mathf.Max(1, layers);
        for (int l = 0; l < L; l++)
        {
            int c = cols - 2 * l;
            int r = rows - 2 * l;
            if (c <= 0 || r <= 0) break;
            int startX = l * 2;
            int startY = l * 2;
            int idx = 0;
            for (int j = 0; j < r; j++)
                for (int i = 0; i < c; i++)
                {
                    tiles.Add(new TileDef
                    {
                        id = "L" + l + "_T" + (idx++),
                        layer = l,
                        x = startX + i * 2,
                        y = startY + j * 2,
                        width = 2,
                        height = 2
                    });
                }
        }
        return new LayoutData { tiles = tiles.ToArray() };
    }
}
