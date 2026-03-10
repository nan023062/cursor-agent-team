# 可移植 AI 智能体团队

**可移植的、IDE 无关的**、会自进化的 AI 协作团队框架。

**支持任意 AI 编程 IDE，零厂商锁定。**

> 每个智能体有自己的记忆（`.dna/`），从错误中学习（`pitfalls`），随时间进化规则（`evolution`）。元智能体可以按需创建新智能体。

---

## IDE 无关架构

本框架设计为支持**任意 AI 编程 IDE**：

| IDE | 状态 | 适配器 |
|-----|------|--------|
| Cursor | ✅ 已支持 | `adapters/cursor/` |
| Claude Code | 🔜 计划中 | `adapters/claude/` |
| Trae | 🔜 计划中 | `adapters/trae/` |
| GitHub Copilot | 🔜 计划中 | `adapters/copilot/` |

### 工作原理

```
┌─────────────────────────────────────────────────────────────┐
│                    可移植层 (IDE 无关)                        │
├─────────────────────────────────────────────────────────────┤
│  agent-manifest.yaml    ← 单一真相源                          │
│  AGENT.md               ← 智能体定义 (纯 Markdown)            │
│  .dna/                  ← 模块记忆 (纯 Markdown)              │
│  protocols/             ← 工作流定义                          │
│  templates/             ← 文件模板                            │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼ 适配器生成
┌─────────────────────────────────────────────────────────────┐
│                    IDE 特定层 (生成的)                        │
├─────────────────────────────────────────────────────────────┤
│  .cursor/rules/*.mdc    ← Cursor 规则                        │
│  .cursor/commands/*.md  ← Cursor 快捷指令                    │
│  CLAUDE.md              ← Claude Code 配置                   │
│  .trae/                 ← Trae 配置 (未来)                   │
└─────────────────────────────────────────────────────────────┘
```

---

## 与众不同之处

- **IDE 无关** — 核心定义在 `agent-manifest.yaml` 和 `.dna/` 中，使用纯 YAML/Markdown。IDE 适配器生成原生配置。
- **自进化记忆** — 智能体在 `.dna/pitfalls.md` 中记录踩坑。同一模式出现 3 次以上时，`evolution` 将其升格为 `.dna/architecture.md` 中的永久规则。团队越用越聪明。
- **零配置** — 复制一个目录到项目中。首次 `init` 自动创建一切。无需配置文件、环境变量或安装脚本。
- **元智能体造智能体** — `@meta new` 交互式生成一个包含 14 个标准模式的完整智能体，即刻可用。
- **完全可移植** — 无硬编码路径，无项目特有内容。任何项目、任何 AI IDE 都能用。

---

## 安装

```bash
# 将团队目录复制到项目根目录
git clone <repo-url> /你的项目/gamedev/

# 然后生成 IDE 特定配置
python gamedev/adapters/cursor/generate.py  # 用于 Cursor
```

完事。无依赖（适配器需要 PyYAML），无构建。

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

## 详细流程

```
gamedev/
├── agent-manifest.yaml         # 单一真相源 (IDE 无关)
├── {agent}/AGENT.md            # 智能体定义 (可移植)
│
│   ▼  adapters/cursor/generate.py
│
.cursor/rules/{agent}-rules.mdc # 生成的: Cursor 规则
.cursor/commands/{agent}-*.md   # 生成的: Cursor 快捷指令
│
│   ▼  日常工作
│
{module}/.dna/pitfalls.md       # 积累踩坑
│
│   ▼  @agent evolution
│
{module}/.dna/architecture.md   # 模式升格为永久规则
│
│   ▼  下次操作读取进化后的规则 — 循环
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
gamedev/                          # 可移植智能体团队
├── agent-manifest.yaml           # [新增] 所有智能体的单一真相源
├── adapters/                     # [新增] IDE 特定生成器
│   ├── cursor/generate.py        # 生成 .cursor/ 配置
│   ├── claude/                   # (未来) Claude Code 适配器
│   └── README.md                 # 适配器开发指南
├── meta-agent/                   # 智能体工厂 + 审计
│   ├── AGENT.md
│   ├── protocols/                # sync、scan、evolution
│   └── templates/                # 骨架模板、.dna/ 模板
├── creative/                     # 创意智能体
├── designer/                     # 策划智能体
├── coder/                        # 编程智能体
├── artist/                       # 美术智能体
└── tester/                       # 测试智能体
```

每个智能体：
```
{agent}/
├── AGENT.md                   # 智能体定义 (IDE 无关)
├── README.md                  # 使用说明
├── .dna/pitfalls.md           # 自身流程摩擦记录
├── protocols/                 # （可选）详细流程协议
└── templates/                 # （可选）带 YAML front matter 的文件模板
```

---

## `.dna/` 记忆体系

每个被管理的模块都有一个 `.dna/` 目录，包含 **IDE 无关** 的 Markdown 文件：

| 文件 | 用途 | 写入者 | 格式 |
|------|------|--------|------|
| `architecture.md` | 永久规则、边界、约束 | `evolution` | YAML front matter + Markdown |
| `pitfalls.md` | 原始踩坑记录 | 日常操作 | YAML front matter + Markdown |
| `changelog.md` | 变更历史 | 日常操作 | YAML front matter + Markdown |
| `dependencies.md` | 依赖白名单 | `init` / 按需 | YAML front matter + Markdown |
| `wip.md` | 进行中任务 | 会话续接 | YAML front matter + Markdown |

### YAML Front Matter

所有 `.dna/` 文件使用标准 YAML front matter 存储元数据：

```markdown
---
last_verified: 2026-03-10
maintainer: "@username"
boundary: hard
---

# 架构

...内容...
```

这种格式：
- 任何编程语言都能解析
- 任何 AI IDE 都能读取
- 兼容静态站点生成器
- 版本控制友好

进化循环：

```
工作 → 犯错 → 记录到 pitfalls
→ @agent evolution → 识别模式（同标签 >= 3 次）
→ 升格为 architecture 约束
→ 下次操作读取进化后的规则 → 错误更少 → 循环
```

---

## 生成 IDE 配置

修改 `agent-manifest.yaml` 后，重新生成 IDE 特定配置：

```bash
# 用于 Cursor
python gamedev/adapters/cursor/generate.py

# 预览模式（不写入文件）
python gamedev/adapters/cursor/generate.py --dry-run
```

---

## 许可

[MIT](LICENSE)

---

**[English / 英文文档](README.md)**
