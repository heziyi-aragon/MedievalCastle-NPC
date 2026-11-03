# 中世纪城堡环境执行清单

- [x] 1. 读取城堡 Renderer Bounds、Terrain 参数、质量配置和 Console 基线；保存 Scene/Game 基线截图。
- [x] 2. 同时备份 `Castle_Exterior.unity` 与 `New Terrain.asset`，确认备份场景引用备份 TerrainData。
- [x] 3. 从 Poly Haven/ambientCG 下载草地、泥土、岩石各一套 CC0 2K PBR，并记录 URL、许可与用途。
- [x] 4. 配置颜色、Normal、Roughness/AO 的导入设置；创建并校正 `TL_Grass_Lush`、`TL_Soil_Dirt`、`TL_Rock_Cliff`。
- [x] 5. 编写 `Assets/Editor/Tools/CastleTerrainBuilder.cs`，依据 Bounds 生成平整平台、非对称平滑坡体和低频自然起伏。
- [x] 6. 依据坡度、高度和低频遮罩生成 Alphamap，实际绘制草、土、岩，并保留 `NewLayer`。
- [x] 7. 从四向 Scene 视角与 1920×1080 Game 视图检查接地、城门、穿模、材质重复和构图，必要时小幅迭代。
- [x] 8. 清空并复查 Console，保存全部资产，输出截图、参数、许可、方法、异常结论和遗留问题。

## 阶段检查点

- [x] 完成 1–2：基线可验证、可独立回退，城堡和现有资源未改。
- [x] 完成 3–4：三类材质在 Terrain 上可见，导入设置正确，无新警告。
- [x] 完成 5–6：城堡落地、平台平整、坡体自然、三类地表分布合理。
- [x] 完成 7–8：Scene/Game 视觉通过，Console 无新增错误，交付信息完整。
