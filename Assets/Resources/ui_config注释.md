# ui_config 配置说明

本文件是游戏**唯一的运行时配置表**（JSON），由 `GameController.LoadResources` 通过
`JsonUtility.FromJson<ConfigData>` 解析为 `ConfigData`（`Assets/Scripts/Data/GameData.cs`），
在运行时被读取。

> 修改 json 后保存即生效（编辑器模式需重新进入游戏 / 重开本关重新加载）。
> 字段名**区分大小写**；新增字段必须同步在 `ConfigData` 中声明，否则被忽略。
> 这是将原先 `ui_config.json` 与 `layout_debug.json` 合并后的单一配置表，
> 原 `layout_debug.json` 已废弃、不再读取。

---

## 字段说明

### 一、整体布局适配
| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `fit_to_screen` | bool | `true` | 是否按屏幕自适应缩放牌阵 |
| `padding` | int | `40` | 牌阵外边距（像素） |
| `zoom` | float | `1.0` | 整体缩放系数 |

### 二、牌 / 牌面 / 阴影尺寸（像素）
| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `tile_width` | float | `40` | 牌底（Body/Back）宽度 |
| `tile_height` | float | `56` | 牌底高度 |
| `face_width` | float | `40` | 牌面图案（Face）宽度 |
| `face_height` | float | `64` | 牌面图案高度 |
| `shadow_x` | float | `5` | 阴影 X 偏移（右下方向，营造 2.5D 叠层阴影） |
| `shadow_y` | float | `7` | 阴影 Y 偏移 |

### 三、层偏移（2.5D 堆叠错位）
| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `layer_dx` | float | `-3.6` | 每一层相对上一层在 X 方向水平错位（底部滑条默认值，重排时叠加） |
| `layer_dy` | float | `5.3` | 每一层相对上一层在 Y 方向高度错位 |

### 四、翻转动画
| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `flip_dur` | float | `0.25` | 正面↔反面翻转动画总时长（秒）。点击反面牌翻正面、取消选中翻回反面均使用此值（`FlipToFront` / `FlipToBack`） |

### 五、配对碰撞消除流程（`CrashSequence`）
配对成功后两张牌的动画编排参数：
| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `crashSideOffset` | float | `40` | 配对后两牌在中点左右各偏移的水平距离（左右目标点初始 X = 中点 X ± 此值） |
| `liftDur` | float | `0.15` | 抬起并移到左右目标点的时长 |
| `collideDur` | float | `0.02` | 两牌从左右目标点相撞到中央点的时长 |
| `shrinkScale` | float | `0.3` | 碰撞瞬间缩放到的目标比例 |
| `shrinkDur` | float | `0.14` | 缩小动画时长 |
| `flyDur` | float | `0.25` | 缩小后飞向图鉴按钮的时长 |
| `pauseDur` | float | `0.25` | 移到左右目标点后、相撞前的停顿时长 |

### 六、调试开关
| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `show_offset_sliders` | bool | `false` | 是否在底部栏显示 X/Y 层偏移滑条（实时重排，观察 2.5D 堆叠错位）。设为 `true` 可在运行时拖动调参；正式发布建议保持 `false` |

---

## 备注
- 等级着色（`CODE_COLORS` 白/绿/蓝/金，用于 `Back_1/Back_2` 与图鉴等级块）仍由代码硬编码，
  不在本配置表中。
- 音频（BGM / 碰音 / 选牌 / 失败）路径与开关（音乐、音效、震动、画质、语言）在
  `SaveManager.SettingsData`（`save.json`）中，与本表独立。
