# Cursor Agent Team

A portable, self-evolving AI team framework for [Cursor IDE](https://cursor.com).

**Zero config. Copy and go.**

> Each agent has its own memory (`.dna/`), learns from mistakes (`pitfalls`), and evolves rules over time (`evolution`). A meta-agent can create new agents on demand.

---

## What Makes This Different

- **Self-Evolving Memory** — Agents remember mistakes in `.dna/pitfalls.md`. When patterns repeat (3+ times), `evolution` promotes them into permanent rules in `.dna/architecture.md`. The team gets smarter over time.
- **Zero Configuration** — Copy one folder into your project. First `init` creates everything automatically. No config files, no environment variables, no setup scripts.
- **Meta-Agent Creates Agents** — `@meta new` interactively generates a fully-formed agent with 14 standard patterns, ready to work.
- **Precise Context Injection** — Each agent's `.mdc` rule file uses Cursor's glob system to activate only when editing relevant files. No noise.
- **Fully Portable** — No hardcoded paths. No project-specific content. Works in any Cursor project.

---

## Install

```bash
# Copy the team directory into your project
git clone <repo-url> /path/to/your/project/.cursor/gamedev/
```

That's it. No dependencies, no build step.

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
AGENT.md                        # What the agent does (portable, no project paths)
    |
    v  first init
.cursor/rules/{agent}-rules.mdc # Project paths + architecture diagram + constraints
    |
    v  @meta sync
.cursor/commands/{agent}-*.md   # Cursor slash commands
    |
    v  daily work
.dna/pitfalls.md                # Mistakes accumulate
    |
    v  evolution
.dna/architecture.md            # Patterns promoted to permanent rules
    |
    v  next operation reads evolved rules — cycle repeats
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
gamedev/                       # Copy this into .cursor/
├── meta-agent/                # Agent factory + auditor
│   ├── AGENT.md
│   ├── protocols/             # sync, scan, evolution
│   └── templates/             # Agent skeleton, .dna/ templates
├── creative/                  # Creative agent
├── designer/                  # Design agent
├── coder/                     # Code agent
├── artist/                    # Art agent
└── tester/                    # Test agent
```

Each agent:
```
{agent}/
├── AGENT.md                   # Agent definition
├── README.md                  # Usage guide
├── .dna/pitfalls.md           # Self-reflection log
├── protocols/                 # (optional) Detailed workflows
└── templates/                 # (optional) File templates
```

---

## The `.dna/` Memory System

Every managed module gets a `.dna/` directory:

| File | Purpose | Written By |
|------|---------|-----------|
| `architecture.md` | Permanent rules, boundaries, constraints | `evolution` (promoted from pitfalls) |
| `pitfalls.md` | Raw lessons learned | Daily operations |
| `changelog.md` | Change history | Daily operations |
| `dependencies.md` | Dependency whitelist | `init` / as needed |

The evolution cycle:

```
Work → Make mistakes → Record in pitfalls
→ @agent evolution → Identify patterns (same tag >= 3 times)
→ Promote to architecture constraints
→ Next operation reads evolved rules → Fewer mistakes → Cycle
```

---

## DNA-MCP Server

The `agentic-os/` directory contains the **Agentic OS** — a C# MCP server that offloads scheduling, topology management, and workspace operations from the AI's context window into a deterministic program layer.

```
agentic-os/                    # MCP Server + CLI + UI
├── agentic-os.csproj
├── AgenticOs.UI/              # Avalonia 可视化界面
├── Cli/                        # CLI: agentic-os cli <command>
├── Services/                  # Task Scheduler, DNA Manager, Workspace Operator
└── Tools/                     # MCP tool definitions
```

**[Agentic OS Documentation →](agentic-os/README.md)**

---

## License

[MIT](LICENSE)

---

**[Chinese / 中文文档](README.zh.md)**
