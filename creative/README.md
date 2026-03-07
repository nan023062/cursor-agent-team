# Creative — 创意智能体

你的创意中枢。调研市场、采集用户反馈、确定设计方向、规划游戏蓝图。

## 创意记忆

> 实际路径以 `creative-rules.mdc → 智能体管辖路径` 为准。

```
<managed_root>/.dna/
├── vision.md       # 游戏愿景（设计支柱 + 红线）
├── pitfalls.md     # 创意教训（方向误判、砍掉的功能、市场误读）
└── changelog.md    # 创意决策变更记录
```

成长闭环：做决策 → 验证 → 对了保持 / 错了记 pitfalls → evolution 时升级为红线

## 角色定位

```
Creative（做什么、为什么做）
├── Designer（怎么设计）
├── Coder（怎么实现）
├── Artist（怎么呈现）
└── Tester（做得对不对）
```

## 命令与使用频率

| 命令 | 做什么 | 频率 |
|------|--------|------|
| `@creative research` | 市场调研、竞品分析 | 立项时 1 次，之后每半年 |
| `@creative vision` | 游戏愿景、设计支柱、红线 | 立项时 1 次，之后几乎不动 |
| `@creative blueprint` | 里程碑规划、打磨预算 | 立项时 1 次，每个 M 结束后微调 |
| `@creative feedback` | 用户反馈采集与洞察 | 有测试数据时（试玩后 / EA 后） |
| `@creative brief` | 创意简报 → 分派给各部门 | 每个里程碑开始时 |
| `@creative evolution` | .dna 记忆进化 | 每个里程碑结束时 |

**典型节奏**：立项期密集（每天），进入开发后每个里程碑首尾各用一次，中间几乎不碰。

## 文件结构

```
creative/
├── AGENT.md          # 智能体定义
├── .dna/
├── README.md         # 本文件
└── .dna/
    └── pitfalls.md   # 智能体自身摩擦记录
```
