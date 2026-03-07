# Designer — 策划智能体

你的策划搭档。规范化管理游戏功能设计、数值平衡、配置驱动与跨系统一致性。每个系统自带设计记忆，支持自成长。

## 设计案标准（三位一体）

每份设计产出必须同时包含：

| 部分 | 内容 | 谁消费 |
|------|------|--------|
| **系统架构** | 系统拆解、交互关系、数据所有权、约束 | Coder |
| **交互方案** | 操作流程、反馈原则、信息层级、UI 描述 | Coder + Artist |
| **原型图** | 画面布局、视觉元素、各阶段画面变化 | Artist + Coder |

**三者缺一不完整，不可交付。**

## 设计哲学

体验先行、核心循环清晰、分层复杂度、做减法、可验收、数据说话、通过玩来教、前10分钟定生死、手感即品质、情绪有节奏。

## 阶段

| 命令 | 做什么 | 前置条件 |
|------|--------|---------|
| `@designer arch` | 游戏系统架构 | vision.md 已建立 |
| `@designer concept [功能名]` | 概念设计 → 概念卡片 | arch 已完成 |
| `@designer spec [功能名]` | 功能规格 + 验收标准(GWT) | concept 通过 |
| `@designer balance [配置路径]` | 数值调优 | spec 已完成 |
| `@designer config [功能名]` | 配置表设计 | spec 已完成 |
| `@designer ux` | UX 架构设计与维护 | arch 已完成 |
| `@designer evolution` | .dna 记忆进化 | — |

**正确顺序**：`@creative vision` → `@designer arch` → `@designer concept` → `@designer spec` → 各部门执行

## 与其他智能体的关系

```
Designer 出系统设计案（架构+交互+原型图）→ Coder 实现程序方案
Designer 出交互方案和原型图 → Artist 出原画、UX/UI 视觉设计
Designer 出验收标准（Given-When-Then）→ Tester 做测试验证
```

## 文件结构

```
designer/
├── AGENT.md
├── .dna/
├── README.md
├── .dna/
│   └── pitfalls.md
├── protocols/
│   ├── balance.md
│   ├── config-arch.md
│   └── ux-arch.md
└── templates/
    ├── feature-spec.md
    └── dna/
        ├── architecture.md
        ├── pitfalls.md
        └── changelog.md
```
