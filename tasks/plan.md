# 实施计划：中世纪城堡外部环境与自然高地

## 核心路线

现状测量与双重备份 → 导入三类 CC0 2K PBR 贴图 → 配置 Terrain Layer → 生成可回退的城堡高地 → 按坡度与高度自动混合地表 → Scene/Game/Console 三重验收。

本轮停止在“城堡自然落在高地上，草地、泥土、岩石正常工作”。道路、树木、灌木、NPC、动画、城门交互和 LLM 系统全部不进入本轮。

## 已确认现状

- Unity 为 `2022.3.62f3c1`，URP 为 `14.0.12`；当前质量等级索引为 1，对应 `Balanced`，并引用 `URP-Balanced.asset`。
- 主场景为 `Assets/Scenes/Castle_Exterior.unity`；城堡实例位置为 `(0, 0, 0)`，旋转为 0，缩放为 1，不应移动或缩放。
- Terrain 位置为 `(-90, 0, -90)`，引用 `Assets/New Terrain.asset`；实际尺寸、高度和分辨率仍需在 Unity API 中读取后决定是否调整。
- 已有草地贴图与 `Assets/NewLayer.terrainlayer` 正常存在。资产真实参数为 Tile Size `8 × 8`、Metallic `0`、Smoothness `0`，与旧任务说明中的数值不一致；执行时以当前资产为准并保留它。
- `NullReferenceException` 的栈顶来自 `UnityEditor.TerrainInspector.ShowResolution` 与 `EditorGUI.DelayedFloatField`，不是项目脚本或 glTFast；先按 Unity Inspector 状态问题处理，不删除脚本、不动包。
- 当前 Git 工作树中的项目文件全部未跟踪，不能依赖 Git 回退；必须做独立资产备份。

## 阶段 1：建立可回退基线

先用一个只读 Editor 工具读取城堡所有 Renderer 的合并 Bounds、最低可见点、Terrain 的位置、尺寸、Heightmap/Alphamap/BaseMap 分辨率、当前 Terrain Layer、活动质量等级和 Console 错误。随后保存场景，并同时备份：

- `Assets/Scenes/Backup/Castle_Exterior_BeforeTerrainWork.unity`
- `Assets/Scenes/Backup/New Terrain_BeforeTerrainWork.asset`

只备份场景不合格，因为场景仍会引用同一份 TerrainData。

验收：备份场景改为引用备份 TerrainData；原始城堡 GLB、glTFast 包和 `NewLayer` 均未变；基线截图与测量结果已记录。

## 阶段 2：取得最小资源集并规范导入

从 Poly Haven 或 ambientCG 选择三套 CC0、2K、无明显方向性的 PBR 地表：自然绿草、泥土/枯草、灰褐岩石。只取 Base Color、OpenGL Normal、Roughness，确有价值时再取 AO；不下载 4K/8K、位移贴图或模型包。资源分别放入：

- `Assets/Art/Textures/Terrain/Grass/`
- `Assets/Art/Textures/Terrain/Soil/`
- `Assets/Art/Textures/Terrain/Rock/`

颜色贴图启用 sRGB；Normal 设为 Normal Map；全部限制 Max Size 2048、启用 Mip Maps、Repeat，并采用平台默认压缩。Roughness/AO 关闭 sRGB。每套资源把名称、原始 URL、CC0 许可和用途写入 `Assets/Art/Textures/Terrain/THIRD_PARTY_NOTICES.md`。

验收：三套贴图无导入警告；法线凸凹方向正确；预计常驻显存与编辑器稳定性符合 6GB VRAM 目标。

## 阶段 3：配置并实际应用 Terrain Layer

创建 `TL_Grass_Lush`、`TL_Soil_Dirt`、`TL_Rock_Cliff` 到 `Assets/Art/TerrainLayers/`，保留 `NewLayer`。起始范围为草 8–12m、土 6–10m、岩 5–8m，Metallic 均为 0，Smoothness 约为 0.20/0.15/0.15，再按纹理实际尺度现场校正。URP Terrain Layer 不直接使用单独 Roughness；若资源适合，则由 Editor 工具把 Roughness/AO 安全打包为 Mask Map，否则以 Smoothness 标量为准。

验收：四个 Layer 都在 TerrainData 中；三类新材质不是“只创建未绘制”；近景无明显棋盘格、错误法线或高光塑料感。

## 阶段 4：生成自然高地

在 `Assets/Editor/Tools/CastleTerrainBuilder.cs` 保存可复用 Editor 工具。工具以 Renderer Bounds 为唯一几何依据：城堡占地外扩安全边距形成平整平台，平台高度对齐城堡最低可见结构；再用 smoothstep 类衰减向 Terrain 外围下降，并叠加幅度受限的低频噪声和轻微非对称偏移。城门方向设置更宽、更缓的保护区，防止掩埋入口；平台范围内不加噪声。

Terrain 目标约 `180 × 30–40 × 180m`、Heightmap 513，但只有实测不满足时才调整。生成前记录 Undo，并提供“恢复备份/重新生成”入口；不修改城堡 Transform。

验收：平台平整且比城堡 Bounds 向外延伸；城堡底部自然接地；城门、外墙和塔楼不被地形穿过；坡体没有规则圆锥、完美对称或尖锐噪点。

## 阶段 5：按地形语义混合材质

用同一工具基于高度、坡度和低频遮罩生成 Alphamap：缓坡以 `TL_Grass_Lush` 为主，平台边缘及过渡带加入 `TL_Soil_Dirt`，陡坡和局部高差处使用 `TL_Rock_Cliff`；`NewLayer` 仅作为少量色差补充，不主导整体。混合阈值带随机扰动但保持连续，避免环形色带。

验收：草、土、岩的分布与坡度关系合理；三者过渡连续；远近视角都没有明显重复网格或机械圆环。

## 阶段 6：视觉校正与交付

在 Scene 与 1920×1080 Game 视图检查四个方向和主镜头，只做地形、贴图比例和摄像机取景的微调，不改城堡比例。清空 Console 后触发一次资源刷新、保存场景并再次检查；若 Terrain Inspector 的 NullReference 仍可复现，记录为 Unity Editor 内部问题，采用关闭/重开 Inspector 或重启编辑器规避，不做破坏性“修复”。

验收：场景保存；Console 无新增项目错误；提供最终 Scene/Game 截图、资源与许可表、Terrain Layer 参数、Terrain 实际参数、雕刻方法、NullReference 结论和遗留问题。

## 风险与控制

| 风险 | 判断 | 控制方式 |
|---|---|---|
| 地形修改不可回退 | 高 | 场景与 TerrainData 双备份、Undo、确定性生成参数 |
| Terrain 穿过城墙或城门 | 高 | 用 Bounds 生成平台，再对城门方向人工复核与局部修正 |
| 贴图显存和导入体积过大 | 中 | 仅 2K、Mip Maps、压缩、只下载必要通道 |
| 自动混合出现规则环带 | 中 | 坡度+高度+低频遮罩联合，而非只按到中心距离 |
| Editor NullReference 反复刷屏 | 低 | 已定位 TerrainInspector；先规避 UI 状态，绝不删除项目脚本 |

## 完成定义

城堡模型、比例和 glTFast 保持不变；城堡稳定落在自然高地平台上；外围山坡平滑且不规则；草、泥土、岩石三类材质均真实应用；无明显穿模和新增 Console 错误；原场景、备份场景、TerrainData、工具源码、许可记录及两张最终截图全部可交付。
