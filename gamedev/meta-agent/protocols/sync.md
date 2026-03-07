# 同步协议（诊断 + Rules + Commands）

> `@meta sync` 时执行。合并了原 `@meta health` 的全部职责。一次性完成智能体健康诊断、规则文件（`.mdc`）和快捷指令（`.md`）的生成、比对、一致性检查。

## 背景

- **Agent 健康**：每个智能体的 `.dna/` 目录记录了永久规则、经验教训和变更历史，需要定期检查过期和膨胀。
- **Rules**：Cursor 从 `.cursor/rules` 加载 `.mdc` 规则文件，控制智能体在特定 globs 下的行为。
- **Commands**：Cursor 从 `.cursor/commands` 加载 `.md` 指令文件，用户输入 `/coder-dev` 即可触发对应智能体流程。

三者都源自 AGENT.md 中的声明，应当保持同步。

---

## 步骤

### 0. 自检 meta-agent commands

检查 `.cursor/commands/` 下是否存在 meta-agent 自身的指令文件（`meta-new.md`、`meta-sync.md`、`meta-evolution.md`）。缺失的立即生成。

### 1. 扫描智能体

读取 `../`（智能体所在目录），找出所有含 `AGENT.md` 或 `SKILL.md`（旧版兼容）的子目录。

对每个智能体，读取定义文件，提取：

**rule 文件**：检查 `.cursor/rules/{agent}-rules.mdc` 是否存在。

**触发指令**（`## 触发` 段）：
- 指令名（如 `@coder dev`）
- 参数（如 `[路径] [需求]`）
- 说明/阶段（如 `Phase 2: Develop`）→ 映射为模板变量 `{PHASE_DESCRIPTION}`，用于区分同动词 command

无 rule 文件的智能体标记为「未 init」。无 `触发` 段的标记为「无指令定义」。

### 2. Agent 健康诊断

对每个智能体，扫描其 `.dna/` 目录（含智能体自身的 `.dna/` 和管辖范围内的 `.dna/`）：

| 检查项 | 判定方法 | 异常标记 |
|--------|---------|---------|
| architecture 过期 | `last_verified` 距今 > 30 天 | ⚠️ 过期 |
| pitfalls 膨胀 | 条目数 > 20 或重复条目占比 > 30% | ⚠️ 需压缩 |
| 记忆文件缺失 | `.dna/` 下缺少 architecture.md 或 pitfalls.md | ❌ 缺失 |
| 智能体 .dna/ 缺失 | 智能体目录下无 `.dna/pitfalls.md`（自进化钩子无持久化） | ❌ 缺失 |

输出健康摘要，不修改任何文件。

### 3. 确保输出目录存在

检查 `.cursor/rules` 和 `.cursor/commands` 目录是否存在，不存在则自动创建。

### 4. 检查 Rules

读取 `.cursor/rules` 下所有 `*-rules.mdc` 文件，与已发现的智能体比对：

| 情况 | 判定 | 操作 |
|------|------|------|
| `.mdc` 不存在 | **未 init** | 提示执行 agent init |
| `.mdc` 存在且格式正确 | **正常** | 无需操作 |
| `.mdc` 存在但缺少必要段（管辖路径/架构图/约束） | **不完整** | 提示补全 |
| `.mdc` 存在但无对应智能体 | **孤立** | 建议删除 |

### 5. 比对 Commands

读取 `.cursor/commands` 下所有 `.md` 文件，区分自动生成的（含 `<!-- [meta-commands] -->` 或 `<!-- [brain-commands] -->` 标记）和手动创建的。

与智能体的触发指令比对：

| 情况 | 判定 | 操作 |
|------|------|------|
| 智能体有指令但无对应 command 文件 | **缺失** | 生成新文件 |
| command 文件存在且内容一致 | **同步** | 无需操作 |
| command 文件存在但内容不一致 | **漂移** | 建议更新 |
| command 文件存在但智能体已无该指令 | **孤立** | 建议删除 |
| 无标记的手动文件 | **跳过** | 不触碰 |

**command 文件名规则**：
- `{agent_name}-{command}.md`，全小写，用 `-` 连接
- 示例：`coder-dev.md`、`designer-spec.md`、`artist-check.md`

**command 文件内容模板**（`templates/command.md`）：

```markdown
<!-- [meta-commands] 由 @meta sync 自动生成，请勿手动编辑 -->
# {AGENT_NAME}-{COMMAND}
## 目标：{PHASE_DESCRIPTION}。读取 ../{AGENT_NAME}/AGENT.md，执行其中 `{COMMAND}` 相关的 Phase 流程。

参数：{ARGS_DESCRIPTION}

路径参数解析：用户提供了路径则用用户输入；未提供则用当前 IDE 选中的文件/目录；都没有则提示参数缺少。

执行前先按 AGENT.md 中的「上下文加载协议」加载必要文件。
```

**模板变量映射**：

| 变量 | 来源 |
|------|------|
| `{AGENT_NAME}` | 智能体名（目录名） |
| `{COMMAND}` | 触发表"触发方式"列中的指令动词（如 `dev`、`bug`） |
| `{PHASE_DESCRIPTION}` | 触发表"阶段"列的值（如 `Bug 修复（改代码）`），用于区分同动词 command |
| `{ARGS_DESCRIPTION}` | 触发表"触发方式"列中的参数部分 + Phase 段的参数说明 |

### 6. 一致性检查

跨维度检查路径和命名规范：

| 检查项 | 检查内容 | 异常判定 |
|--------|---------|---------|
| `.mdc` globs | globs 是否覆盖 agent 管辖路径 | globs 与实际管辖路径不匹配 |
| rule_file 命名 | 是否符合 `{agent}-rules.mdc` 规范 | 命名不规范 |
| command 文件命名 | 是否符合 `{agent}-{command}.md` 规范 | 命名不规范 |
| command 内容路径 | 引用的 AGENT.md 路径是否有效 | 路径指向不存在的文件 |
| agent name ↔ 目录名 | AGENT.md 的 `name` 与所在目录名是否一致 | 名称不一致 |

### 7. 输出统一报告

```
@meta sync 报告 — YYYY-MM-DD

智能体总数：{n}

═══ Agent 健康 ═══

[健康]
  ✓ {agent_name} — .dna/ 完整，architecture 未过期

[异常]
  ⚠️ {agent_name} — {异常描述}（如：architecture 过期 42 天、pitfalls 24 条需压缩）

[缺失]
  ❌ {agent_name} — {缺失描述}（如：无 .dna/pitfalls.md）

═══ Rules 同步 ═══

[已同步]
  ✓ {agent_name} → {rule_file}

[需生成]
  + {agent_name} → {rule_file}（globs: ...）

[需更新]
  ~ {agent_name} → {rule_file}
    差异：{展示不一致项}

[孤立]
  ? {rule_file} → 无对应智能体，建议删除

[无规则定义]
  - {agent_name}（未 init）

═══ Commands 同步 ═══

[已同步]
  ✓ /{agent}-{cmd} → @{agent} {cmd} [参数]

[需生成]
  + /{agent}-{cmd} → @{agent} {cmd} [参数]

[需更新]
  ~ /{agent}-{cmd} → 内容已变更

[孤立]
  ? /{filename} → 建议删除

[手动指令（跳过）]
  - /{filename}

═══ 一致性检查 ═══

{如全部通过：}
✅ 路径与命名检查全部通过

{如有异常：}
⚠️ 发现 {n} 项一致性问题：
  - {agent}: {问题描述}

═══ 汇总 ═══

| 类别 | 同步 | 需生成 | 需更新 | 孤立 |
|------|------|--------|--------|------|
| Agent 健康 | {n} | — | — | — |
| Rules | {n} | {n} | {n} | {n} |
| Commands | {n} | {n} | {n} | {n} |
健康异常：{n} 项
一致性问题：{n} 项
```

### 8. 等待确认

- 回复「生成」→ 写入所有缺失的 rules + commands
- 回复「全部」→ 生成 + 更新 + 删除孤立
- 回复「跳过」→ 不做修改

---

## 硬约束

- 不自动删除任何文件，孤立项仅提示
- write_mode 为 auto，直接写入
- 只操作带 `<!-- [meta-commands] -->` 或 `<!-- [brain-commands] -->` 标记的 command 文件，不触碰手动创建的指令
- 不修改 `alwaysApply` 规则文件的核心内容
- 不修改智能体的 AGENT.md 文件
