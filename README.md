# Portable AI Agent Team

A **portable, IDE-agnostic**, self-evolving AI team framework.

**Works with any AI coding IDE. Zero vendor lock-in.**

> Each agent has its own memory (`.dna/`), learns from mistakes (`pitfalls`), and evolves rules over time (`evolution`). A meta-agent can create new agents on demand.

---

## IDE Agnostic Architecture

This framework is designed to work with **any AI coding IDE**:

| IDE | Status | Adapter |
|-----|--------|---------|
| Cursor | ✅ Supported | `adapters/cursor/` |
| Claude Code | 🔜 Planned | `adapters/claude/` |
| Trae | 🔜 Planned | `adapters/trae/` |
| GitHub Copilot | 🔜 Planned | `adapters/copilot/` |

### How It Works

```
┌─────────────────────────────────────────────────────────────┐
│                    Portable Layer (IDE Agnostic)            │
├─────────────────────────────────────────────────────────────┤
│  agent-manifest.yaml    ← Single source of truth            │
│  AGENT.md               ← Agent definitions (pure Markdown) │
│  .dna/                  ← Module memory (pure Markdown)     │
│  protocols/             ← Workflow definitions              │
│  templates/             ← File templates                    │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼ adapters generate
┌─────────────────────────────────────────────────────────────┐
│                    IDE-Specific Layer (Generated)           │
├─────────────────────────────────────────────────────────────┤
│  .cursor/rules/*.mdc    ← Cursor rules                      │
│  .cursor/commands/*.md  ← Cursor slash commands             │
│  CLAUDE.md              ← Claude Code config                │
│  .trae/                 ← Trae config (future)              │
└─────────────────────────────────────────────────────────────┘
```

---

## What Makes This Different

- **IDE Agnostic** — Core definitions in `agent-manifest.yaml` and `.dna/` are pure YAML/Markdown. IDE adapters generate native configurations.
- **Self-Evolving Memory** — Agents remember mistakes in `.dna/pitfalls.md`. When patterns repeat (3+ times), `evolution` promotes them into permanent rules in `.dna/architecture.md`. The team gets smarter over time.
- **Zero Configuration** — Copy one folder into your project. First `init` creates everything automatically. No config files, no environment variables, no setup scripts.
- **Meta-Agent Creates Agents** — `@meta new` interactively generates a fully-formed agent with 14 standard patterns, ready to work.
- **Fully Portable** — No hardcoded paths. No project-specific content. Works in any project with any AI IDE.

---

## Install

```bash
# Copy the team directory into your project root
git clone <repo-url> /path/to/your/project/gamedev/

# Then generate IDE-specific configs
python gamedev/adapters/cursor/generate.py  # For Cursor
```

That's it. No dependencies (except PyYAML for adapters), no build step.

---

## Quick Start

```bash
# 1. Initialize any agent — rule file is created automatically
@coder init src/

# 2. Start working
@coder dev src/auth "implement login"

# 3. After a while, evolve — distill lessons into rules
@coder evolution

# 4. Audit everything
@meta sync
```

---

## How It Works

```
gamedev/
├── agent-manifest.yaml         # Single source of truth (IDE agnostic)
├── {agent}/AGENT.md            # Agent definition (portable)
│
│   ▼  adapters/cursor/generate.py
│
.cursor/rules/{agent}-rules.mdc # Generated: Cursor-specific rules
.cursor/commands/{agent}-*.md   # Generated: Cursor slash commands
│
│   ▼  daily work
│
{module}/.dna/pitfalls.md       # Mistakes accumulate
│
│   ▼  @agent evolution
│
{module}/.dna/architecture.md   # Patterns promoted to permanent rules
│
│   ▼  next operation reads evolved rules — cycle repeats
```

---

## Included: Game Dev Team

The `gamedev/` directory contains a complete game development team:

| Agent | Role | Key Commands |
|-------|------|-------------|
| **Creative** | Vision, research, milestones | `init` `vision` `blueprint` `research` |
| **Designer** | Systems, specs, balance | `init` `arch` `concept` `spec` `balance` |
| **Coder** | Implementation, architecture | `init` `dev` `fixbug` |
| **Artist** | Art standards, asset pipeline | `init` `ta` `concept` `check` `naming` |
| **Tester** | Testing, bugs, quality gate | `init` `test` `bug` `perf` `gate` |
| **Meta-Agent** | Creates and audits agents | `new` `sync` `evolution` |

Every agent also has `evolution` for memory maintenance.

---

## Create Your Own Team

Don't need game dev? Use the meta-agent to build any team:

```bash
@meta new frontend    # Creates a frontend agent interactively
@meta new backend     # Creates a backend agent
@meta new devops      # Creates a devops agent
@meta sync            # Generates all rule files and commands
```

The meta-agent asks what the agent manages, its commands, responsibilities, and quality constraints — then generates a complete `AGENT.md` with all 14 standard patterns.

---

## Directory Structure

```
gamedev/                          # Portable agent team
├── agent-manifest.yaml           # [NEW] Single source of truth for all agents
├── adapters/                     # [NEW] IDE-specific generators
│   ├── cursor/generate.py        # Generates .cursor/ configs
│   ├── claude/                   # (future) Claude Code adapter
│   └── README.md                 # Adapter development guide
├── meta-agent/                   # Agent factory + auditor
│   ├── AGENT.md
│   ├── protocols/                # sync, scan, evolution
│   └── templates/                # Agent skeleton, .dna/ templates
├── creative/                     # Creative agent
├── designer/                     # Design agent
├── coder/                        # Code agent
├── artist/                       # Art agent
└── tester/                       # Test agent
```

Each agent:
```
{agent}/
├── AGENT.md                   # Agent definition (IDE agnostic)
├── README.md                  # Usage guide
├── .dna/pitfalls.md           # Self-reflection log
├── protocols/                 # (optional) Detailed workflows
└── templates/                 # (optional) File templates with YAML front matter
```

---

## The `.dna/` Memory System

Every managed module gets a `.dna/` directory:

| File | Purpose | Written By | Format |
|------|---------|-----------|--------|
| `architecture.md` | Permanent rules, boundaries, constraints | `evolution` | YAML front matter + Markdown |
| `pitfalls.md` | Raw lessons learned | Daily operations | YAML front matter + Markdown |
| `changelog.md` | Change history | Daily operations | YAML front matter + Markdown |
| `dependencies.md` | Dependency whitelist | `init` / as needed | YAML front matter + Markdown |
| `wip.md` | Work in progress | Session continuity | YAML front matter + Markdown |

### YAML Front Matter

All `.dna/` files use standard YAML front matter for metadata:

```markdown
---
last_verified: 2026-03-10
maintainer: "@username"
boundary: hard
---

# Architecture

...content...
```

This format is:
- Parseable by any programming language
- Readable by any AI IDE
- Compatible with static site generators
- Version control friendly

The evolution cycle:

```
Work → Make mistakes → Record in pitfalls
→ @agent evolution → Identify patterns (same tag >= 3 times)
→ Promote to architecture constraints
→ Next operation reads evolved rules → Fewer mistakes → Cycle
```

---

## Generate IDE Configurations

After modifying `agent-manifest.yaml`, regenerate IDE-specific configs:

```bash
# For Cursor
python gamedev/adapters/cursor/generate.py

# Dry run (preview without writing)
python gamedev/adapters/cursor/generate.py --dry-run
```

---

## License

[MIT](LICENSE)

---

**[Chinese / 中文文档](README.zh-CN.md)**
