# Meta-Agent — 智能体缔造者

交互式创建拥有独立记忆和自进化能力的新智能体。

**IDE 无关设计** — 不绑定任何 IDE 或项目类型。做游戏、做电影、做产品、做研究，都可以。

> **定位**：Meta-Agent 是 **智能体工厂**。它定义了自进化的核心模式（记忆→提炼→规则），并通过标准化骨架模板将这些模式实例化为具体领域的智能体。生成的智能体独立运行，不依赖 Meta-Agent。

---

## 安装

**一步安装**：将 `meta-agent/` 放入智能体团队目录即可。

```
你的项目/
├── gamedev/                    # 智能体团队目录
│   ├── agent-manifest.yaml     # 统一配置（IDE 无关）
│   ├── adapters/               # IDE 适配器
│   │   └── cursor/generate.py
│   ├── meta-agent/             # ← 放这里
│   ├── coder/                  #    生成的智能体会出现在这里
│   ├── designer/
│   └── ...
└── .cursor/                    # 生成的 IDE 配置（由适配器生成）
    ├── rules/
    └── commands/
```

无需修改任何文件。所有路径基于约定自动解析：

| 路径 | 指向 | 约定 |
|------|------|------|
| `../` | 兄弟智能体 | meta-agent 与其他 agent 同级 |
| `../agent-manifest.yaml` | 统一配置 | IDE 无关的智能体描述 |
| `../adapters/` | IDE 适配器 | 生成 IDE 特定配置 |

---

## 快速开始

### 1. 创建第一个智能体

```
@meta new coder
```

交互式采集要素后自动生成：
- `../coder/AGENT.md` — 智能体定义（IDE 无关）
- `../coder/.dna/pitfalls.md` — 记忆文件
- `../agent-manifest.yaml` — 更新配置

然后运行适配器生成 IDE 配置：

```bash
python gamedev/adapters/cursor/generate.py
```

### 2. 全局审计

```
@meta sync
```

一次性完成：
- 所有 agent 健康检查（.dna/ 过期/膨胀/缺失）
- manifest 一致性检查
- 建议重新生成 IDE 配置

### 3. 进化所有 agent

```
@meta evolution
```

扫描所有 agent 的 .dna/ 记忆，识别高频教训，建议升格为规则。

---

## 指令速查

| 指令 | 说明 |
|------|------|
| `@meta new [名称]` | 交互式创建新智能体 |
| `@meta sync` | 全局审计 + manifest 检查 |
| `@meta evolution` | 进化所有 agent |

---

## 创建新智能体需要的要素

| 要素 | 说明 | 示例 |
|------|------|------|
| **智能体名称** | 英文小写 | `coder` |
| **一句话定位** | 做什么 | 代码工程化 |
| **管辖范围** | 管理哪些目录 | `src/**` |
| **核心流程** | 3-6 个命令 | init → dev → evolution |
| **职责边界** | 做什么 / 不做什么 | 管代码，不管需求 |
| **交付契约** | 消费谁 / 产出给谁 | 消费 designer spec |
| **质量红线** | 绝对禁止的事 | 禁止越界修改 |

---

## 生成的智能体自动包含 13 个通用模式

| # | 模式 | 说明 |
|---|------|------|
| 1 | 元数据头 | name / description |
| 2 | 设计思想 | 领域核心原则 |
| 3 | 职责边界 | 与其他智能体的分工 |
| 4 | 触发 | 指令表 |
| 5 | 管理范围 | 管辖目录，指向 manifest |
| 6 | 记忆结构 | `.dna/` 标准化目录 |
| 7 | 上下文加载协议 | 操作前必读文件表 |
| 8 | 交付契约 | 输入/输出声明 |
| 9 | Phase 分阶段流程 | 每个命令一个 Phase |
| 10 | Evolution 阶段 | 六步框架 + 成长闭环 |
| 11 | 硬约束段 | 不可违反的红线 |
| 12 | 自进化钩子 | 4 种流程摩擦标签 |
| 13 | 智能体 .dna/ | 自身摩擦记录 |

---

## IDE 适配器

智能体定义与 IDE 配置分离：

| 层级 | 文件 | 职责 |
|------|------|------|
| **可移植层** | `agent-manifest.yaml` | 统一描述所有智能体 |
| **可移植层** | `{agent}/AGENT.md` | 智能体定义 |
| **可移植层** | `{agent}/.dna/` | 智能体记忆 |
| **生成层** | `.cursor/rules/*.mdc` | Cursor 规则 |
| **生成层** | `.cursor/commands/*.md` | Cursor 指令 |

运行适配器：

```bash
# 生成 Cursor 配置
python gamedev/adapters/cursor/generate.py

# 预览模式
python gamedev/adapters/cursor/generate.py --dry-run
```

---

## 目录结构

```
meta-agent/
├── AGENT.md                      智能体定义
├── README.md                     本文件
├── protocols/
│   ├── sync.md                   同步协议（审计 + manifest 检查）
│   ├── scan.md                   扫描协议（被 sync 调用）
│   └── evolution.md              进化协议（结构补齐 + 升格）
└── templates/
    ├── agent-skeleton.md         AGENT.md 骨架模板（13 个通用模式）
    ├── command.md                快捷指令模板
    └── dna/                      .dna/ 记忆文件模板
        ├── architecture.md
        ├── pitfalls.md
        ├── changelog.md
        └── dependencies.md
```

---

## 文档索引

| 文档 | 内容 |
|------|------|
| [AGENT.md](AGENT.md) | 完整流程定义 + 设计架构 |
| [protocols/sync.md](protocols/sync.md) | 同步协议详细步骤 |
| [protocols/scan.md](protocols/scan.md) | 扫描协议详细步骤 |
| [protocols/evolution.md](protocols/evolution.md) | 进化协议详细步骤 |
| [templates/](templates/) | 骨架模板 + .dna/ 模板 |
