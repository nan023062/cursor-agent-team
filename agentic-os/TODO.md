# Agentic OS — TODO List

> 按优先级排列的待办事项，解决后 Agentic OS 才能在真实项目中可实操。

---

## P0 — 致命缺陷（不解决就跑不起来）

### [ ] 1. 模块名支持多层路径

**问题**：`ParseModuleNode` 只取 `Path.GetFileName()`，导致 `Code/Core` 和 `Art/Core` 都叫 `Core`，拓扑冲突。`DependencyNameRegex` 也不支持路径分隔符。

**修复方向**：
- 模块名改为相对于 `projectRoot` 的路径（如 `Code/Core`、`Art/Characters`）
- `DependencyNameRegex` 支持 `/` 分隔的路径名
- 所有 name 比较、字典 key 统一用相对路径

**涉及文件**：
- `Services/DnaManagerService.cs` — `ParseModuleNode`、`FindModuleDirectories`、`DependencyNameRegex`
- `Models/ModuleNode.cs` — Name 语义说明
- `Tools/DnaManagerTools.cs` — 工具参数描述

---

### [ ] 2. 拓扑扫描结果缓存

**问题**：`GetModuleContext`、`ValidateDependency`、`GetExecutionPlan` 每次调用都全量重扫目录树。中型项目几万目录，延迟不可接受。

**修复方向**：
- 内存缓存 `TopologyResult`，带 TTL（如 30 秒）或文件系统 watcher 失效
- 首次扫描后缓存，后续调用直接用缓存
- 提供 `refresh_topology` 工具手动刷新
- 考虑将拓扑结果持久化到 `.agentic-os/topology-cache.json`

**涉及文件**：
- `Services/DnaManagerService.cs` — 所有调用 `ScanTopology` 的方法
- `Tools/DnaManagerTools.cs` — 新增 `refresh_topology` 工具（可选）

---

### [ ] 3. SharedOrSoft 视界过滤真正实现

**问题**：`GetContentFiles` 注释说"返回与职责相关的内容文件"，但实现直接返回全部文件，跟 Current 级别一样。核心安全机制形同虚设。

**修复方向**：
- SharedOrSoft 只返回 `.dna/` 目录下的文件 + 职责声明中引用的文件
- 或只返回顶层目录的文件（不递归子目录）
- 或引入 `.dna/exports.md` 显式声明对外可见的文件列表
- 需要明确定义"职责相关文件"的具体规则

**涉及文件**：
- `Services/DnaManagerService.cs` — `GetContentFiles`

---

## P1 — 架构级缺陷（不解决严重限制实用性）

### [ ] 4. "当前模块"状态自动追踪

**问题**：`get_module_context` 需要 AI 手动传 `currentModule`，AI 传错或不传就绕过了视界隔离。

**修复方向**：
- 在 `.agentic-os/session-state.json` 中记录当前活跃模块
- 调用栈栈顶的 `ModuleName` 自动作为 currentModule
- `get_module_context` 的 `currentModule` 参数改为可选，默认从状态中读取
- 新增 `set_current_module` 工具，或在 `suspend_and_push` / `complete_and_pop` 时自动切换

**涉及文件**：
- `Services/TaskSchedulerService.cs` — 维护 session state
- `Services/DnaManagerService.cs` — `GetModuleContext` 默认读取当前模块
- `Tools/DnaManagerTools.cs` — 参数逻辑调整
- 新增 `Models/SessionState.cs`（可选）

---

### [ ] 5. 调用栈支持并行任务（树形栈）

**问题**：单线程 `List<TaskFrame>` 无法表达并行工作。现实中美术和程序可以同时推进。

**修复方向**：
- 从线性栈改为任务树（每个 TaskFrame 有 `ParentId` 和 `Children`）
- 支持同一层级多个任务并行执行（状态均为 Running）
- `suspend_and_push` 变为"在当前节点下创建子任务"
- `complete_and_pop` 变为"标记当前节点完成，检查兄弟节点是否全完成，是则恢复父节点"
- CLI `stack` 命令改为树形展示

**涉及文件**：
- `Models/TaskFrame.cs` — 新增 `ParentId`、`Children`
- `Models/TaskFrame.cs` — `CallStack` 从 List 改为 Tree
- `Services/TaskSchedulerService.cs` — 全部重写 push/pop 逻辑
- `Tools/TaskSchedulerTools.cs` — 工具描述更新
- `Cli/CliHandler.cs` — 树形展示

---

### [ ] 6. 并发安全（文件锁）

**问题**：多个 AI Agent 同时操作同一项目时，`call-stack.json` 和 `.dna/` 文件读写会冲突。

**修复方向**：
- 对 `call-stack.json` 读写加文件锁（`FileShare.None` 或 `.lock` 文件）
- 乐观锁方案：JSON 中加 `version` 字段，写入时检查版本一致性
- `.dna/` 文件写入加 advisory lock
- 考虑用 SQLite 替代纯 JSON 文件（单文件数据库，自带并发控制）

**涉及文件**：
- `Services/TaskSchedulerService.cs` — `LoadCallStack`、`SaveCallStack`
- `Services/WorkspaceService.cs` — 所有文件写入方法

---

### [ ] 7. `register_module` 自动生成 DNA 模板

**问题**：注册模块后只创建空 `.dna/` 目录，新用户不知道下一步做什么。

**修复方向**：
- 注册时自动生成模板文件：`architecture.md`、`pitfalls.md`、`dependencies.md`、`changelog.md`
- 支持 `moduleType` 参数（`code` / `art` / `design` / `doc` / `generic`），不同类型生成不同模板
- 代码类型的 architecture.md 预置 `## Public API` 段
- 美术类型预置 `## 资产规范` 段
- 策划类型预置 `## 交付契约` 段

**涉及文件**：
- `Services/DnaManagerService.cs` — `RegisterModule`
- `Models/ModuleNode.cs` — 新增 `ModuleType` 枚举（可选）
- `Tools/DnaManagerTools.cs` — `register_module` 新增 `moduleType` 参数
- 新增模板文件或内嵌模板字符串

---

## P2 — 实操级缺陷（不解决体验差）

### [ ] 8. 模块类型系统

**问题**：设计上区分代码/美术/策划模块，但 `ModuleNode` 没有 `Type` 字段，靠正则猜测 architecture.md 段落标题。

**修复方向**：
- `ModuleNode` 新增 `ModuleType` 属性
- `architecture.md` 元数据中新增 `type: code|art|design|doc|generic`
- 解析时读取 type 字段，回退到段落标题推断
- 视界过滤可以按类型做差异化处理

**涉及文件**：
- `Models/ModuleNode.cs`
- `Services/DnaManagerService.cs` — `ParseModuleNode`

---

### [ ] 9. 任务恢复条件自动检查

**问题**：`TaskFrame.ResumeCondition` 是纯文本，系统不会自动检查是否满足。

**修复方向**：
- 结构化恢复条件：`{ type: "module_complete", module: "Art/Characters" }`
- `complete_and_pop` 时自动扫描栈中所有挂起帧，检查恢复条件
- 条件满足时自动通知（返回消息中包含"可恢复的任务列表"）
- 保留纯文本条件作为 fallback（人工判断）

**涉及文件**：
- `Models/TaskFrame.cs` — 新增结构化 `ResumeCondition`
- `Services/TaskSchedulerService.cs` — `CompleteAndPop` 中增加自动检查逻辑

---

### [ ] 10. wip.md 与调用栈自动同步

**问题**：`wip.md` 和 `call-stack.json` 功能重叠但不同步，状态不一致会导致 AI 困惑。

**修复方向**：
- `suspend_and_push` 时自动将任务摘要写入对应模块的 `wip.md`
- `complete_and_pop` 时自动清理对应模块的 `wip.md` 相关条目
- 或者反过来：`wip.md` 作为只读视图，由调用栈自动生成
- 明确两者的职责边界：调用栈 = 程序状态，wip.md = 人可读的工作备忘

**涉及文件**：
- `Services/TaskSchedulerService.cs` — `SuspendAndPush`、`CompleteAndPop`
- `Services/WorkspaceService.cs` — 新增 `UpdateWipAsync`

---

### [ ] 11. Pitfall 索引路径去硬编码

**问题**：`WritePitfallAsync` 写死了 `coder/pitfall-index.md` 路径，通用项目不兼容。

**修复方向**：
- 索引路径改为 `.agentic-os/pitfall-index.md`
- 或通过配置文件 `.agentic-os/config.json` 自定义索引路径
- 路径不存在时自动创建而非静默跳过

**涉及文件**：
- `Services/WorkspaceService.cs` — `WritePitfallAsync`

---

## P3 — 长期演进

### [ ] 12. AI 行为约束强制力

**问题**：所有保护机制都是工具级的，AI 可以直接用 IDE 的文件读写绕过 MCP。

**演进方向**：
- 短期：在 Agent 的 System Prompt / Rules 中强制要求走 MCP 通道
- 中期：提供 `.cursor/rules` 或 `AGENTS.md` 模板，声明"操作此项目必须先调用 get_module_context"
- 长期：等待 MCP 协议支持"capability negotiation"，在协议层约束 Agent 行为
- 终极：文件系统层面的只读挂载（Agent 只能通过 MCP 写入）

---

### [ ] 13. 事件/通知机制

**问题**：一切操作都是 pull 模式，AI 必须主动查询才能知道状态变化。

**演进方向**：
- 模块 DNA 变更时生成事件日志（`.agentic-os/events.jsonl`）
- `complete_and_pop` 时自动检查并通知可恢复的任务
- MCP 协议的 notification 机制（如果 MCP 支持的话）

---

### [ ] 14. 多项目/多工作区支持

**问题**：当前假设一个 `projectRoot` 对应一个工作区。Monorepo 或跨仓库项目无法覆盖。

**演进方向**：
- 支持多 projectRoot 注册
- 跨项目依赖声明
- 全局拓扑图（多项目合并视图）

---

## 进度追踪

| # | 优先级 | 状态 | 简述 |
|---|--------|------|------|
| 1 | P0 | 待做 | 模块名支持多层路径 |
| 2 | P0 | 待做 | 拓扑扫描结果缓存 |
| 3 | P0 | 待做 | SharedOrSoft 视界过滤真正实现 |
| 4 | P1 | 待做 | "当前模块"状态自动追踪 |
| 5 | P1 | 待做 | 调用栈支持并行任务（树形栈） |
| 6 | P1 | 待做 | 并发安全（文件锁） |
| 7 | P1 | 待做 | register_module 自动生成 DNA 模板 |
| 8 | P2 | 待做 | 模块类型系统 |
| 9 | P2 | 待做 | 任务恢复条件自动检查 |
| 10 | P2 | 待做 | wip.md 与调用栈自动同步 |
| 11 | P2 | 待做 | Pitfall 索引路径去硬编码 |
| 12 | P3 | 待做 | AI 行为约束强制力 |
| 13 | P3 | 待做 | 事件/通知机制 |
| 14 | P3 | 待做 | 多项目/多工作区支持 |
