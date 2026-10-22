using System;

// 单关卡文件（Resources/level/levelN.json）：
// - 若提供 tiles 则直接作为布局（多布局文件，真实几何）；
// - 否则用 cols/rows/layers 由 LayoutFactory 生成占位塔布局。
[Serializable]
public class LevelFile
{
    public string name;        // 显示名（空则用 "第N关"）
    public int targetScore;    // 通关目标分（展示用）
    public int seed;           // 牌面分配随机种子（0 亦可，确定性）
    public int cols;           // 生成塔底层列数（tiles 为空时生效）
    public int rows;           // 生成塔底层行数
    public int layers;         // 生成塔层数
    public TileDef[] tiles;    // 可选：直接布局
}
