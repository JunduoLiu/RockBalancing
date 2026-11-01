# RockBalancing

面向手机浏览器的轻量 Unity 3D 叠石头小游戏。目前处于玩法、职责与接口规划阶段。

## 打开 Unity 项目

使用 Unity **6000.3.20f1**。在 Unity Hub 中添加本仓库根目录，即同时包含
`Assets/`、`Packages/` 和 `ProjectSettings/` 的文件夹。

当前起始场景为 `Assets/Scenes/SampleScene.unity`。仓库包含 Unity 项目初始文件，
玩法与分工决策仍按下方文档和 GitHub Issues 追踪。

`Library/`、`Temp/`、`Logs/`、`UserSettings/` 等本地生成内容由 Unity 重建，不纳入版本控制。

## 项目文档

- [领域术语](CONTEXT.md)
- [决策地图](MAP.md)
- [任务索引](TICKETS.md)
- [三人分工建议](PROPOSAL.md)
- [追踪约定](TRACKER.md)
- [Agent 技能配置](AGENTS.md)

GitHub Issues 是任务状态与讨论的来源。本地地图和决策票保留为带链接的文档快照。
开发实现任务需在职责与接口确认后另行拆分。

此项目不使用 Agent Hub。
