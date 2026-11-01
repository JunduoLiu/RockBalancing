# Issue tracker: GitHub

议题与规格以 JunduoLiu/RockBalancing 的 GitHub Issues 为准。
优先使用已连接的 GitHub 工具；命令行认证可用时可使用 gh。
从 git remote -v 确认仓库，操作时显式指定仓库。

- 创建、读取、评论、标记和关闭议题均在 GitHub 进行。
- gh 的多行正文使用 --body-file；读取议题时包含评论和标签。
- MAP.md、TICKETS.md 和 tickets/ 是带 GitHub 链接的文档快照。
  修改决策时先更新 GitHub，再更新受影响的本地快照。
- 地图使用 wayfinder:map；决策票使用 wayfinder:grilling。
- 优先使用原生 sub-issues 和 issue dependencies。
  不支持时，在地图中列子票，在子票正文记录 Part of 和 Blocked by。
- 下一票：按地图顺序，选择开放、未认领且所有阻塞票已关闭的子票。
- 开始解决前认领；关闭时记录实际结论，并在地图中添加具名链接。
- 建议与已确认决定分开记录；需要人参与的决策由人实际参与。

## Pull requests as a triage surface
PRs as a request surface: no.
