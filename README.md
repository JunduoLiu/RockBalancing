# RockBalancing

面向手机浏览器的轻量 Unity 3D 叠石头小游戏。首版玩法已实现，正在进行设备验收。

[打开公开测试版](https://rock-balance-playtest-20261110.burblingcrow1.chatgpt.site/)（无需登录）。当前为站点版本 **2**，游戏构建对应 `0348d9c`，包含 #22–25 的扁石、摇杆、独立释放及三维观察迭代。见[本轮构建与桌面预检证据](docs/experiments/issue-26-web-preflight.md)和 [Issue #26](https://github.com/JunduoLiu/RockBalancing/issues/26)；三项 iPhone＋Chrome 真机验收待测，[PR #21](https://github.com/JunduoLiu/RockBalancing/pull/21) 仍为草稿且未合并。

## 打开 Unity 项目

使用 Unity **6000.3.20f1**。在 Unity Hub 中添加本仓库根目录，即同时包含
`Assets/`、`Packages/` 和 `ProjectSettings/` 的文件夹。

当前可玩场景为 `Assets/RockBalancing/Scenes/Balance.unity`，打开后进入 Play Mode。
操作方式、Web 构建和测试命令见[游戏说明](Assets/RockBalancing/README.md)。

`Library/`、`Temp/`、`Logs/`、`UserSettings/` 等本地生成内容由 Unity 重建，不纳入版本控制。

## 项目文档

- [领域术语](CONTEXT.md)
- [决策地图](https://github.com/JunduoLiu/RockBalancing/issues/1)
- [任务与讨论](https://github.com/JunduoLiu/RockBalancing/issues)
- [三人分工建议](PROPOSAL.md)
- [追踪约定](docs/agents/issue-tracker.md)
- [Agent 技能配置](AGENTS.md)

GitHub Issues 是任务、规格、认领、状态与决策讨论的唯一追踪来源。
仓库保留领域术语、架构决策和供 Issues 引用的方案文档。
已确认规格见 [#9](https://github.com/JunduoLiu/RockBalancing/issues/9)，实现与设备验收票按地图顺序追踪。

此项目不使用 Agent Hub。
