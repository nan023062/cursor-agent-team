# Phase 4: Config（配置体系设计）

### 全局配置架构（L1 级，立项时建立一次）

和 UX 架构一样，配置也有一个全局架构方案，约束所有系统的配置设计。

> 以下结构以 `designer-rules.mdc → 智能体管辖路径` 的根目录为起点。

```
{设计根目录}/ConfigArch/
├── .dna/
│   ├── architecture.md    # 配置架构总纲
│   ├── pitfalls.md        # 配置教训（格式踩坑、导出异常、解析性能）
│   └── changelog.md       # 配置架构变更记录
└── README.md                     # 配置架构概述
```

**配置架构总纲应定义**：
- **源格式标准**：策划用什么格式编辑（Excel / CSV / Google Sheets / 自研工具）
- **运行时格式**：代码加载什么格式（ScriptableObject / JSON / Binary / 自研格式）
- **导出管线**：源格式 → 运行时格式的转换流程和工具（由 Coder 实现）
- **命名约定**：配置表命名、字段命名、枚举命名的统一规范
- **类型体系**：支持的字段类型（int / float / string / Enum / 外键引用 / 数组）
- **校验规则**：导出时统一校验（范围检查、外键完整性、空值检查、重复主键检查）
- **版本兼容**：配置表 Schema 变更时如何保持旧数据兼容
- **热更新策略**：哪些配置支持运行时热更，哪些只在启动时加载

**各系统的 `config/schema.md` 必须遵循全局配置架构的格式标准和类型体系。**

Coder 根据此架构实现：
- 配置导出工具（Editor 脚本）
- 运行时配置加载和解析系统
- 导出校验器

### 触发

`@designer config 地下城电力系统`

### 目的

按全局配置架构的规范，为具体系统设计配置表结构。让策划直接编辑配置，不碰代码。

### 配置目录结构

每个系统的配置定义和数据都存在其设计目录下：

> 以下结构以 `designer-rules.mdc → 智能体管辖路径` 的根目录为起点。

```
{系统名}/
├── .dna/                       # 设计记忆
├── feature-spec.md               # 功能规格
└── config/                       # 配置体系
    ├── schema.md                 #   配置表 Schema 定义（字段、类型、约束）
    ├── data/                     #   策划填写的实际配置数据
    │   ├── generators.csv        #     发电机配置
    │   ├── fuel-types.csv        #     燃料类型
    │   └── ...
    └── export/                   #   导出规则
        └── export-rules.md       #     导出目标格式、路径、工具
```

### 流程

1. **识别可配置项**
   - 从 feature-spec 中提取所有数值、规则、阈值
   - 判断：硬编码 vs 配置表（原则：策划可能调的 → 配置表）

2. **设计 Schema（schema.md）**

   每张配置表定义：

   ```markdown
   ### PowerGeneratorConfig
   - 用途: 发电设施的基础参数
   - 主键: generatorType

   | 字段 | 类型 | 默认值 | 范围 | 说明 |
   |------|------|--------|------|------|
   | generatorType | Enum | Coal | Coal/Fusion | 发电机类型 |
   | baseOutput | float | 100 | 10-1000 | 基础产能(kW) |
   | fuelConsumption | float | 2 | 0.1-10 | 每秒燃料消耗 |
   | overloadThreshold | float | 1.2 | 1.0-2.0 | 过载触发阈值(倍) |
   | overloadDamageRate | float | 0.1 | 0-1 | 过载每秒损坏率 |
   ```

3. **定义导出规则（export-rules.md）**

   ```markdown
   ## 导出规则
   - 源格式: CSV（策划在 Excel/Google Sheets 编辑，导出 CSV）
   - 目标格式: ScriptableObject / JSON（Coder 运行时加载）
   - 导出路径: 见 designer-rules.mdc → 跨智能体协作路径 的数据路径
   - 导出工具: Unity Editor 菜单 Tools → Config → Import {系统名}
   - 校验规则: 导出时自动检查字段范围、外键引用完整性
   ```

5. **填写配置数据（data/）**
   - 策划按 Schema 在 `data/` 下填写 CSV
   - 禁止在代码中硬编码 Schema 已覆盖的数值

6. **交给 Coder**
   - Coder 根据 Schema 实现数据类和加载代码
   - Coder 根据 export-rules 实现导出工具（Editor 脚本）
   - Coder 在 `architecture.md` 中引用配置路径
   - 后续策划改数值只改 CSV → 跑导出工具 → 代码自动读取

### 配置纪律

- 策划可能调的数值必须走配置表，禁止硬编码
- 每张配置表必须有 Schema（字段、类型、范围、说明），无 Schema 的配置禁止使用
- 配置间引用必须在 dependencies.md 中声明，禁止隐式引用
- 配置变更须在 `changelog.md` 记录
- 导出后必须通过校验（范围检查 + 外键完整性），校验不通过禁止提交
