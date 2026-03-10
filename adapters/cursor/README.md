# Cursor IDE Adapter

Generates Cursor-specific configurations from `agent-manifest.yaml`.

## Generated Files

| Source | Generated | Description |
|--------|-----------|-------------|
| `agents.*.managed_paths` | `.cursor/rules/{agent}-rules.mdc` | Rule files with globs |
| `agents.*.triggers` | `.cursor/commands/{agent}-{cmd}.md` | Slash commands |
| `agents.*.skills` | `.cursor/skills/{skill}/SKILL.md` | Skill definitions |

## Usage

```bash
# From workspace root
python gamedev/adapters/cursor/generate.py

# Dry run (preview without writing)
python gamedev/adapters/cursor/generate.py --dry-run

# Custom paths
python gamedev/adapters/cursor/generate.py \
    --manifest gamedev/agent-manifest.yaml \
    --output .cursor/ \
    --gamedev gamedev/
```

## Manifest to Cursor Mapping

### Rule Files (.mdc)

```yaml
# agent-manifest.yaml
agents:
  coder:
    description: "Programming agent..."
    managed_paths:
      - "Assets/**"
      - "XDTMonoProjects/**"
```

Generates:

```yaml
# .cursor/rules/coder-rules.mdc
---
description: "Programming agent..."
owner: pending-owner
department: client
globs:
  - "Assets/**"
  - "XDTMonoProjects/**"
---
```

### Command Files

```yaml
# agent-manifest.yaml
agents:
  coder:
    triggers:
      - pattern: "init|dev|fixbug|evolution"
        scope: explicit
```

Generates:
- `.cursor/commands/coder-init.md`
- `.cursor/commands/coder-dev.md`
- `.cursor/commands/coder-fixbug.md`
- `.cursor/commands/coder-evolution.md`

## Cursor-Specific Features

### Globs

Cursor uses globs to determine when rules apply:
- `"Assets/**"` - All files under Assets/
- `"**/*.cs"` - All C# files
- `"!**/node_modules/**"` - Exclude patterns

### alwaysApply

For rules that should always apply (not file-specific), add to manifest:

```yaml
agents:
  meta-agent:
    always_apply: true  # Generates alwaysApply: true in .mdc
```

### Skills

Skills are more complex and may require manual configuration. The adapter generates a skeleton that can be customized.

## Requirements

- Python 3.8+
- PyYAML (`pip install pyyaml`)

## Regeneration

Generated files include a marker comment. The adapter is idempotent - running it multiple times produces the same output.

To regenerate after manifest changes:

```bash
python gamedev/adapters/cursor/generate.py
```
