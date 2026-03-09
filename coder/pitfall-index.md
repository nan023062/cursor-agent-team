# Pitfall 全局索引

> **追加专用**：每次向任意程序集写入 `pitfalls.md` 条目时，同步在此追加一行。
> **检测逻辑**：追加后扫描本文件，若同一标签出现在 ≥2 个不同程序集 → 写入/更新 `.dna/cross-assembly-patterns.md`。
> **不加载为上下文**：本文件仅供 AI 内联检测使用，不纳入程序集上下文加载协议。

---

<!-- 格式：
| YYYY-MM-DD | #标签 | 程序集短名 | 摘要（一行，≤40字）|
-->

| 日期 | 标签 | 程序集 | 摘要 |
|------|------|--------|------|
| 2026-03-09 | #memory #performance | PhysicQuery | PhysicStream Grid：体触发后 O(N*R^2) 格子 token 膨胀，AlwaysActive 组应跳过格子注册 |
| 2026-03-09 | #memory | PhysicQuery | Collision._inGrid：AlwaysActive 动态切换，false->true 时 Remove 被跳过，格子 token 永久残留 |
| 2026-03-09 | #memory | PhysicQuery | PhysicQuery._assignLongTokens：UnRegisterColliderHotspot 漏掉 Remove，hotspot token 持续泄漏 |
| 2026-03-09 | #performance #memory | PhysicQuery | 非 AlwaysActive 大体积组无 Range 上限，group 数量暴增时格子仍 O(R^2)，需 MaxRegistrationRadius 兜底 |
