using System.Collections.Generic;

// 布局骨架里的一张牌位（来自 mahjong-layout.json）。
[System.Serializable]
public class TileDef
{
    public string id;
    public int layer;
    public int x;
    public int y;
    public int width;
    public int height;
}

[System.Serializable]
public class LayoutData
{
    public TileDef[] tiles;
}

// 来自 ui_config.json。
[System.Serializable]
public class ConfigData
{
    public bool fit_to_screen = true;
    public float padding = 40f;
    public float zoom = 1f;
    public int tile_width = 56;
    public int tile_height = 96;
    public int face_width = 40;
    public int face_height = 64;
    public float shadow_x = 5f;
    public float shadow_y = 7f;
    public float layer_dx = 0f;
    public float layer_dy = 0f;
}

// 生成后的一张牌（带牌面种类）。
[System.Serializable]
public class TileOut
{
    public string id;
    public int layer;
    public int x;
    public int y;
    public int width;
    public int height;
    public int tileType;
}

// 交互层判定「自由牌」所需的几何关系（id 列表）。
public class TileGeo
{
    public List<string> above = new List<string>();
    public List<string> left = new List<string>();
    public List<string> right = new List<string>();
}

// 关卡生成结果（仅在内存中使用，无需可序列化）。
public class GenResult
{
    public bool ok;
    public string error;
    public int attempt;
    public int tileCount;
    public int pairCount;
    public List<TileOut> tiles;
    public Dictionary<string, int> assignments;
    public Dictionary<string, TileGeo> geometry;
    public Dictionary<int, int> faceCounts;
}
