# 同步协议（诊断 + Manifest + IDE 配置）

> `@meta sync` 时执行。一次性完成智能体健康诊断、manifest 一致性检查、IDE 配置同步建议。

## 背景

- **Agent 健康**：每个智能体的 `.dna/` 目录记录了永久规则、经验教训和变更历史，需要定期检查过期和膨胀。
- **Manifest**：`agent-manifest.yaml` 是所有智能体的统一描述文件（IDE 无关），应与实际目录结构保持同步。
- **IDE 配置**：各 IDE 的配置文件由适配器从 manifest 生成，sync 检查是否需要重新生成。

三者都源自 AGENT.md 中的声明，应当保持同步。

---

## 步骤

### 0. 自检

检查 `../agent-manifest.yaml` 是否存在。缺失则从模板创建。

### 1. 扫描智能体

读取 `../`（智能体所在目录），找出所有含 `AGENT.md` 或 `SKILL.md`（旧版兼容）的子目录。

对每个智能体，读取定义文件，提取：

**基本信息**：
- 智能体名（目录名）
- 描述（从 YAML front matter）
- 管辖路径（从 `## 管理范围` 或 `## 管辖范围` 段）

**触发指令**（`## 触发` 段）：
- 指令名（如 `@coder dev`）
- 参数（如 `[路径] [需求]`）
- 说明/阶段（如 `Phase 2: Develop`）

无 `触发` 段的标记为「无指令定义」。

### 2. Agent 健康诊断

> **meta-agent 免检**：meta-agent 是所有智能体的起源，其进化由人主动维护，不参与 `.dna/` 自进化闭环。跳过 meta-agent 的 `.dna/` 健康检查，不报缺失。

对每个智能体（meta-agent 除外），扫描其 `.dna/` 目录（含智能体自身的 `.dna/` 和管辖范围内的 `.dna/`）：

| 检查项 | 判定方法 | 异常标记 |
|--------|---------|---------|
| architecture 过期 | `last_verified` 距今 > 30 天 | ⚠️ 过期 |
| pitfalls 膨胀 | 条目数 > 20 或重复条目占比 > 30% | ⚠️ 需压缩 |
| 记忆文件缺失 | `.dna/` 下缺少 architecture.md 或 pitfalls.md | ❌ 缺失 |
| 智能体 .dna/ 缺失 | 智能体目录下无 `.dna/pitfalls.md`（自进化钩子无持久化） | ❌ 缺失 |

输出健康摘要，不修改任何文件。

### 3. 检查 Manifest 一致性

读取 `../agent-manifest.yaml`，与已发现的智能体比对：

| 情况 | 判定 | 操作 |
|------|------|------|
| agent 目录存在但 manifest 未注册 | **未注册** | 建议添加到 manifest |
| manifest 有配置但目录不存在 | **孤立** | 建议从 manifest 移除 |
| definition 路径无效 | **路径错误** | 建议修正 |
| managed_paths 与实际不符 | **漂移** | 建议更新 |
| 完全一致 | **同步** | 无需操作 |

### 4. 一致性检查

跨维度检查路径和命名规范：

| 检查项 | 检查内容 | 异常判定 |
|--------|---------|---------|
| definition 路径 | 是否指向有效的 AGENT.md | 路径指向不存在的文件 |
| managed_paths | 路径格式是否有效 | 无效的 glob 模式 |
| agent name ↔ 目录名 | AGENT.md 的 `name` 与所在目录名是否一致 | 名称不一致 |
| triggers pattern | 正则模式是否有效 | 无效的正则表达式 |

### 5. 输出统一报告

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

═══ Manifest 同步 ═══

[已同步]
  ✓ {agent_name} — 配置与实际一致

[未注册]
  + {agent_name} — 建议添加到 agent-manifest.yaml

[需更新]
  ~ {agent_name} — {差异描述}

[孤立]
  ? {agent_name} — manifest 中存在但目录不存在，建议移除

═══ 一致性检查 ═══

{如全部通过：}
✅ 路径与命名检查全部通过

{如有异常：}
⚠️ 发现 {n} 项一致性问题：
  - {agent}: {问题描述}

═══ IDE 配置建议 ═══

{如 manifest 有变更：}
建议运行适配器重新生成 IDE 配置：
  python gamedev/adapters/cursor/generate.py

═══ 汇总 ═══

| 类别 | 同步 | 需添加 | 需更新 | 孤立 |
|------|------|--------|--------|------|
| Agent 健康 | {n} | — | — | — |
| Manifest | {n} | {n} | {n} | {n} |
健康异常：{n} 项
一致性问题：{n} 项
```

### 6. 等待确认

- 回复「更新」→ 更新 manifest 中的漂移项
- 回复「添加」→ 将未注册的 agent 添加到 manifest
- 回复「全部」→ 更新 + 添加 + 移除孤立
- 回复「跳过」→ 不做修改

确认后，如有 manifest 变更，提示运行适配器：

```bash
python gamedev/adapters/cursor/generate.py
```

---

## 硬约束

- 不自动删除任何文件，孤立项仅提示
- write_mode 为 auto，直接写入
- 不修改智能体的 AGENT.md 文件
- IDE 配置由适配器生成，sync 只检查不直接修改
- manifest 变更需用户确认后执行
