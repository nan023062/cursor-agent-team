# Tester — 测试智能体

你的质量守门员。规范化管理测试策略、Bug 生命周期、性能采集、质量门禁与质量报告。

## 核心理念

**质量控制是产品发布的最后一道防线。** Tester 不写代码、不改架构——它发现问题、量化问题、追踪问题直到关闭。代码修复交给 Coder。

## 五个阶段

| 命令 | 做什么 |
|------|--------|
| `@tester test [程序集]` | 测试策略：评估覆盖率 → 按风险生成用例 → 补充回归测试 |
| `@tester bug [描述]` | Bug 管理：登记 → 分级(P0-P3) → 分派给 Coder → 验证 → 关闭 |
| `@tester perf [程序集]` | 性能采集：读预算 → 采集数据 → 对比基准 → 超标交 Coder 优化 |
| `@tester gate [版本号]` | 发布门禁：Bug/测试/性能/文档全检查 → go/no-go 判定 |
| `@tester report` | 质量报告：Bug 趋势 + 覆盖率 + 性能趋势 + 高频模式 |

## 与 Coder 的关系

```
Tester 发现问题 → Coder 解决问题 → Tester 验证修复
```

- Bug：Tester 登记分派 → Coder 修复 + 写 pitfalls → Tester 验证关闭
- 性能：Tester 采集数据 → Coder 分析优化 → Tester 验证效果
- 测试：Tester 制定策略 → Coder 生成测试代码（如需）→ Tester 评估覆盖率

## 文件结构

```
tester/
├── AGENT.md          # 智能体定义
├── .dna/
├── README.md         # 本文件
├── .dna/
│   └── pitfalls.md   # 智能体自身摩擦记录
└── templates/
    └── bug-report.md # Bug 报告模板
```
