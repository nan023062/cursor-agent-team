---
# DNA Architecture Template (Generic) - IDE Agnostic Format
# Replace {MODULE_TERM} with your domain term (e.g., 模块, 系统, 功能)
last_verified: # YYYY-MM-DD, update after each operation or Evolution
maintainer: # @username, required for team projects
maturity: 0 # Evolution promotion count: 0=embryo, 1-3=infant, 4-9=juvenile, 10+=mature
boundary: hard # hard | soft | shared
---

# {MODULE_TERM}架构

> 编写指南：总体保持 50–150 行。每段回答核心问题，不复制代码/资产/设计细节。
> last_verified 超过 30 天未更新时 Evolution 发出警告。

## 概述
<!-- 2-3 句话：这个{MODULE_TERM}是什么？解决什么核心问题？ -->
<!-- 维护者信息已移至 front matter 的 maintainer 字段 -->

## 核心模型
<!-- 每个关键概念 2-3 行：职责、关键属性、与其他模型的关系 -->

## {MODULE_TERM}边界

- **对外暴露**：{公共接口/产出物列表}
- **职责范围**：只负责什么，不负责什么
- **交互方式**：上下游如何与本{MODULE_TERM}交互
- **边界约束**：本{MODULE_TERM}特有的禁止事项

## 设计目标
<!-- 3-5 条 bullet；最后一条写「非目标」，明确不做什么 -->
- 目标 1
- 非目标：...

## 约束
<!-- 由 Evolution 升格写入的经验规则。初始为空，随使用逐步积累 -->
