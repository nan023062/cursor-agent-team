# 游戏开发智能体团队

一套可移植的游戏开发 AI 协作团队，覆盖创意→设计→编程→美术→测试全流程。

**零配置**：复制到任意 Cursor 项目，调用 `@agent init` 即可开始工作。

---

## 安装

将 `gamedev/` 整个目录放入项目的 `.cursor/` 下：

```
你的项目/
└── .cursor/
    └── gamedev/           # ← 放这里，完成
```

首次使用任意智能体的 init 命令时，会自动创建该智能体的 `.cursor/rules/{agent}-rules.mdc`。无需手动配置。

---

## 快速开始

### 1. 立项

```
@creative vision              # 确定游戏愿景
@creative blueprint           # 规划里程碑
@designer arch                # 设计系统架构
```

### 2. 开发

```
@designer spec [功能名]        # 写功能规格
@coder init [路径]             # 初始化代码模块
@coder dev [路径] [需求]       # 开发迭代
@artist ta breakdown [需求]    # 拆解美术需求
```

### 3. 验证

```
@tester test [路径]            # 生成测试策略
@tester gate [版本号]          # 发布门禁
```

### 4. 进化

```
@coder evolution               # 代码记忆进化
@designer evolution            # 设计记忆进化
@meta sync                     # 全局审计
```

---

## 团队成员

| 智能体 | 职责 | 核心指令 |
|--------|------|---------|
| **Creative** | 愿景、调研、里程碑 | `vision` `blueprint` `research` `feedback` `brief` |
| **Designer** | 系统设计、数值、配置 | `arch` `concept` `spec` `balance` `config` `ux` |
| **Coder** | 代码实现、架构管理 | `init` `dev` `fixbug` |
| **Artist** | 美术规范、资源管理 | `ta` `ta breakdown` `concept` `check` `naming` |
| **Tester** | 测试、Bug、性能、门禁 | `test` `bug` `perf` `gate` `report` |
| **Meta-Agent** | 智能体工厂 | `new` `sync` `evolution` |

所有智能体都有 `evolution` 命令用于记忆进化。

---

## 工作原理

```
AGENT.md（做什么）
    ↓ 首次 init
.cursor/rules/{agent}-rules.mdc（项目路径 + 架构图 + 协作 + 约束）
    ↓ @meta sync
.cursor/commands/{agent}-{cmd}.md（Cursor 快捷指令）
    ↓ 日常使用
.dna/pitfalls.md（积累经验）
    ↓ evolution
.dna/architecture.md（升格为规则）
```

每个智能体通过 `.dna/` 记忆目录实现自进化：踩坑 → 记录 → 识别模式 → 升格为规则 → 自动遵循。

---

## 目录结构

```
gamedev/
├── meta-agent/            智能体工厂（创建 + 审计 + 进化）
├── creative/              创意智能体
├── designer/              策划智能体
├── coder/                 编程智能体
├── artist/                美术智能体
└── tester/                测试智能体
```

每个智能体目录结构：

```
{agent}/
├── AGENT.md               智能体定义（做什么、怎么做）
├── README.md              使用说明
├── .dna/
│   └── pitfalls.md        智能体自身的流程摩擦记录
├── protocols/             流程协议（可选）
└── templates/             模板文件（可选）
```

---

## 可移植性

本团队完全可移植，不含任何项目特有路径或配置：

- AGENT.md 零硬编码路径
- 项目路径由 `.cursor/rules/{agent}-rules.mdc` 管理，init 时自动创建
- 快捷指令由 `@meta sync` 自动生成
- 复制 `gamedev/` 到新项目 → 开始使用，无需任何修改
