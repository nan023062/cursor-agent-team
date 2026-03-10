# IDE Adapters

This directory contains adapters that generate IDE-specific configurations from the portable `agent-manifest.yaml`.

## Architecture

```
agent-manifest.yaml          (Portable, IDE-agnostic)
        │
        ▼
   ┌─────────────────────────────────────────┐
   │           IDE Adapters                   │
   ├─────────────────────────────────────────┤
   │  cursor/     → .cursor/rules, commands  │
   │  claude/     → CLAUDE.md, .claude/      │
   │  trae/       → (future)                 │
   │  copilot/    → (future)                 │
   └─────────────────────────────────────────┘
```

## Usage

### Cursor Adapter

```bash
# Generate Cursor configurations from manifest
python gamedev/adapters/cursor/generate.py

# Or specify custom paths
python gamedev/adapters/cursor/generate.py \
    --manifest gamedev/agent-manifest.yaml \
    --output .cursor/
```

### Adding a New Adapter

1. Create a new directory: `adapters/{ide-name}/`
2. Implement `generate.py` with the following interface:

```python
def generate(manifest_path: str, output_dir: str) -> None:
    """
    Read agent-manifest.yaml and generate IDE-specific configurations.
    
    Args:
        manifest_path: Path to agent-manifest.yaml
        output_dir: Target directory for generated files
    """
    pass
```

3. Document IDE-specific mappings in `adapters/{ide-name}/README.md`

## Manifest to IDE Mapping

| Manifest Field | Cursor | Claude Code | Trae |
|---------------|--------|-------------|------|
| `agents.*.definition` | Referenced in rules | Included in CLAUDE.md | TBD |
| `agents.*.triggers` | `auto_trigger` in skills | `commands` section | TBD |
| `agents.*.managed_paths` | `globs` in rules | `globs` in rules | TBD |
| `agents.*.protocols` | Linked in skills | Linked in prompts | TBD |

## Design Principles

1. **Single Source of Truth**: All agent definitions live in `agent-manifest.yaml`
2. **Generated, Not Edited**: IDE-specific files are generated and should not be manually edited
3. **Idempotent**: Running the adapter multiple times produces the same output
4. **Backward Compatible**: Adapters should handle missing optional fields gracefully

## File Markers

Generated files include a marker comment to indicate they are auto-generated:

```markdown
<!-- [generated] from agent-manifest.yaml by cursor adapter -->
<!-- DO NOT EDIT MANUALLY - run 'python gamedev/adapters/cursor/generate.py' to regenerate -->
```

## Directory Structure

```
gamedev/
├── agent-manifest.yaml           # Source of truth
└── adapters/
    ├── README.md                 # This file
    ├── cursor/
    │   ├── generate.py           # Cursor adapter script
    │   └── README.md             # Cursor-specific docs
    └── claude/                   # (future)
        └── generate.py
```

## Contributing

When adding support for a new IDE:

1. Study the IDE's configuration format and capabilities
2. Map manifest fields to IDE-specific equivalents
3. Implement the adapter with proper error handling
4. Add tests for the adapter
5. Update this README with the new mapping
