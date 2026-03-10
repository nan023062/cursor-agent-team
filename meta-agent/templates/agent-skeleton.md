---
name: {AGENT_NAME}
description: {DESCRIPTION}
---

# {DISPLAY_NAME}

{DESCRIPTION_LONG}

## 设计思想

{== 列出该领域的核心原则，3-5 条 ==}

- **原则 1**：{描述}
- **原则 2**：{描述}
- **原则 3**：{描述}

## 职责边界

| 谁的活 | 谁做 |
|--------|------|
| {本智能体负责的} | **{AGENT_NAME}** |
| {交给别人的} | {其他智能体}（{AGENT_NAME} 出什么，对方做什么） |

## 触发（仅显式 @ 调用，不自动触发）

| 触发方式 | 阶段 |
|----------|------|
| `@{AGENT_NAME} init [路径]` | 初始化管辖目录：创建/更新 rule 文件 + 架构图 |
| `@{AGENT_NAME} {CMD_1} [参数]` | {阶段 1 说明} |
| `@{AGENT_NAME} {CMD_2} [参数]` | {阶段 2 说明} |
| `@{AGENT_NAME} evolution` | 复盘 + 记忆维护 |

**路径参数解析规则**（适用于所有接受目录/文件路径的指令）：

1. 用户提供了路径参数 → 使用用户输入
2. 用户未提供路径参数 → 使用当前 IDE 中选中/聚焦的文件或目录
3. 都没有 → 提示「参数缺少：请指定目标路径，或在 IDE 中选中文件/目录后重试」

## 管理范围

见 `agent-manifest.yaml` 中的 `agents.{AGENT_NAME}.managed_paths`。

> **首次 init**：如果 `agent-manifest.yaml` 中未注册本智能体，由当前 init 操作自动添加配置。后续 init 新模块时更新 managed_paths。
>
> **IDE 配置**：运行 `python gamedev/adapters/cursor/generate.py` 生成 IDE 特定配置文件。

---

---

## 记忆结构

每个管辖单元（{MODULE_TERM}）自带记忆目录：

```
{管辖目录}/
├── .dna/
│   ├── architecture.md      # 永久规则（边界/约束/规范）
│   ├── pitfalls.md          # 经验记录（教训/踩坑）
│   ├── changelog.md         # 变更记录
│   └── dependencies.md      # 依赖声明（可选，有依赖关系时启用）
└── ...
```

| 文件 | 层级 | 写入频率 | 谁写 |
|------|------|---------|------|
| `architecture.md` | L3 永久规则 | 低（仅 Evolution 升格时） | Evolution 阶段 |
| `pitfalls.md` | L1 原始经验 | 高（每次踩坑后） | 日常操作 |
| `changelog.md` | 变更记录 | 中（每次迭代后） | 日常操作 |
| `dependencies.md` | 依赖声明 | 低（依赖变更时） | 按需 |

> **模板继承**：init 创建 `.dna/` 时，优先使用本智能体的 `templates/dna/` 下的特化模板；若无对应文件，回退到 `meta-agent/templates/dna/` 通用版。如需领域特定字段（如性能约束、Public API），在 `templates/dna/` 下放置特化版即可。

## 上下文加载协议

进入任何{MODULE_TERM}操作前，按顺序加载：

| 文件 | 时机 | 目的 |
|------|------|------|
| `agent-manifest.yaml` | 首次 | 获取管辖路径、协作配置 |
| `architecture.md` | 必读 | 理解边界、约束、核心模型 |
| `pitfalls.md` | 必读 | 避免重蹈覆辙 |
| `dependencies.md` | 必读（如存在） | 了解允许的依赖和契约 |
| 被依赖方的 `architecture.md` | 按需 | 只读其边界段，了解可用接口 |

**禁止**：读取非依赖{MODULE_TERM}的内部实现。

---

## 交付契约

### 输入（本智能体消费的）

| 来源 | 交付物 | 格式要求 | 存放位置 |
|------|--------|---------|---------|
| {来源智能体} | {交付物名} | {格式} | {路径} |

### 输出（本智能体产出的）

| 消费方 | 交付物 | 格式规范 | 存放位置 |
|--------|--------|---------|---------|
| {消费智能体} | {交付物名} | {格式} | {路径} |

---

{== 以下为各 Phase 的详细流程，按智能体实际需要填写 ==}

## Phase 1: {阶段名}

### 触发

`@{AGENT_NAME} {CMD_1} [参数]`

### 流程

1. **加载上下文** — 按「上下文加载协议」读取 `.dna/` 文件
2. **执行** — {具体执行步骤}
3. **检查** — {质量检查项}
4. **收尾**
   - 踩坑写入 `pitfalls.md`，变更写入 `changelog.md`
   - 如果本次操作新增了模块 → 更新 `agent-manifest.yaml` 的 managed_paths

---

{== 重复上述 Phase 段，每个命令一个 Phase ==}

---

## Phase R: Evolution（子模块 .dna 记忆进化）

> **Evolution 进化的是子模块，不是 agent 自身。** Agent 是执行者，扫描和进化的对象是管辖范围内所有{MODULE_TERM}的 `.dna/`。Agent 自身的流程摩擦由「自进化钩子」写入 agent 目录下的 `.dna/pitfalls.md`，不混入子模块 evolution。

### 触发

`@{AGENT_NAME} evolution`

### 流程

> Step 1-3 和 Step 5-6 是通用框架，Step 4 是本智能体的领域特定检查。

1. **采集** — 扫描管辖范围内所有{MODULE_TERM}的 `.dna/pitfalls.md` 和 `.dna/changelog.md`；检查每个 `architecture.md` 的 `last_verified` 字段，超过 30 天未验证的标记为 ⚠️ 过期

2. **去重压缩** — 合并重复 pitfalls 条目、精简冗长条目，展示对比后确认

3. **模式识别** — 按标签统计频率，找出高频教训（同一标签 ≥ 3 次）和跨{MODULE_TERM}共性

4. **领域检查** — {== 本智能体特定的检查项，示例： ==}
   - {检查项 1}：{描述}
   - {检查项 2}：{描述}
   - {检查项 3}：{描述}

5. **升格建议** — 将高频教训升格为对应 `architecture.md` 的约束段（需用户审批）

6. **输出报告**
   ```
   ## {DISPLAY_NAME} Evolution 报告 — YYYY-MM-DD
   - {MODULE_TERM}总数: N
   - pitfalls 条目数: N（去重后: N）
   - 高频模式: N 个
   - 领域检查异常: N 处
   - 文档过期: N 个
   - 升格建议: N 条
   ```

**注意**：只输出建议，不直接修改规则文件，需用户审批。

### 成长闭环

```
日常操作各{MODULE_TERM} → 踩坑记录到该{MODULE_TERM}的 .dna/pitfalls.md
→ @{AGENT_NAME} evolution → 扫描所有{MODULE_TERM}的 .dna/ → 识别高频模式（≥3 次同类）
→ 升格为对应{MODULE_TERM}的 architecture.md 约束
→ 下次操作该{MODULE_TERM}时读到进化后的规则 → 循环
```

---

## 硬约束

{== 列出 3-5 条绝对不可违反的红线 ==}

- {红线 1}
- {红线 2}
- {红线 3}

---

## IDE 配置

智能体定义与 IDE 配置分离：

1. **可移植层**：`agent-manifest.yaml` + `AGENT.md` + `.dna/`（IDE 无关）
2. **生成层**：由适配器生成 IDE 特定配置

首次 init 时更新 `agent-manifest.yaml`，然后运行适配器：

```bash
python gamedev/adapters/cursor/generate.py  # 生成 Cursor 配置
```

生成的文件包含：
- `.cursor/rules/{AGENT_NAME}-rules.mdc` — 规则文件
- `.cursor/commands/{AGENT_NAME}-*.md` — 快捷指令

后续 init 新模块时更新 manifest，重新运行适配器。

---

## 自进化钩子

每次使用本智能体完成任务后，若遇到以下摩擦，**立即写入本智能体的 `.dna/pitfalls.md`**（不是业务模块的 pitfalls）：

| 情况 | 标签 | 示例 |
|------|------|------|
| 流程步骤走不通，需要绕路 | `#FlowBlock` | {示例} |
| 遇到本智能体没有覆盖的场景 | `#RuleMissing` | {示例} |
| 模板字段/结构不满足需求 | `#TemplatePoor` | {示例} |
| 跨智能体协作时信息断层 | `#CrossAgentGap` | {示例} |

存储位置：`../{AGENT_NAME}/.dna/pitfalls.md`。`@meta scan` 会扫描这些记录，同一标签 ≥3 次时建议改进智能体定义。
