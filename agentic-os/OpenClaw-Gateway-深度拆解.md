# OpenClaw Gateway 核心原理深度拆解

## 一句话定义

OpenClaw Gateway 本质上是一个**本地运行的、面向 AI Agent 的消息操作系统**——它解决的核心问题是：**如何让一个 AI Agent 稳定地活在你的所有聊天通道里，像一个真正的团队成员一样工作**。

---

## 一、核心架构：单进程全权拥有

```
                    ┌─────────────────────────────────┐
                    │       Gateway 进程 (:18789)      │
                    │                                  │
  WhatsApp ─────┐   │  ┌──────────┐   ┌────────────┐  │
  Telegram ─────┤   │  │ 消息路由  │──▶│ Agent Turn │  │
  Discord  ─────┤──▶│  │ Bindings │   │  串行队列   │  │
  Slack    ─────┤   │  └──────────┘   └─────┬──────┘  │
  飞书      ─────┤   │                      │         │
  WebChat  ─────┘   │  ┌──────────┐   ┌─────▼──────┐  │
                    │  │ Session  │◀──│ pi-agent   │  │
  macOS App ────┐   │  │  Store   │   │  runtime   │  │
  CLI      ─────┤──▶│  └──────────┘   └─────┬──────┘  │
  iOS Node ─────┘   │                      │         │
                    │  ┌──────────┐   ┌─────▼──────┐  │
                    │  │  Cron    │   │   Tools    │  │
                    │  │ 调度器    │   │ 工具系统    │  │
                    │  └──────────┘   └────────────┘  │
                    └─────────────────────────────────┘
```

**关键设计决策：一个进程拥有一切。**

Gateway 不是一个简单的 HTTP 代理或消息转发器。它是一个**有状态的单进程守护程序**，独占所有通道连接（WhatsApp 会话、Telegram Bot、飞书 App 等），独占所有 Session 状态，独占所有 Agent 执行。

这个设计的工程意义是：**消除了分布式一致性问题**。不需要消息队列、不需要数据库、不需要锁服务——所有状态在同一个进程内，用 TypeScript Promise 链保证串行。

---

## 二、核心机制：Agent Turn（轮次引擎）

这是 Gateway 最核心的概念。一个 Agent Turn 是一次完整的 agentic loop：

```
入站消息
  │
  ▼
① 路由 ── Bindings 规则决定哪个 Agent 处理
  │
  ▼
② Session Key 解析 ── 确定对话归属（私聊/群聊/Cron）
  │
  ▼
③ 去重 + 防抖 ── 避免重复触发，合并连续消息
  │
  ▼
④ 队列串行化 ── 每 session 同时只有一个 run
  │
  ▼
⑤ 上下文组装 ── System Prompt + Skills + Bootstrap + 历史消息
  │
  ▼
⑥ 模型推理 ── 调用 LLM（DeepSeek / Claude / GPT 等）
  │
  ▼
⑦ 工具执行 ── exec / read / write / browser / 消息 / cron...
  │     │
  │     └──▶ 循环回⑥（模型决定是否继续调用工具）
  │
  ▼
⑧ 流式回复 ── delta 流推送 + 通道限制 + 分块
  │
  ▼
⑨ 持久化 ── 写入 session JSONL 记录
```

### 队列串行化（第④步）

这是解决 Agent 最棘手并发问题的关键——如果用户连续发 3 条消息，不会同时启动 3 个 Agent run 互相踩踏。Gateway 提供了 5 种队列模式：

| 模式 | 行为 | 适用场景 |
|------|------|---------|
| `collect` (默认) | 合并排队消息，一次处理 | 普通对话 |
| `steer` | 注入当前运行中的 turn | 需要打断 Agent 思路 |
| `followup` | 当前 run 完成后再处理 | 严格按序 |
| `steer-backlog` | 注入当前 + 保留 followup | 既要即时反应又要完整处理 |
| `interrupt` | 中止当前 run，执行新消息 | 紧急场景 |

### Agent Turn 内部运行时

Agent Turn 的执行依赖 `pi-agent-core` 运行时（嵌入式 Agent 引擎）：

1. **`agent` RPC** → 验证参数、解析 session、持久化元数据 → 立即返回 `{ runId, acceptedAt }`
2. **`agentCommand`** → 解析 model、加载 skills 快照 → 调用 `runEmbeddedPiAgent`
3. **`runEmbeddedPiAgent`** → 通过 per-session + global 队列串行化 → 构建 pi session → 订阅事件流 → 超时中止
4. **`subscribeEmbeddedPiSession`** → 将 pi 事件映射为 OpenClaw 流：
   - 工具事件 → `stream: "tool"`
   - 助手输出 → `stream: "assistant"`
   - 生命周期 → `stream: "lifecycle"` (`phase: "start"|"end"|"error"`)

---

## 三、核心机制：Session 管理

Gateway 把 Session 当作**一等公民**来管理，而不是简单地把对话历史塞给模型。

### Session Key 映射体系

```
私聊 (dmScope=main)     → agent:main:main          # 所有私聊共享
私聊 (per-channel-peer) → agent:main:feishu:dm:u123 # 按通道+用户隔离
群聊                    → agent:main:feishu:group:oc_xxx
Cron 任务               → cron:job-uuid
Webhook                 → hook:uuid
```

### Session 生命周期

- **每日重置**：默认每天 4:00 AM 自动重置（避免上下文无限膨胀）
- **Idle 超时**：可选，滑动窗口，与每日重置取先到者
- **自动 Compaction**：压缩旧对话历史，保留关键信息
- **Memory Flush**：接近 compaction 时自动触发，提醒 Agent 把重要信息写到文件
- **磁盘配额**：防止 JSONL 记录无限增长，支持 pruneAfter / maxEntries / maxDiskBytes

### 存储位置

- 元数据：`~/.openclaw/agents/<agentId>/sessions/sessions.json`
- 对话记录：`~/.openclaw/agents/<agentId>/sessions/<SessionId>.jsonl`
- Gateway 是唯一的事实来源，所有 UI 客户端从 Gateway 查询

---

## 四、核心机制：通道抽象层

Gateway 的一个关键工程贡献是**把所有聊天通道统一抽象成同一套语义**：

```
WhatsApp（Baileys）  ┐
Telegram（grammY）   │
Discord（Carbon）    │     ┌───────────────────┐
Slack（Bolt）        ├────▶│ 统一的 Inbound     │──▶ Agent Turn
飞书（Lark SDK）     │     │ Body / CommandBody │
Signal              │     │ / Envelope / Media │
iMessage            │     └───────────────────┘
WebChat             ┘
```

### 入站处理

无论消息来自哪个通道，Agent 看到的是同一套抽象：

- **Body**：发送给 Agent 的 prompt 文本（含通道信封和可选历史包装）
- **CommandBody**：原始用户文本，用于指令/命令解析
- **去重**：短期缓存防止通道重连导致的重复消息
- **防抖**：`debounceMs` 配置合并同一发送方的连续消息（可按通道单独覆盖）

### 出站处理

- 按通道文本限制自动分块（不拆分代码块）
- Block Streaming 支持边生成边发送
- 按通道做格式适配（Telegram 支持 Markdown，WhatsApp 限制不同）
- `humanDelay` 模拟人类打字间隔

**核心意义：Agent 写一次逻辑，自动在所有通道上工作。**

---

## 五、核心机制：消息路由（Bindings）

Bindings 决定哪个 Agent 处理哪条消息，路由优先级从高到低：

1. 精确 peer match（指定某个联系人）
2. 父 peer match（线程继承）
3. Guild + Roles match（Discord 服务器 + 角色）
4. Guild match
5. Team match（Slack 团队）
6. Account match（飞书账号匹配）
7. Channel match（通道级别）
8. **默认 Agent**

```json
{
  "bindings": [
    {
      "agentId": "coder",
      "match": { "channel": "feishu", "accountId": "coder" }
    }
  ]
}
```

这意味着同一个 Gateway 可以托管多个 Agent，每个以不同的 Bot 身份出现在不同通道。

---

## 六、核心机制：工具系统（Tools）

### 内置工具

| 类别 | 工具 |
|------|------|
| 文件系统 | `read`、`write`、`edit`、`apply_patch` |
| 执行 | `exec`（Shell 命令） |
| 浏览器 | `browser` |
| 消息 | `message`（跨通道发消息） |
| 调度 | `cron`（创建/管理定时任务） |
| Session | `sessions_list`、`sessions_history`、`sessions_spawn` |
| Gateway | `gateway`（网关管理） |
| 设备 | `canvas`、`nodes`（控制 iOS/Android/macOS 设备） |

### 工具策略

- **Profile 预设**：`minimal` / `coding` / `messaging` / `full`
- **白名单/黑名单**：`tools.allow` / `tools.deny`（deny 优先）
- **按 Provider 限制**：`tools.byProvider` 对不同模型提供商做细粒度控制

### 工具执行流程

工具调用在 Agent Turn 内循环执行，模型决定是否继续调用工具。插件可通过 `before_tool_call` / `after_tool_call` Hook 拦截。

---

## 七、核心机制：插件系统

Gateway 通过 Plugin Hook 系统提供完整的生命周期拦截能力：

```
gateway_start
  │
  ▼  消息进来
message_received → before_model_resolve → before_prompt_build
  │                                        │
  ▼                                        ▼
before_agent_start → [Agent Turn 执行中] → agent_end
  │                       │
  ▼                       ▼
before_tool_call    after_tool_call → tool_result_persist
  │
  ▼  消息出去
message_sending → message_sent
  │
  ▼  session 结束
session_end
  │
  ▼
gateway_stop
```

### 插件能力

插件是 TypeScript 模块，in-process 加载，可以注册：

- Gateway RPC 方法
- Gateway HTTP handler
- Agent 工具
- CLI 命令
- 后台服务
- 新的通道（如 Matrix、Teams、Nostr）
- Provider OAuth 流程
- Auto-reply 命令（不经过 AI Agent 直接响应）

### 官方插件示例

- `@openclaw/voice-call` — 语音通话
- `@openclaw/msteams` — Microsoft Teams 通道
- `@openclaw/matrix` — Matrix 通道
- `@openclaw/nostr` — Nostr 通道
- Memory (LanceDB) — 长期记忆搜索

---

## 八、核心机制：Cron 调度

Cron 运行在 Gateway 进程内，不是模型逻辑的一部分。

### 两种执行模式

| 模式 | Session Key | 行为 |
|------|------------|------|
| `main session` | 入队 system event | 由下次 heartbeat 处理 |
| `isolated` | `cron:<jobId>` | 独立 Agent Turn，每次新 session |

### 投递模式

- `announce`：结果发送到指定通道（飞书/Telegram 等）
- `webhook`：结果发送到 URL

### 配置示例

```json
{
  "schedule": { "kind": "cron", "expr": "0 9 * * *", "tz": "Asia/Shanghai" },
  "sessionTarget": "isolated",
  "payload": { "kind": "agentTurn", "message": "执行代码审查", "timeoutSeconds": 900 },
  "delivery": { "mode": "announce", "channel": "feishu", "to": "ou_xxx" }
}
```

---

## 九、协议层：WebSocket + JSON

### 传输协议

- 传输层：WebSocket，文本帧，JSON 载荷
- 首帧必须是 `connect` 握手（含 Token 认证 + 设备签名）
- 请求：`{type:"req", id, method, params}` → 响应：`{type:"res", id, ok, payload|error}`
- 事件：`{type:"event", event, payload, seq?, stateVersion?}`
- 副作用操作需要 **idempotency key** 以安全重试

### 客户端角色

| 角色 | 类型 | 能力 |
|------|------|------|
| `operator` | macOS App / CLI / Web UI | 控制面操作 |
| `node` | macOS / iOS / Android / headless | 设备能力：camera / screen / canvas / location |

### 安全模型

- 设备配对：新设备需要 approval，Gateway 签发 device token
- 本地连接（loopback）可自动批准
- 非本地连接需要显式批准
- 支持 TLS + 证书指纹固定

---

## 十、核心价值总结

### 第一层：把 Agent 从"API 调用"变成"常驻服务"

大多数框架是**无状态的函数调用**——每次要用 Agent 就写脚本调一次 LLM API。Gateway 把 Agent 变成了一个 **7x24 常驻守护进程**，有自己的 Session、Cron、消息通道，像一个真正在线的人。

### 第二层：把 Agent 从"聊天机器人"变成"操作系统级 Actor"

Gateway 管理的是一个完整的 Actor 系统：

- **身份**：每个 Agent 有独立身份（SOUL/IDENTITY），以不同 Bot 出现
- **状态**：持久化 Session 历史 + 可进化的 `.dna/` 记忆
- **调度**：Cron 让 Agent 自主行动（每天发日报、做代码审查）
- **协作**：Agent 间通过 `sessions_spawn` 派发子任务
- **工具**：exec 命令、读写文件、浏览器、IoT 设备控制

### 第三层：本地优先 + 单一事实来源

```
┌──────────────────────────────────────────┐
│           Gateway = 单一事实来源           │
│                                          │
│  ✓ 所有通道连接由 Gateway 独占            │
│  ✓ 所有 Session 状态由 Gateway 持有       │
│  ✓ 所有 Agent 执行由 Gateway 串行化       │
│  ✓ 所有数据在本地磁盘，不经过任何云服务     │
│  ✓ 多端（Mac App/CLI/WebChat/手机）       │
│    都是 Gateway 的客户端，不持有状态        │
└──────────────────────────────────────────┘
```

### 与其他框架的本质差异

| 维度 | 其他框架（LangChain/AutoGen/CrewAI） | OpenClaw Gateway |
|------|--------------------------------------|------------------|
| 运行模式 | 脚本式一次性调用 | 常驻守护进程 |
| 通道集成 | 需要自己写适配器 | 内置 WhatsApp/Telegram/Discord/Slack/飞书等 |
| 状态管理 | 开发者自行实现 | Session + JSONL + Compaction + Memory Flush |
| 并发控制 | 无 | 队列串行化 + 5 种队列模式 |
| Agent 协作 | 框架级编排 | sessions_spawn + bindings 路由 |
| 部署 | 需要服务器/云服务 | 本地单进程，零外部依赖 |
| 数据所有权 | 取决于云服务 | 完全本地，用户拥有一切 |

---

## 一句话概括

**OpenClaw Gateway 的核心价值 = 把 LLM 的"一次性调用"包装成了一个"有状态、有身份、有调度、多通道、可进化的本地 AI 操作系统"。**

它不是在做"如何更好地调 API"，而是在做"如何让 AI Agent 像操作系统中的进程一样稳定地长期运行并与真实世界交互"。这就是 Gateway 的本质——一个面向 Agent 的本地运行时。
