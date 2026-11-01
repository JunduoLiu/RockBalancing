# Issue tracker: GitHub

任务、规格、认领、状态与决策讨论统一在 [JunduoLiu/RockBalancing 的 GitHub Issues](https://github.com/JunduoLiu/RockBalancing/issues) 追踪。
GitHub Issues 是唯一追踪来源；仓库保留领域文档和供议题引用的方案材料，不维护本地任务或地图快照。
优先使用已连接的 GitHub 工具；命令行认证可用时可使用 gh。
从 git remote -v 确认仓库，操作时显式指定仓库。

- 创建、读取、评论、标记和关闭议题均在 GitHub 进行。
- gh 的多行正文使用 --body-file；读取议题时包含评论和标签。
- 技能要求“发布到 tracker”时创建 GitHub Issue；要求“读取相关 ticket”时读取对应 Issue 的正文、评论和标签。
- 建议与已确认决定分开记录；需要人参与的决策由人实际参与。
- 已确认输入继续有效；议题开放只表示仍有未决部分。

## Wayfinding operations

- 地图是带 `wayfinder:map` 标签的单个 Issue；当前入口为[手机叠石头小游戏的三人协作边界](https://github.com/JunduoLiu/RockBalancing/issues/1)。
- 决策票使用与类型匹配的 `wayfinder:research`、`wayfinder:prototype`、`wayfinder:grilling` 或 `wayfinder:task` 标签，作为地图的原生子议题。
- 阻塞关系使用 GitHub 原生 issue dependencies。仅在对应原生能力不可用时，使用地图清单及子票正文的 `Part of`、`Blocked by` 约定。
- 下一票：查询地图的子议题，按其顺序选择开放、未认领且所有阻塞票已关闭的第一张。开放票状态以查询为准。
- 开始解决前，将票指派给实际负责的开发者。历史处理者 `codex` 不自动映射为任何 GitHub 账号。
- 正文的 `Question` 记录问题。解决时发布包含实际回答与日期的 `Resolution` 评论，关闭议题，再在地图的 `Decisions so far` 添加具名链接和一句摘要。
- `Decisions so far` 只索引已完成决策；决策细节保留在对应议题。
- 决策票用于确定边界。决策收敛后，经 `to-spec` 汇总规格、`to-tickets` 拆分实现票并建立阻塞关系，再逐票 `implement`。这些实现票已准备就绪，无需再次 triage。

## Pull requests as a triage surface
PRs as a request surface: no.
