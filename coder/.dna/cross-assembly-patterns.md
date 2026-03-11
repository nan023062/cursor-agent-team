# 跨程序集高频陷阱

> **自动聚合**：当同一标签在 ≥2 个不同程序集的 `pitfall-index.md` 中出现时，由 Phase 2 写 pitfall 流程内联冒泡至此。
> **消费方**：所有程序集上下文加载时必读（若文件非空）。
> **升格**：Evolution Phase 3 负责将成熟模式升格为 `architecture.md` 约束或 Rule；升格后可从本文件删除对应行。

---

<!-- 格式：
| 标签 | 模式摘要（跨集共性描述） | 涉及程序集（短名） | 最近触发 |
-->

| 标签 | 模式摘要 | 涉及程序集 | 最近触发 |
|------|---------|----------|---------|
| `#memory` | 池化对象未 Dispose/归还导致泄漏：UValue<T> 用完必须 Dispose；AddBuff 热路径 new List 产生 GC；PhysicQuery 格子 token 在动态标志位切换时未清理 (**Object + PhysicQuery 部分已升格为各自 architecture.md 约束**) | Object, Ugc.Buff, PhysicQuery | 2026-03-09 |
| `#logic` `#架构迁移` | 旧系统迁移残留 WIP：双路径初始化 / 旧 API 仍被路由 / TODO 桩未实现，导致运行时走旧路径或功能缺失。根因均为"新旧并存迁移期" | Ugc.Skill, Ugc.Buff, Unit, Ugc.State, Trigger, Blockly | 2026-03-09 |
