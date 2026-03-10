<!-- [meta-commands] 由 @meta sync 自动生成，请勿手动编辑 -->
# {AGENT_NAME}-{COMMAND}
## 目标：{PHASE_DESCRIPTION}。读取 ../{AGENT_NAME}/AGENT.md，执行其中 `{COMMAND}` 相关的 Phase 流程。

参数：{ARGS_DESCRIPTION}

路径参数解析：用户提供了路径则用用户输入；未提供则用当前 IDE 选中的文件/目录；都没有则提示参数缺少。

执行前先按 AGENT.md 中的「上下文加载协议」加载必要文件。
