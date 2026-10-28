# AI 使用记录

> 工具：Antigravity IDE（Claude 模型）
> 按实际发送顺序记录，Prompt 原文保持不变。

## P-001

- 目标：让 AI 阅读任务书，梳理需求、技术方案和时间安排

### Prompt 原文

> 你看一下这个txt文件，这是一个面试题目
> 请自今天起5天内提交（今天计为第1天）。在保证作业质量的前提下，提交越早，评价越高。
>
> 完成后，请将作业及链接发送至hr@holocode.net，邮件标题统一为"全栈工程师实操作业-姓名"，并在附件中附上个人简历；同时在BOSS上回复"已完成"，并附上作业链接。
>
> 如果这里面有你不熟悉的研发技能，别担心，通过 AI 学习和掌握不熟悉的技能也是考察内容一部分。
> 另外，题目中对手机是否有 AR 模块有依赖（你看完题目就知道我在讲啥了），如果你的手机不支持 AR 模块，可以来找我换题目

### 结果

- 采用
- AI 整理出三端必做功能、交付物和注意事项；本人对照任务书原文逐条核对，确认没有遗漏。

## P-002

- 目标：确认手机是否支持 AR，并检查本机开发环境

### Prompt 原文

> 我是三星的s23ultra

### 结果

- 采用
- AI 确认 S23 Ultra 在 Google ARCore 支持设备列表中；检查本机后发现 Go、Node.js、Git、Unity 均未安装。

## P-003

- 目标：纠正回复语言

### Prompt 原文

> 能不能中文回答我

### 结果

- 采用
- 之后的回复和操作说明统一使用中文。

## P-004

- 目标：规范开发工具的安装位置

### Prompt 原文

> 电脑上的软件给我安装再E:\下面吧，要求文件规范化，我手机上已经有 Google Play Services for AR，版本号1.57.262640093

### 结果

- 修改后采用
- AI 原计划用 winget 默认安装到 C 盘，按本人要求改为统一装到 `E:\DevTools\` 下的子目录（Git / Go / GoPath / NodeJS / NodeCache / UnityHub / Unity\Editors）。
- 验证：分别执行 `git --version`、`go version`、`node -v`，确认程序路径都在 E:\DevTools 下。

## P-005

- 目标：设计并实现 Go 后端核心服务，提供健康检查、问题上报、列表查询与状态修改接口，并保证数据持久化

### Prompt 原文

> 现在开始写 Go 后端。请严格按照任务书要求实现：健康检查接口、问题增删查改和状态更新（open, in_progress, resolved）。注意三端字段名称必须一致（id, title, description, priority, status, position: {x,y,z}, createdAt, updatedAt）。数据库用 SQLite，但不要使用需要 GCC 的 CGO 库，容易有跨平台编译问题，找一个纯 Go 实现的 SQLite 驱动。要写单元测试验证重启后数据仍然存在，以及空标题返回 400 错误。

### 结果

- 修改后采用
- AI 初步方案建议使用 `mattn/go-sqlite3`，经本人评估该库强依赖 CGO 环境，在 Windows 系统下容易出现工具链缺失问题。要求 AI 改用纯 Go 驱动 `modernc.org/sqlite`，并开启 WAL 模式以保证单写多读的并发安全性。
- 验证：运行 `go test -v ./...`，自动化单元测试全部通过（PASS），覆盖参数校验（空标题 400、无效枚举 400）、状态更新与服务重启持久化验证。

## P-006

- 目标：实现 React + TypeScript 管理端界面，支持列表展示、状态修改与防白屏容错

### Prompt 原文

> 现在开始开发 React 管理端。用 Vite + React + TypeScript 搭建，放到 admin-web 目录。实现任务书 3.2 的要求：展示问题列表（标题、优先级、状态、坐标），能够直接把状态修改为 open/in_progress/resolved。任务书特别强调后端不可用时不能白屏，要加全局 ErrorBoundary 和重试提示。UI 风格做成清晰直观的深色管理看板，顺便做个 2D 雷达俯视图把巡检点的 x/z 坐标直观画出来。

### 结果

- 采用
- AI 实现了问题表格、状态即时切换、优先级彩色徽章，并封装了 ErrorBoundary 错误边界组件防止网络或数据异常引起白屏崩溃。同时利用 SVG 实现了现场问题点在 X/Z 水平面上的 2D 雷达投影。
- 验证：执行 `npm run build` 打包顺利通过；在后端停机状态下访问前端，页面准确弹出离线重试警告横幅，未出现白屏；启动后端后能够正常拉取数据并实时修改状态。

## P-007

- 目标：使用 Unity AR Foundation 实现真机水平平面检测、点击放置 3D 标记与防 UI 误触穿透逻辑

### Prompt 原文

> 现在开发 Unity AR 客户端，基于 AR Foundation 5.x 和 ARCore。需要实现：1. 水平平面扫描与点击检测；2. 在命中位置生成 3D 标记牌（朝向相机，带优先级颜色）；3. 弹出填写表单（标题必填、描述可选、优先级高中低、自动填入 x/y/z 坐标）；4. 提交后给 Toast 提示。注意任务书 3.1 明确要求：点击 UI 按钮或输入框时，绝对不能误在场景中生成标记，这个防穿透逻辑要处理好；另外在手机上要能动态配置后端的 IP 地址，不要写死。

### 结果

- 修改后采用
- AI 最初仅使用 `EventSystem.current.IsPointerOverGameObject(touch.fingerId)` 进行防穿透，在部分 Android 触摸抬起瞬间偶发误触。本人要求补充了 `GraphicRaycaster.RaycastAll` 双层过滤，确保触摸 UI 任何区域时绝不向场景发射 AR Raycast；并在应用内增加了运行时 IP 配置面板与连通性测试。
- 验证：在真机上操作设置弹窗、表单输入框与按钮，场景中不会误放置标记；点击未遮挡的检测平面时精准生成标记点。

## P-008

- 目标：解决 Unity 导出 Android APK 时的 SDK 版本要求、图形接口冲突与纯 64 位芯片架构问题

### Prompt 原文

> 我在 Unity Build Settings 里点了 Build And Run 打包到手机，但是控制台报错了：ARCore Required apps require a minimum SDK version of 24，还有 You have enabled the Vulkan graphics API which is not supported by ARCore。我的手机是三星 S23 Ultra（骁龙 8 Gen 2）。帮我写一个自动配置脚本把这些打包参数修复好。

### 结果

- 采用
- AI 定位了三处关键配置：ARCore 强制要求最低 Android 7.0 (API 24)；ARCore 官方不支持 Vulkan，必须将 Android Graphics API 切换为 OpenGLES3；三星 S23 Ultra 搭载骁龙 8 Gen 2 纯 64 位架构，必须将 Scripting Backend 设为 IL2CPP 并启用 ARM64。编写了自动化配置脚本并应用。
- 验证：再次执行 `Build And Run`，Gradle 编译顺利通过，并通过 USB ADB 自动将 APK 安装至手机并直接拉起运行。

## P-009

- 目标：解决 Android 真机运行时中文字体缺失显示方块，以及 UnityWebRequest 默认阻断明文 HTTP 的问题

### Prompt 原文

> 手机上 App 跑起来了，但是有两个问题：1. 屏幕上的中文全部变成了方块或者空白，只有英文和数字正常，我键盘输入的中文也变成了方块；2. 手机上的设置面板里测试连接，一直提示无法连接到电脑上的 Go 服务（电脑上 8080 是开着的）。抓一下真机日志看看是怎么回事。

### 结果

- 修改后采用
- 通过 ADB logcat 抓取真机运行日志定位根因：
  1. 网络阻断：日志显示 `Non-secure network connections disabled in Player Settings`。Unity 默认的 `insecureHttpOption` 为 0（禁止明文 HTTP），在 `PlayerSettings` 中将其开启为 `AlwaysAllowed`，并将客户端默认请求地址对齐到本机局域网 IP `http://192.168.31.6:8080`；
  2. 字体方块：TextMeshPro 默认只有英文字元，动态图集在手机真机运行时受显存与初始尺寸限制无法动态光栅化生字。经本人决策，将系统黑体字库引入工程，并编写脚本将 3800 个国家一级常用规范汉字预烘焙进字体贴图，从源头彻底杜绝方块字。
- 验证：真机重新打包后，顶部指示灯立即变绿；在手机端测试连接显示 `连接成功！延迟: 5ms · 数据库: ok`；表单中文与输入文字均清晰渲染，无方块乱码。

## P-010

- 目标：优化移动端小屏幕下的表单触控体验，解决下拉菜单点击无响应问题

### Prompt 原文

> 手机表单里的优先级下拉框点不动，只能保持默认的“高”。在手机这种小屏幕上用下拉菜单本来就不太好按，能不能改成点一下就自动切换（高->中->低->高）的形式，配上红黄蓝颜色变化，方便单手快速操作？

### 结果

- 采用
- AI 分析原组件因缺乏移动端下拉模板层级导致触摸被拦截，改用实现 `IPointerClickHandler` 原生触摸接口的轻量级循环徽章：单手轻触即可在【高 (红)】➔【中 (橙)】➔【低 (蓝)】之间轮换，每次点击都有即时色彩与文字反馈。
- 验证：在真机上轻触优先级徽章，响应灵敏无延迟；提交新问题后，电脑端 React 看板即时收到推送，显示对应的优先级标签和颜色。
