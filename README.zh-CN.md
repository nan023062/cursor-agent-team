# Cursor Agent Team

可移植的、会自进化的 AI 协作团队框架，基于 [Cursor IDE](https://cursor.com)。

**零配置，复制即用。**

> 每个智能体有自己的记忆（`.dna/`），从错误中学习（`pitfalls`），随时间进化规则（`evolution`）。元智能体可以按需创建新智能体。

---

## 与众不同之处

- **自进化记忆** — 智能体在 `.dna/pitfalls.md` 中记录踩坑。同一模式出现 3 次以上时，`evolution` 将其升格为 `.dna/architecture.md` 中的永久规则。团队越用越聪明。
- **零配置** — 复制一个目录到项目中。首次 `init` 自动创建一切。无需配置文件、环境变量或安装脚本。
- **元智能体造智能体** — `@meta new` 交互式生成一个包含 14 个标准模式的完整智能体，即刻可用。
- **精准上下文注入** — 每个智能体的 `.mdc` 规则文件使用 Cursor 的 glob 机制，只在编辑相关文件时激活。零噪音。
- **完全可移植** — 无硬编码路径，无项目特有内容。任何 Cursor 项目都能用。

---

## 安装

```bash
# 将团队目录复制到你的项目
git clone <repo-url> /你的项目/.cursor/gamedev/
```

完事。无依赖，无构建。

---

## 快速开始

```bash
# 1. 初始化任意智能体 — rule 文件自动创建
@coder init src/

# 2. 开始干活
@coder dev src/auth "实现登录"

# 3. 一段时间后，进化 — 把教训提炼为规则
@coder evolution

# 4. 全局审计
@meta sync
```

---

## 工作原理

```
AGENT.md                        # 智能体做什么（可移植，无项目路径）
    |
    v  首次 init
.cursor/rules/{agent}-rules.mdc # 项目路径 + 架构图 + 约束
    |
    v  @meta sync
.cursor/commands/{agent}-*.md   # Cursor 快捷指令
    |
    v  日常工作
.dna/pitfalls.md                # 积累踩坑
    |
    v  evolution
.dna/architecture.md            # 模式升格为永久规则
    |
    v  下次操作读取进化后的规则 — 循环
```

---

## 内置：游戏开发团队

`gamedev/` 目录包含一个完整的游戏开发团队：

| 智能体 | 职责 | 核心指令 |
|--------|------|---------|
| **Creative** | 愿景、调研、里程碑 | `init` `vision` `blueprint` `research` |
| **Designer** | 系统设计、数值、配置 | `init` `arch` `concept` `spec` `balance` |
| **Coder** | 代码实现、架构管理 | `init` `dev` `fixbug` |
| **Artist** | 美术规范、资源管理 | `init` `ta` `concept` `check` `naming` |
| **Tester** | 测试、Bug、性能、门禁 | `init` `test` `bug` `perf` `gate` |
| **Meta-Agent** | 智能体工厂 | `new` `sync` `evolution` |

所有智能体都有 `evolution` 命令用于记忆进化。

---

## 创建你自己的团队

不做游戏？用元智能体构建任意团队：

```bash
@meta new frontend    # 交互式创建前端智能体
@meta new backend     # 创建后端智能体
@meta new devops      # 创建运维智能体
@meta sync            # 生成所有 rule 文件和快捷指令
```

元智能体会询问管辖范围、命令、职责和质量红线，然后生成包含 14 个标准模式的完整 `AGENT.md`。

---

## 目录结构

```
gamedev/                       # 复制到 .cursor/ 下
├── meta-agent/                # 智能体工厂 + 审计
│   ├── AGENT.md
│   ├── protocols/             # sync、scan、evolution
│   └── templates/             # 骨架模板、.dna/ 模板
├── creative/                  # 创意智能体
├── designer/                  # 策划智能体
├── coder/                     # 编程智能体
├── artist/                    # 美术智能体
└── tester/                    # 测试智能体
```

每个智能体：
```
{agent}/
├── AGENT.md                   # 智能体定义
├── README.md                  # 使用说明
├── .dna/pitfalls.md           # 自身流程摩擦记录
├── protocols/                 # （可选）详细流程协议
└── templates/                 # （可选）文件模板
```

---

## `.dna/` 记忆体系

每个被管理的模块都有一个 `.dna/` 目录：

| 文件 | 用途 | 写入者 |
|------|------|--------|
| `architecture.md` | 永久规则、边界、约束 | `evolution`（从 pitfalls 升格） |
| `pitfalls.md` | 原始踩坑记录 | 日常操作 |
| `changelog.md` | 变更历史 | 日常操作 |
| `dependencies.md` | 依赖白名单 | `init` / 按需 |

进化循环：

```
工作 → 犯错 → 记录到 pitfalls
→ @agent evolution → 识别模式（同标签 >= 3 次）
→ 升格为 architecture 约束
→ 下次操作读取进化后的规则 → 错误更少 → 循环
```

---

## DNA-MCP Server

`dna-mcp/` 目录包含 **Agentic OS** — 一个 C# MCP Server，将调度、拓扑管理和工作区操作从 AI 的上下文窗口中卸载到确定性程序层。

```
dna-mcp/                       # MCP Server + CLI 工具
├── dna-mcp.csproj
├── Cli/                       # CLI：dna-mcp cli <命令>
├── Services/                  # 任务调度器、DNA 管理器、工作区执行器
└── Tools/                     # MCP 工具定义
```

**[DNA-MCP 使用文档 →](dna-mcp/README.md)**

---

## 许可

[MIT](LICENSE)

---

**[English / 英文文档](README.md)**
