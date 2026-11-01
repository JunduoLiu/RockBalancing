# 叠石头小游戏：决策 Tickets

地图：[手机叠石头小游戏的三人协作边界](MAP.md)

这些是 wayfinder 决策票：解决分工之前需要明确的问题。不是“实现生成器”“实现操作器”一类开发任务。具体开发任务应依据确认的边界另行拆分。

已迁移到独立仓库 [JunduoLiu/RockBalancing](https://github.com/JunduoLiu/RockBalancing)。GitHub Issues 是状态来源；本文件与 tickets/ 为文档快照。

## 可开始

| Ticket | 尚需决定什么 | 完成时留下什么 |
|---|---|---|
| [摆放、释放与丢弃怎样交接控制权](tickets/01-stone-handoff.md) | A/B/C 怎样交接石头；谁验证摆放；释放后谁控制；无效释放怎样返回 | 一份生成、待摆、释放、弃石的状态与控制权约定 |
| [高度、倒塌与重开怎样定义](tickets/02-height-and-restart.md) | 哪些石头属于有效塔体；什么时候算稳定高度；谁提供当前高度与最高纪录 | 一份计分、稳定判定与重开职责约定 |

以上均为 open、未认领、无阻塞。默认下一张是“摆放、释放与丢弃怎样交接控制权”。

## 等待前置决定

| Ticket | 等待什么 | 完成时留下什么 |
|---|---|---|
| [全景分享图与成绩怎样保持一致](tickets/03-panorama-sharing.md) | “高度、倒塌与重开怎样定义” | B 提供的数据、C 拍照时机、全景图与成绩配对方式、分享负责人 |
| [三人的最终职责与联合验收怎样划分](tickets/04-ownership-and-acceptance.md) | 控制权、计分、分享与游玩入口均明确 | 三份可认领的职责范围、交付物、交接约定和联调验收链路 |

以上均为 open、未认领；游玩入口前置已完成，其余依赖仍开放。

## 已完成

| Ticket | 决定 |
|---|---|
| [手机分享链接通往哪种游玩入口](tickets/05-mobile-entry.md) | 手机浏览器通过链接直接游玩。运行与分享兼容性仍需实际验证。 |

## 已确认但未完整关闭的票

- “摆放、释放与丢弃怎样交接控制权”已记录：弃石留场参与碰撞，不可重新拿取，不计高度；接口与控制权仍待确认。
- “高度、倒塌与重开怎样定义”已记录：倒塌保留现场，可继续或手动重开；有效塔体及稳定判定仍待明确。
- “全景分享图与成绩怎样保持一致”已记录：分享最高稳定纪录对应的整塔全景；数据交接与拍照约定仍待确认。

已确认的回答不会重问。票开放是因为还有未决的部分，不是否定已确认输入。

分工建议与讨论材料：[三人分工方案 v1](PROPOSAL.md)。GitHub Issues 是状态的依据，本地票的元数据与本索引是迁移快照。


## GitHub 对照

| 本地记录 | GitHub |
| --- | --- |
| 手机叠石头小游戏的三人协作边界 | [#1](https://github.com/JunduoLiu/RockBalancing/issues/1) |
| 摆放、释放与丢弃怎样交接控制权 | [#2](https://github.com/JunduoLiu/RockBalancing/issues/2) |
| 高度、倒塌与重开怎样定义 | [#3](https://github.com/JunduoLiu/RockBalancing/issues/3) |
| 全景分享图与成绩怎样保持一致 | [#4](https://github.com/JunduoLiu/RockBalancing/issues/4) |
| 三人的最终职责与联合验收怎样划分 | [#5](https://github.com/JunduoLiu/RockBalancing/issues/5) |
| 手机分享链接通往哪种游玩入口 | [#6](https://github.com/JunduoLiu/RockBalancing/issues/6) |
