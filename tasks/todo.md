# 运行时地形与建筑编辑执行清单

- [ ] 1. 隔离 `Medieval Castle - Modular`：只评估 Wall/Tower，禁止把 Demo 或完整 Castle 合入主场景。
- [ ] 2. 创建灰石 URP `RuntimeWall.prefab` 与 `RuntimeTower.prefab`，校正 Pivot、Collider、比例并完成风格门槛检查。
- [ ] 3. 建立运行时 TerrainData 副本与 Reset，证明退出 Play Mode 后 `Assets/New Terrain.asset` 不变。
- [ ] 4. 实现并测试 Raise、Lower、Smooth 三种局部笔刷，以及 Size/Strength 控制。
- [ ] 5. 实现 Wall/Tower 的绿色/红色预览、1m 吸附、`Q/E` 15° 旋转和左键放置。
- [ ] 6. 实现已放置模块选择与 `Delete` 删除；保护原城堡和场景对象不可删除。
- [ ] 7. 加入 Edit Mode HUD 与独立编辑镜头，屏蔽 UI 点击和非编辑模式输入。
- [ ] 8. 在 `Castle_Exterior` 完整演练地形编辑、建筑放置/旋转/删除、Reset，并验证 Console 与原始资产。

## 检查点

- [ ] 完成 1–2：只新增两个视觉合格的可编辑模块，没有第二座城堡。
- [ ] 完成 3–4：三种笔刷可用，原 TerrainData 可证明未被修改。
- [ ] 完成 5–7：无需 Inspector 即可完成全部运行时编辑操作。
- [ ] 完成 8：主场景一次演示通过，Console 无新增 Error，截图与测试记录齐全。

## 场景环境布置清单

- [x] 9. 在 `Castle_Exterior` 建立六个环境根节点，确认主门方向，并保留 6m×45m 的入口视线与通行走廊。
- [x] 10. 使用 Ash、Birch、Chestnut、Spruce 的 URP Prefab 放置第一轮 24–32 棵外围树；不使用 Willow。
- [x] 11. 用 Fence、Crate、Barel、Bucket 建立门外守卫检查区，保留 4m 通道和 NPC 站位。
- [x] 12. 用 Crate、Barel、Rope、PlanksPile 及组合式简易摊位建立商人区，保留 2.5m 交互空间。
- [x] 13. 用 Fence、Axe、Hammer、Pickaxe、Trunk 和组合式训练假人建立 18×22m 骑士训练区。
- [x] 14. 用 Rock、Trunk、LongGrass、Dandelion 完成道路—林缘—城墙根部过渡；不使用 CrystalCluster 和 Anchor。
- [x] 15. 检查 URP 材质、碰撞体、遮挡、NPC动线及 25×35m 运行时编辑空地，确保 Console 无新增 Error。

## 环境布置检查点

- [x] 完成 9–10：出生点能看见主门，Terrain 边界有遮挡且没有规则树阵。
- [x] 完成 11–13：守卫、商人、骑士区域不看 UI 也能辨认，主路持续可走。
- [x] 完成 14–15：无粉色、漂浮、穿地或阻路物件，玩法区域与运行时编辑互不干扰。
