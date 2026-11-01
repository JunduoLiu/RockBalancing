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
- [决策地图](https://github.com/JunduoLiu/RockBalancing/issues/1)
- [任务与讨论](https://github.com/JunduoLiu/RockBalancing/issues)
- [三人分工建议](PROPOSAL.md)
- [追踪约定](docs/agents/issue-tracker.md)
- [Agent 技能配置](AGENTS.md)

GitHub Issues 是任务、规格、认领、状态与决策讨论的唯一追踪来源。
仓库保留领域术语、架构决策和供 Issues 引用的方案文档。
当前地图的子议题是决策票；职责与接口确认后，经 `to-spec`、`to-tickets` 拆分开发实现任务。

此项目不使用 Agent Hub。
