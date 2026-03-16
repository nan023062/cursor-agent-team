# Agentic OS

**Agentic OS** — 面向 AI Agent 的工作区操作系统，将任务调度与上下文管控从 AI 对话上下文中解耦到确定性程序层。

Agentic OS 是一个 **MCP Server**（Model Context Protocol），同时也是一个 **CLI 工具** 和 **可视化监控界面**，为 AI Agent 提供三层核心能力：

| 层 | 职责 |
|----|------|
| **任务调度器** | 调用栈管理，支持任务挂起/恢复，跨会话保持状态 |
| **DNA 拓扑管理器** | 解析模块 DAG，按视界分级过滤上下文，拓扑排序执行计划 |
| **工作区执行器** | 结构化读写 `.dna/` 记忆文件，C# 模块可用 Roslyn 提取 Contract |

> **模块 = 任何类型的工作内容单元**。代码、美术资产、策划文档、音视频、配置表等均可作为模块，通过 `.dna/` 目录定义规则与记忆。Agentic OS 不限定模块类型——它管理的是工作区，不是代码。

---

## 快速开始

### 前置条件

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- 目标工程中至少有一个含 `.dna/` 目录的模块（见[初始化](#初始化目标工程)）

### 编译

```powershell
# Debug（开发用）
dotnet build agentic-os/agentic-os.sln

# Release（日常使用，推荐）
dotnet build agentic-os/agentic-os.sln -c Release
```

编译产物位于：

```
agentic-os/publish/agentic-os.exe   ← Windows
agentic-os/publish/agentic-os       ← Linux / macOS
```

建议将该目录加入 `PATH`，之后直接用 `agentic-os` 命令即可。

### 仪表盘（Web）

```powershell
agentic-os ui
# 或
agentic-os --ui
```

启动 Web 仪表盘（http://localhost:5050），自动打开浏览器，实时查看调用栈与 DNA 拓扑图。需事先设置环境变量 `AGENTIC_OS_PROJECT_ROOT`（或 `DNA_MCP_PROJECT_ROOT`）。

### 配置 MCP 客户端

Agentic OS 兼容所有支持 MCP（Model Context Protocol）的 AI 编程工具。以下为各主流客户端的配置方式。

MCP Server 启动命令（所有客户端通用）：

```
command: agentic-os
env:     AGENTIC_OS_PROJECT_ROOT=<你的项目根目录>
```

> 兼容旧配置：`DNA_MCP_PROJECT_ROOT` 仍可生效。

---

#### Cursor

在项目或全局的 `.cursor/mcp.json` 中添加：

```json
{
  "servers": {
    "agentic-os": {
      "type": "stdio",
      "command": "agentic-os",
      "env": {
        "AGENTIC_OS_PROJECT_ROOT": "C:\\path\\to\\your\\project"
      }
    }
  }
}
```

重启 Cursor 后，Agent 模式下即可调用所有 MCP 工具。

---

#### Claude Code

在项目根目录的 `.mcp.json` 中添加，或通过 `claude mcp add` 命令注册：

```json
{
  "mcpServers": {
    "agentic-os": {
      "command": "agentic-os",
      "env": {
        "AGENTIC_OS_PROJECT_ROOT": "/path/to/your/project"
      }
    }
  }
}
```

或使用命令行：

```bash
claude mcp add agentic-os agentic-os --env AGENTIC_OS_PROJECT_ROOT=/path/to/your/project
```

---

#### OpenAI Codex CLI

在 `~/.codex/config.toml` 或项目级 `codex.toml` 中添加：

```toml
[[mcp_servers]]
name    = "agentic-os"
command = "agentic-os"

[mcp_servers.env]
AGENTIC_OS_PROJECT_ROOT = "/path/to/your/project"
```

---

#### Windsurf

在 `~/.codeium/windsurf/mcp_config.json` 中添加：

```json
{
  "mcpServers": {
    "agentic-os": {
      "command": "agentic-os",
      "env": {
        "AGENTIC_OS_PROJECT_ROOT": "/path/to/your/project"
      }
    }
  }
}
```

---

#### Continue.dev

在 `~/.continue/config.json` 的 `mcpServers` 字段中添加：

```json
{
  "mcpServers": [
    {
      "name": "agentic-os",
      "command": "agentic-os",
      "env": {
        "AGENTIC_OS_PROJECT_ROOT": "/path/to/your/project"
      }
    }
  ]
}
```

---

重载 MCP 配置后，Agent 模式下即可直接调用所有 MCP 工具。

---

## CLI 用法

CLI 模式与 MCP Server 模式共享同一个二进制文件，通过第一个参数 `cli` 区分：

```powershell
agentic-os cli <子命令> [参数...]
```

> **提示**：设置环境变量 `AGENTIC_OS_PROJECT_ROOT` 后，所有命令的 `[root]` 参数均可省略。

### 子命令一览

#### `topology` — 扫描模块拓扑图

```powershell
agentic-os cli topology [projectRoot]
```

扫描目标工程中所有含 `.dna/` 目录的模块，输出 DAG 拓扑图（按边界类型分组）及依赖关系。

```
  ╔═ DNA 拓扑图 — C:\MyProject

  共 12 个模块，8 条依赖边

  ── ◈ Shared 边界（2 个） ──
  Core  @alice  → [Pool]
  Framework  @alice

  ── ◇ Soft 边界（3 个） ──
  Art/Characters  @bob  → [Core]
  ...

  ── 依赖关系 ──
  Art/Characters  →  Core
  ...
```

#### `stack` — 查看调用栈

```powershell
agentic-os cli stack [projectRoot]
```

显示当前所有挂起的 AI 任务帧，包括任务描述、挂起原因、子任务完成进度和上次进展摘要。

```
  ╔═ 调用栈状态 — C:\MyProject

  栈深度: 2  |  更新时间: 2026-03-12 10:30 UTC

  ▶ 栈顶（当前执行）
    模块:    Art/Characters
    任务:    整理角色资产命名规范
    状态:    Running
    子任务:  2/4 已完成
      ✓ 定义命名规则
      ✓ 整理现有资产清单
      ○ 批量重命名
      ○ 更新 identity.md

  栈帧 #1（已挂起）
    模块:    Core
    任务:    重构事件总线
    状态:    Suspended
    挂起原因: 等待 Art/Characters 完成资产规范定义
```

#### `context` — 模块 DNA 摘要

```powershell
agentic-os cli context <moduleName> [projectRoot]
```

显示指定模块的边界类型、依赖关系，以及 `.dna/` 各文件的存在状态和行数。

```
  ╔═ 模块上下文摘要 — Core

  路径:     C:\MyProject\src\Core
  边界:     ◈ shared
  维护者:   @alice
  依赖:     Pool

  ── .dna/ 文件状态 ──
  ✓ identity.md           142 行  (2026-03-11 09:20)
  ✓ lessons.md             38 行  (2026-03-10 15:44)
  ✓ links.md               12 行  (2026-03-08 11:00)
  ✓ history.md             67 行  (2026-03-11 09:20)
  ○ active.md             （不存在）
```

#### `plan` — 生成执行计划

```powershell
agentic-os cli plan <module1,module2,...> [projectRoot]
```

对指定的模块集合执行拓扑排序，输出正确的执行顺序（被依赖方优先）。

```powershell
agentic-os cli plan Core,Art/Characters,Design/Combat
```

```
  ╔═ 执行计划 — Core, Art/Characters, Design/Combat

  按以下顺序依次执行（被依赖方优先）：

  1. Core
  2. Art/Characters
  3. Design/Combat

  执行顺序：Core → Art/Characters → Design/Combat
```

#### `validate` — 校验依赖关系

```powershell
agentic-os cli validate <调用方> <被调用方> [projectRoot]
```

检查两个模块之间的依赖是否在 `links.md` 中声明，并验证边界访问权限。

```powershell
agentic-os cli validate Art/Characters Core
# ✓ 依赖关系合法：Art/Characters → Core 已在 links.md 中声明

agentic-os cli validate Design/Combat Art/Characters
# ✗ 未声明依赖：Design/Combat 的 links.md 中不包含 Art/Characters
```

#### `dna` — 读取完整 DNA 上下文

```powershell
agentic-os cli dna <modulePath>
```

合并输出指定模块 `.dna/` 目录下所有文件的完整内容，用于快速审阅记忆状态。

#### `help` — 显示帮助

```powershell
agentic-os cli help
```

---

## 初始化目标工程

Agentic OS 依赖 `.dna/` 目录来识别模块。有两种方式初始化：

### 方式一：通过 MCP 工具（推荐）

在任意支持 MCP 的 AI IDE Agent 模式下：

```
register_module(modulePath: "C:\MyProject\src\Core")
```

自动创建 `.dna/` 目录。

### 方式二：手动创建

```powershell
mkdir src\Core\.dna
```

然后在 `identity.md` 中添加模块声明：

```markdown
## 元数据
- **边界模式**: `boundary: shared`
- **维护者**: @yourname

## Contract
（对外暴露的接口/职责/资产规范等）
```

在 `links.md` 中声明依赖：

```markdown
## 依赖列表
- Pool
- Core
```

---

## MCP 工具参考

以下工具在任意支持 MCP 的 AI IDE Agent 模式下可直接调用。所有工具的 `projectRoot` 参数均可省略（回退到 `AGENTIC_OS_PROJECT_ROOT` 环境变量）。

### DNA 拓扑管理器

| 工具 | 说明 |
|------|------|
| `get_topology` | 扫描并返回完整拓扑图（JSON） |
| `get_execution_plan` | 对指定模块集合进行拓扑排序 |
| `get_module_context` | 按视界分级返回模块上下文（物理过滤） |
| `validate_dependency` | 校验依赖关系合法性 |
| `register_module` | 初始化模块 `.dna/` 目录 |

### 任务调度器

| 工具 | 说明 |
|------|------|
| `suspend_and_push` | 挂起当前任务，压入调用栈 |
| `complete_and_pop` | 完成栈顶任务，弹出并恢复上一帧 |
| `get_call_stack` | 读取当前调用栈状态 |
| `update_task_status` | 更新子任务完成状态 |

### 工作区执行器

| 工具 | 说明 |
|------|------|
| `extract_public_api` | 【C# 模块专用】用 Roslyn 精确提取 Contract 签名 |
| `write_lesson` | 结构化写入教训记录到 `lessons.md` |
| `write_history` | 追加变更记录到 `history.md` |
| `read_dna` | 读取模块完整 `.dna/` 上下文 |

---

## 上下文视界分级

`get_module_context` 根据模块间的边界关系，对返回内容进行物理过滤：

| 访问级别 | 触发条件 | 可见内容 |
|----------|----------|----------|
| **Current** | 当前正在操作的模块 | `.dna/` 全部 + 全部内容文件路径 |
| **SharedOrSoft** | Shared 层或 Soft 边界邻居 | `.dna/` 全部 + Contract 相关文件路径 |
| **HardDependency** | Hard 边界依赖 | 仅 `identity.md` 的 `## Contract` 段 |
| **Unlinked** | 无依赖关系的模块 | 拦截，返回阻断消息 |

> **Contract 段**统一使用 `## Contract` 标题（兼容旧的 `## Public API` / `## 交付契约` / `## 资产规范`）。

---

## 调用栈持久化

调用栈保存在目标工程根目录下：

```
{projectRoot}/.agentic-os/call-stack.json
```

跨 AI IDE 会话、跨对话窗口均可恢复状态。AI 重启后通过 `get_call_stack` 即可找回上次的任务进度。

---

## `.dna/` 记忆体系

每个模块（无论类型）都通过 `.dna/` 目录暴露结构化上下文给 AI：

```
{模块}/
├── .dna/
│   ├── identity.md          # 身份编码：定位、Contract、边界、Issues、设计决策
│   ├── lessons.md           # 教训记录（结构化标签）
│   ├── history.md           # 变更记录
│   ├── links.md             # 依赖白名单
│   └── active.md            # 进行中任务（跨会话续接）
└── [任意内容]               # 代码/美术/文档/配置/音视频...
```

| 文件 | 层级 | 写入频率 | 通用含义 |
|------|------|---------|---------|
| `identity.md` | L3 永久规则 | 低（仅升格时） | 模块是什么、对外暴露什么、约束是什么、有什么已知问题 |
| `lessons.md` | L1 原始经验 | 高（每次踩坑后） | 在这个模块里踩过的教训 |
| `history.md` | 变更记录 | 中（每次迭代后） | 变更历史 |
| `links.md` | 依赖声明 | 低（依赖变更时） | 依赖哪些其他模块 |
| `active.md` | 任务续接 | 中（跨会话任务） | 当前进行中的工作 |

---

## 项目结构

```
agentic-os/
├── kernel/                         # 内核（MCP Server + CLI）
│   ├── kernel.csproj
│   ├── Program.cs                  # 入口：CLI 模式 vs MCP Server 模式分流
│   ├── Cli/
│   │   └── CliHandler.cs           # CLI 子命令实现
│   ├── Models/
│   │   ├── ModuleNode.cs           # 模块节点 + BoundaryMode
│   │   ├── TaskFrame.cs            # 调用栈帧 + CallStack
│   │   └── DnaContext.cs           # 上下文视界 + 拓扑结果模型
│   ├── Services/
│   │   ├── ProjectConfig.cs        # projectRoot 解析（环境变量回退）
│   │   ├── DnaManagerService.cs    # 拓扑扫描、DAG 排序、视界过滤
│   │   ├── TaskSchedulerService.cs # 调用栈 push/pop、JSON 持久化
│   │   └── WorkspaceService.cs     # Roslyn API 提取（C# 专用）、.dna/ 文件读写
│   └── Tools/
│       ├── DnaManagerTools.cs      # MCP 工具：拓扑管理
│       ├── TaskSchedulerTools.cs   # MCP 工具：任务调度
│       └── WorkspaceTools.cs       # MCP 工具：工作区操作
├── dashboard/                      # 仪表盘（ASP.NET Core Web，端口 5050）
│   └── dashboard.csproj
├── templates/                      # 通用模板
└── agentic-os.sln
```

---

## 将 agentic-os 加入 PATH（推荐）

编译一次后，将 exe 所在目录加入系统 PATH，之后在任意终端直接使用 `agentic-os` 命令：

**Windows（PowerShell）**

```powershell
dotnet build agentic-os/agentic-os.sln -c Release

# 临时
$env:PATH += ";$PWD\agentic-os\publish"

# 永久
[Environment]::SetEnvironmentVariable(
    "PATH",
    $env:PATH + ";$PWD\agentic-os\publish",
    "User"
)
```

**Linux / macOS（bash/zsh）**

```bash
dotnet build agentic-os/agentic-os.sln -c Release
export PATH="$PATH:$(pwd)/agentic-os/publish"
```

配置完成后，新开终端即可直接使用：

```powershell
agentic-os cli topology
agentic-os cli stack
agentic-os cli help
```

---

## 环境变量

| 变量 | 说明 | 示例 |
|------|------|------|
| `AGENTIC_OS_PROJECT_ROOT` | 默认目标工程根目录，省略工具参数时使用 | `C:\MyProject` |
| `DNA_MCP_PROJECT_ROOT` | 兼容旧配置，`AGENTIC_OS_PROJECT_ROOT` 未设置时回退 | `C:\MyProject` |
