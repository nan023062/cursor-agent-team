# DNA-MCP

**Agentic OS** — 将 AI 工作流调度从 AI 编程 IDE 对话上下文中解耦出来的通用程序层。

DNA-MCP 是一个 **MCP Server**（Model Context Protocol），同时也是一个 **CLI 工具**，为 AI Agent 提供三层核心能力：

| 层 | 职责 |
|----|------|
| **任务调度器** | 调用栈管理，支持任务挂起/恢复，跨会话保持状态 |
| **DNA 拓扑管理器** | 解析程序集 DAG，按视界分级过滤上下文，拓扑排序执行计划 |
| **工作区执行器** | Roslyn 精确提取 Public API，结构化写入 `.dna/` 记忆文件 |

---

## 快速开始

### 前置条件

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- 目标工程中至少有一个含 `.dna/` 目录的程序集（见[初始化](#初始化目标工程)）

### 编译

```powershell
# Debug（开发用）
dotnet build dna-mcp/dna-mcp.csproj

# Release（日常使用，推荐）
dotnet build dna-mcp/dna-mcp.csproj -c Release
```

编译产物位于：

```
dna-mcp/bin/Release/net8.0/dna-mcp.exe   ← Windows
dna-mcp/bin/Release/net8.0/dna-mcp       ← Linux / macOS
```

建议将该目录加入 `PATH`，之后直接用 `dna-mcp` 命令即可。

### 配置 MCP 客户端

DNA-MCP 兼容所有支持 MCP（Model Context Protocol）的 AI 编程工具。以下为各主流客户端的配置方式。

MCP Server 启动命令（所有客户端通用）：

```
command: dna-mcp
env:     DNA_MCP_PROJECT_ROOT=<你的项目根目录>
```

---

#### Cursor

在项目或全局的 `.cursor/mcp.json` 中添加：

```json
{
  "servers": {
    "dna-mcp": {
      "type": "stdio",
      "command": "dna-mcp",
      "env": {
        "DNA_MCP_PROJECT_ROOT": "C:\\path\\to\\your\\project"
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
    "dna-mcp": {
      "command": "dna-mcp",
      "env": {
        "DNA_MCP_PROJECT_ROOT": "/path/to/your/project"
      }
    }
  }
}
```

或使用命令行：

```bash
claude mcp add dna-mcp dna-mcp --env DNA_MCP_PROJECT_ROOT=/path/to/your/project
```

---

#### OpenAI Codex CLI

在 `~/.codex/config.toml` 或项目级 `codex.toml` 中添加：

```toml
[[mcp_servers]]
name    = "dna-mcp"
command = "dna-mcp"

[mcp_servers.env]
DNA_MCP_PROJECT_ROOT = "/path/to/your/project"
```

---

#### Windsurf

在 `~/.codeium/windsurf/mcp_config.json` 中添加：

```json
{
  "mcpServers": {
    "dna-mcp": {
      "command": "dna-mcp",
      "env": {
        "DNA_MCP_PROJECT_ROOT": "/path/to/your/project"
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
      "name": "dna-mcp",
      "command": "dna-mcp",
      "env": {
        "DNA_MCP_PROJECT_ROOT": "/path/to/your/project"
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
# 编译后直接运行（推荐）
dna-mcp cli <子命令> [参数...]

# 或使用完整路径（Windows 示例）
<安装目录>\dna-mcp.exe cli <子命令> [参数...]
```

> **提示**：设置环境变量 `DNA_MCP_PROJECT_ROOT` 后，所有命令的 `[root]` 参数均可省略。

### 子命令一览

#### `topology` — 扫描程序集拓扑图

```powershell
dna-mcp cli topology [projectRoot]
```

扫描目标工程中所有含 `.dna/` 目录的程序集，输出 DAG 拓扑图（按边界类型分组）及依赖关系。

```
  ╔═ DNA 拓扑图 — C:\MyProject

  共 12 个程序集，8 条依赖边

  ── ◈ Shared 边界（2 个） ──
  Core  @alice  → [Pool]
  Framework  @alice

  ── ◇ Soft 边界（3 个） ──
  Services/Audio  @bob  → [Core, EngineWrapper]
  ...

  ── 依赖关系 ──
  Services/Audio  →  Core
  ...
```

#### `stack` — 查看调用栈

```powershell
dna-mcp cli stack [projectRoot]
```

显示当前所有挂起的 AI 任务帧，包括任务描述、挂起原因、子任务完成进度和上次进展摘要。

```
  ╔═ 调用栈状态 — C:\MyProject

  栈深度: 2  |  更新时间: 2026-03-12 10:30 UTC

  ▶ 栈顶（当前执行）
    程序集:  Services/Audio
    任务:    实现空间音效混响系统
    状态:    Running
    子任务:  2/4 已完成
      ✓ 定义 IAudioMixer 接口
      ✓ 实现基础混音器
      ○ 集成 Wwise 空间化参数
      ○ 单元测试

  栈帧 #1（已挂起）
    程序集:  Core
    任务:    重构事件总线
    状态:    Suspended
    挂起原因: 等待 Services/Audio 完成混响接口定义
```

#### `context` — 程序集 DNA 摘要

```powershell
dna-mcp cli context <assemblyName> [projectRoot]
```

显示指定程序集的边界类型、依赖关系，以及 `.dna/` 各文件的存在状态和行数。

```
  ╔═ 程序集上下文摘要 — Core

  路径:     C:\MyProject\src\Core
  边界:     ◈ shared
  维护者:   @alice
  依赖:     Pool

  ── .dna/ 文件状态 ──
  ✓ architecture.md       142 行  (2026-03-11 09:20)
  ✓ pitfalls.md            38 行  (2026-03-10 15:44)
  ✓ dependencies.md        12 行  (2026-03-08 11:00)
  ✓ changelog.md           67 行  (2026-03-11 09:20)
  ○ wip.md                （不存在）
```

#### `plan` — 生成执行计划

```powershell
dna-mcp cli plan <assembly1,assembly2,...> [projectRoot]
```

对指定的程序集集合执行拓扑排序，输出正确的开发顺序（被依赖方优先）。

```powershell
dna-mcp cli plan Core,Services/Audio,Services/Scene
```

```
  ╔═ 执行计划 — Core, Services/Audio, Services/Scene

  按以下顺序依次开发（被依赖方优先）：

  1. Core
  2. Services/Audio
  3. Services/Scene

  执行顺序：Core → Services/Audio → Services/Scene
```

#### `validate` — 校验依赖关系

```powershell
dna-mcp cli validate <调用方> <被调用方> [projectRoot]
```

检查两个程序集之间的依赖是否在 `dependencies.md` 中声明，并验证边界访问权限。

```powershell
dna-mcp cli validate Services/Audio Core
# ✓ 依赖关系合法：Services/Audio → Core 已在 dependencies.md 中声明

dna-mcp cli validate Services/Scene Services/Audio
# ✗ 未声明依赖：Services/Scene 的 dependencies.md 中不包含 Services/Audio
```

#### `dna` — 读取完整 DNA 上下文

```powershell
dna-mcp cli dna <assemblyPath>
```

合并输出指定程序集 `.dna/` 目录下所有文件的完整内容，用于快速审阅记忆状态。

```powershell
dna-mcp cli dna C:\MyProject\src\Core
```

#### `help` — 显示帮助

```powershell
dna-mcp cli help
```

---

## 初始化目标工程

DNA-MCP 依赖 `.dna/` 目录来识别程序集。有两种方式初始化：

### 方式一：通过 MCP 工具（推荐）

在任意支持 MCP 的 AI IDE Agent 模式下：

```
register_assembly(assemblyPath: "C:\MyProject\src\Core")
```

自动创建 `.dna/` 目录及所有模板文件。

### 方式二：手动创建

```powershell
mkdir src\Core\.dna
```

然后在 `architecture.md` 中添加边界声明：

```markdown
## 元数据
- boundary: shared
- 维护者: @yourname

## Public API
（由 extract_public_api 工具自动填充）
```

在 `dependencies.md` 中声明依赖：

```markdown
## 依赖列表
- Pool
- UnityEngine
```

---

## MCP 工具参考

以下工具在任意支持 MCP 的 AI IDE Agent 模式下（Cursor、Claude Code、Codex、Windsurf、Continue.dev 等）可直接调用。所有工具的 `projectRoot` 参数均可省略（回退到 `DNA_MCP_PROJECT_ROOT` 环境变量）。

### DNA 拓扑管理器

| 工具 | 说明 |
|------|------|
| `get_topology` | 扫描并返回完整拓扑图（JSON） |
| `get_execution_plan` | 对指定程序集集合进行拓扑排序 |
| `get_assembly_context` | 按视界分级返回程序集上下文（物理过滤） |
| `validate_dependency` | 校验依赖关系合法性 |
| `register_assembly` | 初始化程序集 `.dna/` 目录 |

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
| `extract_public_api` | 用 Roslyn 精确提取 C# Public API 签名 |
| `write_pitfall` | 结构化写入踩坑记录到 `pitfalls.md` |
| `write_changelog` | 追加变更记录到 `changelog.md` |
| `read_dna` | 读取程序集完整 `.dna/` 上下文 |

---

## 上下文视界分级

`get_assembly_context` 根据程序集间的边界关系，对返回内容进行物理过滤：

| 访问级别 | 触发条件 | 可见内容 |
|----------|----------|----------|
| **Current** | 当前正在开发的程序集 | `.dna/` 全部 + 全部源码路径 |
| **SharedOrSoft** | Shared 层或 Soft 边界邻居 | `.dna/` 全部 + Public API 源文件路径 |
| **HardDependency** | Hard 边界依赖 | 仅 `architecture.md` 的 `## Public API` 段 |
| **Unlinked** | 无依赖关系的程序集 | 拦截，返回阻断消息 |

---

## 调用栈持久化

调用栈保存在目标工程根目录下：

```
{projectRoot}/.dna-mcp/call-stack.json
```

跨 AI IDE 会话、跨对话窗口均可恢复状态。AI 重启后通过 `get_call_stack` 即可找回上次的任务进度。

---

## 项目结构

```
dna-mcp/
├── dna-mcp.csproj
├── Program.cs                  # 入口：CLI 模式 vs MCP Server 模式分流
├── Cli/
│   └── CliHandler.cs           # CLI 子命令实现
├── Models/
│   ├── AssemblyNode.cs         # 程序集节点 + BoundaryMode
│   ├── TaskFrame.cs            # 调用栈帧 + CallStack
│   └── DnaContext.cs           # 上下文视界 + 拓扑结果模型
├── Services/
│   ├── ProjectConfig.cs        # projectRoot 解析（环境变量回退）
│   ├── DnaManagerService.cs    # 拓扑扫描、DAG 排序、视界过滤
│   ├── TaskSchedulerService.cs # 调用栈 push/pop、JSON 持久化
│   └── WorkspaceService.cs     # Roslyn API 提取、.dna/ 文件读写
└── Tools/
    ├── DnaManagerTools.cs      # MCP 工具：拓扑管理
    ├── TaskSchedulerTools.cs   # MCP 工具：任务调度
    └── WorkspaceTools.cs       # MCP 工具：工作区操作
```

---

## 将 dna-mcp 加入 PATH（推荐）

编译一次后，将 exe 所在目录加入系统 PATH，之后在任意终端直接使用 `dna-mcp` 命令：

**Windows（PowerShell）**

```powershell
# 编译 Release
dotnet build dna-mcp/dna-mcp.csproj -c Release

# 将输出目录加入当前会话 PATH（临时）
$env:PATH += ";$PWD\bin\Release\net8.0"

# 或永久加入用户 PATH
[Environment]::SetEnvironmentVariable(
    "PATH",
    $env:PATH + ";$PWD\bin\Release\net8.0",
    "User"
)
```

**Linux / macOS（bash/zsh）**

```bash
# 编译 Release
dotnet build dna-mcp/dna-mcp.csproj -c Release

# 加入 PATH（追加到 ~/.bashrc 或 ~/.zshrc）
export PATH="$PATH:$(pwd)/bin/Release/net8.0"
```

配置完成后，新开终端即可直接使用：

```powershell
dna-mcp cli topology
dna-mcp cli stack
dna-mcp cli help
```

---

## 环境变量

| 变量 | 说明 | 示例 |
|------|------|------|
| `DNA_MCP_PROJECT_ROOT` | 默认目标工程根目录，省略工具参数时使用 | `C:\MyProject\src` |
