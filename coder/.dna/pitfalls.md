# 智能体摩擦记录

标签: #FlowBlock #RuleMissing #TemplatePoor #CrossAgentGap

- `#FlowBlock` — 流程步骤走不通，需要绕路
- `#RuleMissing` — 没有覆盖的场景，临时自行决策
- `#TemplatePoor` — 模板字段/结构不满足需求
- `#CrossAgentGap` — 跨智能体协作时信息断层

---

- [2026-03-10] #RuleMissing Dev 阶段读取依赖程序集源码导致大量 Token 浪费
  - 场景: 日常开发中 AI 为了了解依赖程序集的接口，直接打开了依赖模块的源文件，产生大量不必要的 Token 开销；且 Init 阶段未强制要求扫描源码生成 architecture.md，导致 Public API 段长期为手写文本列表，不够精准。
  - 绕路: 临时约定「读 architecture.md，不读源码」，并手动批量补全 Public API csharp 签名块。
  - 建议（已落地）:
    1. **上下文加载协议** 新增强约束：依赖程序集接口只读 `architecture.md ## Public API`，禁止打开源文件
    2. **Init 流程**（新建/存量导入）：强制扫描源码提取 public 签名，以 csharp 块填入 architecture.md
    3. **Phase 2 边界守卫** 新增第 4 项：`public` 方法/类/属性变更时必须同步更新 architecture.md
    4. **按需触发表格**「公共 API 变更」：先更新 architecture.md，再同步 README

- [2026-03-10] #TemplatePoor architecture.md 模板缺少「边界模式」字段和 C# API 签名格式
  - 场景: 对 Core.UGC 及其所有子模块执行 coder-init 后，architecture.md 的 Public API 段为纯文本列表，缺少 `boundary` 字段声明。用户反馈：LLM 在长上下文中注意力涣散，容易对参数类型产生幻觉，且无法快速定位边界模式。
  - 绕路: 手动批量更新 9 个 architecture.md + 模板文件，新增 `- **边界模式**: \`boundary: hard\`` 字段，将 Public API 改为 \`\`\`csharp 代码签名块（只写签名不写实现，每行注释所属类）。
  - 建议: 模板已修正（`templates/dna/architecture.md`）。**Evolution 阶段应检查所有已注册程序集的 architecture.md，补充缺失的 `边界模式` 字段和将文本 API 转换为 csharp 签名块**（见 wip.md 中的 Evolution 任务）。

<!-- 条目格式:

- [YYYY-MM-DD] #标签 简述
  - 场景: 具体遇到了什么
  - 绕路: 怎么临时处理的
  - 建议: 应该怎么改智能体定义

同一标签 ≥3 次时，@meta scan 会建议升格为 AGENT.md 的流程/规则改进。
-->
