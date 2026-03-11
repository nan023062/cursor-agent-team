# Work In Progress — Coder Agent

> 跨会话任务续接。每次 Evolution 执行前先检查此文件。

---

## [Evolution] 全量同步 architecture.md 双优化

- **创建**: 2026-03-10
- **触发**: 下次执行 `@coder evolution` 时
- **优先级**: 中

### 背景

2026-03-10 对 Core.UGC 及其子模块批量优化了 architecture.md，修复了 `#TemplatePoor` + `#RuleMissing` 问题：
1. 在 `概述` 段末尾新增 `- **边界模式**: \`boundary: xxx\`` 机器标签
2. 将 `## Public API` 文本列表改为 \`\`\`csharp 代码签名块
3. 同步更新 AGENT.md：Init 强制扫描提取 API、Dev 禁止读依赖源码、边界守卫增加第 4 项、按需触发 API 变更行完善

模板（`templates/dna/architecture.md`）已同步更新，但 **存量程序集** 尚未全量覆盖。

### 待检查范围

Evolution 时对 coder-rules.mdc 架构树中所有带 `*` 的已注册程序集逐一检查，重点关注：

- [ ] XDTBaseService/Core/ 及子模块（Core, Pool, World, Profiler 等）
- [ ] XDTBaseService/Framework/
- [ ] XDTBaseService/Foundations/ 各子模块（ActionGraph, EventCenter, FlowGraph 等）
- [ ] XDTBaseService/Utility/
- [ ] XDTBaseService/Services/ 各子模块（Audio, Cache, Scene 等）
- [ ] 其他在 coder-rules.mdc 中注册但不属于 Core.UGC 的程序集

### 检查标准

对每个程序集的 `.dna/architecture.md` 验证：
1. `概述` 段内是否有 `- **边界模式**: \`boundary: ...\`` 行
2. `## Public API` 段是否为 \`\`\`csharp 签名块（而非纯文本列表）

### 执行动作

- 缺少字段 → 补充（参考 Core.UGC 子模块的现有写法）
- Public API 为文本列表 → 转换为 csharp 签名块
- 执行完毕后，在此任务下追加 `完成: YYYY-MM-DD` 并清理本条目

**完成: 2026-03-10** — 已补全 26 个 XDTBaseService 程序集的 `边界模式` 字段（全部为 `boundary: hard`），并将 27 个程序集的 Public API 从文本列表转换为 csharp 签名块（World 新增了 `## Public API` 章节）。
