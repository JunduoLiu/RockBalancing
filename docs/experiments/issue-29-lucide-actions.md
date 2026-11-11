# #29 Lucide 主功能图标

基线：#28 的 410b71bf7a8295fae4fadbc98e9b76ff74be44df。
需求：[Issue #29](https://github.com/JunduoLiu/RockBalancing/issues/29)。

## 实现

- 四个主功能改为 Lucide 0.468.0 图标：分享 share-2、重开 rotate-ccw、释放 arrow-down-to-line、弃置 trash-2。原始 SVG、192px 透明 PNG、可复现转换脚本及 ISC / Feather MIT 许可一并保留，Web 模板含许可文本。
- 图标只是呈现层，操作仍通过原有 Button / StoneActionButton。原有按下时绑定待摆石头、连续点击冷却、取消触控以及流程占用规则不变。
- 手机与小窗口的四个主操作点击区域至少 48 × 48 屏幕像素；保留 #28 的高度/操作区布局与摇杆/升降方向文字。小窗口临时反馈移到加高的操作按钮上方，避免遮挡。
- 桌面鼠标悬停显示功能名称；提示不拦截射线，触控不生成悬停提示，按下、移出、失焦、禁用对象及进入弹窗会清理提示。
- 按下使用深绿色反馈；不可用按钮降低背景与图标对比。分享在 stablizing、分享占用和弹窗期间不可用；重开确认仍使用原来的完整文字、取消和确认按钮。
- 未改变相机观察规则、键位、物理计高、分享快照或石头生成规则。

## 验证

Unity 6000.3.20f1，通过既有 Editor/Pipeline Play Mode 路径：

| 窗口 | 范围 | 结果 |
| --- | --- | --- |
| 430 × 900 | 全套 | 55/55 passed，50.60s |
| 1280 × 720 | 全套 | 55/55 passed，50.87s |
| 320 × 568 | 本票图标与流程专项 | 6/6 passed，4.49s |
| 640 × 360 | 本票图标与流程专项 | 6/6 passed，4.44s |

以上均 0 failed、0 skipped。node --test tools/share.test.cjs 为 9/9 passed。Unity 编译与 git diff --check 通过。

新增专项覆盖：
- 四种独立 sprite、没有常驻文字标题、48px 点击区域、窗口内边界和非重叠布局。
- 鼠标悬停文字、按下颜色、提示不拦截触控、弹窗清理提示。
- 鼠标与模拟 Touchscreen 实际点击释放/弃置，交叉连点不作用于下一颗，不带动相机旋转。
- 模拟触控重开：文字弹窗，取消保持当前局，确认才清场。
- 模拟触控分享：预览、取消返回、完成回调返回，流程占用禁止释放/弃置，返回不误释放。
- stablizing 期间分享禁用和变淡，计时结束后恢复。

开发中的测试失败已修正后复跑：新 fixture 最初错误地在根对象而非子对象取 Session，且未重置已移除的模拟 Touchscreen；补正确取值、设备清理和绑定等待后通过。原始 SVG 的 currentColor 不受 Unity SVG importer 支持，因此将未改动的上游源文件放在 tools/lucide，运行时导入预渲染 PNG。失败轮次不计入最终通过数。

## 实际画面

下面是 Unity Game View 截图，已逐张检查，不是真机或浏览器截图。

- [430 × 900](issue-29-430x900.png)
- [1280 × 720](issue-29-1280x720.png)
- [320 × 568](issue-29-320x568.png)
- [640 × 360](issue-29-640x360.png)
- [鼠标悬停名称](issue-29-hover.png)
- [按下反馈](issue-29-pressed.png)
- [释放后 stablizing 与禁用状态](issue-29-stablizing.png)
- [弃置后飞石与下一颗](issue-29-discard.png)
- [分享预览](issue-29-share.png)
- [重开文字确认](issue-29-restart.png)

截图流程实际点击了释放、弃置、分享及重开，并验证重开取消、确认返回。四种窗口中主按钮测量均不少于 48px；430 × 900 的重开/分享为 48 × 48，释放 227.9 × 58.5，弃置 116.1 × 58.5。

## 验收边界

未重新构建或部署 Web，未进行 iPhone + Chrome 真机验收，也未实际调用操作系统分享面板。手机交互证据是 Unity Input System 的模拟触控；分享完成/取消桥接另有 9 项 JS 契约回归。真机和实际浏览器验收仍待进行，公开试玩站点未更新。

没有合并或关闭本票。尝试将票指派给已验证的当前开发者 Herlilen 时，GitHub 集成返回 403 Resource not accessible by integration；未换通道绕过。此前同一集成 PR 创建被拒，本票未重试创建 PR。既存 Stone.mat 修改和 Assets/Resources.meta 原样保留，不纳入提交。
