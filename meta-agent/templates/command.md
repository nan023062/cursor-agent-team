<!-- [generated] from agent-manifest.yaml by IDE adapter -->
<!-- DO NOT EDIT MANUALLY - regenerate with: python gamedev/adapters/cursor/generate.py -->
# {AGENT_NAME}-{COMMAND}

## 目标

{PHASE_DESCRIPTION}。读取 `gamedev/{AGENT_NAME}/AGENT.md`，执行其中 `{COMMAND}` 相关的 Phase 流程。

## 参数

{ARGS_DESCRIPTION}

## 路径参数解析

1. 用户提供了路径 → 使用用户输入
2. 未提供 → 使用当前 IDE 选中的文件/目录
3. 都没有 → 提示参数缺少

## 执行

执行前先按 AGENT.md 中的「上下文加载协议」加载必要文件。

---
Owner: {AGENT_NAME}
