#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// 一键把牌面资源接入 Addressables：
/// 1) 把 Assets/Resources/Card 移出 Resources 到 Assets/Art/Card
/// 2) 删除根目录遗留的旧 icon_001..036
/// 3) 把所有牌面 png 重命名为 card_000..card_143（含 card_front）
/// 4) 创建 SpriteAtlas（CardAtlas，includeInBuild=false），把全部牌面加进去作为合图容器
/// 5) 标记 Addressable：图集 + 每张 sprite（address=card_000..card_143 / card_front）
///    运行时按 sprite address 加载，sprite 自动解析到图集内的合图（draw call 受益），不再依赖图集按名取图。
/// 运行前请先：Package Manager 安装 Addressables，并打开一次 Window/Asset Management/Addressables/Groups 初始化。
/// 标记完成后，再执行 Tools/Build Addressables 构建资源。
/// </summary>
public static class CardAddressableSetup
{
    const string SRC = "Assets/Resources/Card";
    const string DST = "Assets/Art/Card";
    const string ATLAS_PATH = "Assets/Art/Card/CardAtlas.spriteatlas";
    const string ATLAS_ADDRESS = "CardAtlas";
    const string LABEL = "CardFace";

    [MenuItem("Tools/Setup Card Addressables")]
    public static void Setup()
    {
        // 1. 移出 Resources
        if (AssetDatabase.IsValidFolder(SRC))
        {
            if (AssetDatabase.IsValidFolder(DST))
            {
                if (AssetDatabase.FindAssets("", new[] { DST }).Length == 0)
                    AssetDatabase.DeleteAsset(DST);
            }
            if (!AssetDatabase.IsValidFolder(DST))
            {
                AssetDatabase.MoveAsset(SRC, DST);
                AssetDatabase.Refresh();
                Debug.Log("[Addressables] 已移动 " + SRC + " -> " + DST);
            }
            else
            {
                MergeFolder(SRC, DST);
                AssetDatabase.Refresh();
                Debug.Log("[Addressables] 已合并 Card 内容到 " + DST);
            }
        }
        else if (!AssetDatabase.IsValidFolder(DST))
        {
            Debug.LogError("[Addressables] 找不到 Card 目录，请确认 Assets/Resources/Card 或 Assets/Art/Card 存在");
            return;
        }

        // 2. 删除根目录遗留的旧 icon_001..036（已被 144 张新牌面取代）
        foreach (var g in AssetDatabase.FindAssets("", new[] { DST }))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (AssetDatabase.IsValidFolder(p)) continue;
            if (Path.GetFileName(p).StartsWith("icon_") && p.EndsWith(".png"))
                AssetDatabase.DeleteAsset(p);
        }

        // 3. 重命名牌面为 card_000..card_143（含 card_front），顺序与关卡 face 索引一致
        var guids = AssetDatabase.FindAssets("", new[] { DST });
        System.Array.Sort(guids, (a, b) => AssetDatabase.GUIDToAssetPath(a).CompareTo(AssetDatabase.GUIDToAssetPath(b)));
        int idx = 0;
        var spritePaths = new System.Collections.Generic.List<string>();
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".png")) continue;
            string dir = Path.GetDirectoryName(path).Replace('\\', '/');
            bool isFront = path.EndsWith("card_front.png");
            bool inSubfolder = dir != DST; // 真实牌面都在 groupNN_icons 子目录；根目录仅 card_front 需要
            if (!isFront && !inSubfolder) continue;

            string newName = isFront ? "card_front" : ("card_" + idx.ToString("D3"));
            if (path != dir + "/" + newName + ".png")
                AssetDatabase.RenameAsset(path, newName);
            spritePaths.Add(dir + "/" + newName + ".png");
            if (!isFront) idx++;
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 3.5 这些 png 当前是 Sprite Mode = Multiple（每张仅 1 个子精灵）。
        // 但 Multiple 模式下 LoadAsset<Sprite>/Addressable<Sprite> 会拿不到精灵（需按子精灵取），
        // 故统一改为 Sprite(2D and UI) + 单精灵(Single)，这样才能按文件 address 直接加载。
        foreach (var sp in spritePaths)
        {
            var ti = AssetImporter.GetAtPath(sp) as TextureImporter;
            if (ti != null)
            {
                ti.textureType = TextureImporterType.Sprite;
                // Unity 6 中 spriteMode 已从 TextureImporter 移除，需经 TextureImporterSettings 设置
                var s = new TextureImporterSettings();
                ti.ReadTextureSettings(s);
                s.spriteMode = 1; // Single（每张图仅一个子精灵）
                ti.SetTextureSettings(s);
                ti.SaveAndReimport();
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 4. 创建/更新 SpriteAtlas，把全部牌面加进去（作为合图容器）
        var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(ATLAS_PATH);
        if (atlas == null)
        {
            atlas = new SpriteAtlas();
            AssetDatabase.CreateAsset(atlas, ATLAS_PATH);
        }
        var sprites = new System.Collections.Generic.List<Sprite>();
        foreach (var sp in spritePaths)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(sp);
            if (s != null) sprites.Add(s);
        }
        SpriteAtlasExtensions.Add(atlas, sprites.ToArray());

        // 合图打包参数：关闭旋转（UI 牌面不应被旋转，避免部分平台渲染成旋转态）、
        // 加 padding（避免相邻牌面像素渗入出现杂色/串色）、关闭 tight packing（矩形排布边缘更干净）。
        var packing = new SpriteAtlasPackingSettings();
        packing.padding = 8;
        packing.enableRotation = false;
        packing.enableTightPacking = false;
        atlas.SetPackingSettings(packing);

        // Unity 6：includeInBuild 与平台设置改由 SpriteAtlasImporter 控制
        var atlasImporter = AssetImporter.GetAtPath(ATLAS_PATH) as SpriteAtlasImporter;
        if (atlasImporter != null)
        {
            atlasImporter.includeInBuild = false; // 仅通过 Addressables 提供，避免打进默认包
            var ps = atlasImporter.GetPlatformSettings("Default");
            ps.maxTextureSize = 4096; // 合图尺寸，确保 144 张小图能放进一张（按实际图大小可下调）
            atlasImporter.SetPlatformSettings(ps);
            atlasImporter.SaveAndReimport();
        }

        // 5. 标记 Addressable：图集 + 每张 sprite
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[Addressables] 未找到 AddressableAssetSettings，请先打开 Window/Asset Management/Addressables/Groups 初始化");
            return;
        }
        if (!settings.GetLabels().Contains(LABEL)) settings.AddLabel(LABEL);

        // 图集本身
        var atlasEntry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(ATLAS_PATH), settings.DefaultGroup);
        atlasEntry.address = ATLAS_ADDRESS;
        if (!atlasEntry.labels.Contains(LABEL)) atlasEntry.labels.Add(LABEL);

        // 每张 sprite（单 sprite 纹理，按文件名即 address 加载）
        int count = 0;
        foreach (var sp in spritePaths)
        {
            string name = Path.GetFileNameWithoutExtension(sp);
            string address = name; // 已是 card_000 / card_front 形式
            string guid = AssetDatabase.AssetPathToGUID(sp);
            var entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
            entry.address = address;
            if (!entry.labels.Contains(LABEL)) entry.labels.Add(LABEL);
            count++;
        }
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        Debug.Log("[Addressables] 标记完成：图集(" + ATLAS_ADDRESS + ") + " + count + " 张 sprite，均打 Label=" + LABEL + "；运行时按 address 加载");
    }

    // 把 src 下的子文件夹与根文件合并移动到 dst（dst 已存在且非空时使用）
    private static void MergeFolder(string src, string dst)
    {
        foreach (var sub in AssetDatabase.GetSubFolders(src))
        {
            string name = Path.GetFileName(sub);
            string dest = dst + "/" + name;
            if (!AssetDatabase.IsValidFolder(dest)) AssetDatabase.MoveAsset(sub, dest);
        }
        foreach (var g in AssetDatabase.FindAssets("", new[] { src }))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (AssetDatabase.IsValidFolder(p)) continue;
            string name = Path.GetFileName(p);
            string dest = dst + "/" + name;
            string abs = Path.Combine(Application.dataPath, dest.Substring("Assets/".Length));
            if (!File.Exists(abs)) AssetDatabase.MoveAsset(p, dest);
        }
    }

    [MenuItem("Tools/Build Addressables")]
    public static void Build()
    {
        if (AddressableAssetSettingsDefaultObject.Settings == null)
        {
            Debug.LogError("[Addressables] 未初始化，无法 Build");
            return;
        }
        AddressableAssetSettings.BuildPlayerContent();
        Debug.Log("[Addressables] Build 完成");
    }
}
#endif
