#!/usr/bin/env python3
"""
Cursor IDE Adapter - Generate .cursor/ configurations from agent-manifest.yaml

This adapter reads the portable gamedev/agent-manifest.yaml and generates:
- .cursor/rules/{agent}-rules.mdc
- .cursor/commands/{agent}-*.md
- .cursor/skills/{skill}/SKILL.md (if defined)

Usage:
    # From workspace root
    python gamedev/adapters/cursor/generate.py
    
    # With custom paths
    python gamedev/adapters/cursor/generate.py --manifest gamedev/agent-manifest.yaml --output .cursor/
    
    # Preview without writing
    python gamedev/adapters/cursor/generate.py --dry-run
"""

import argparse
import os
import sys
from pathlib import Path
from datetime import datetime
from typing import Any

try:
    import yaml
except ImportError:
    print("Error: PyYAML is required. Install with: pip install pyyaml")
    sys.exit(1)


GENERATED_MARKER = """<!-- [generated] from agent-manifest.yaml by cursor adapter -->
<!-- DO NOT EDIT MANUALLY - run 'python gamedev/adapters/cursor/generate.py' to regenerate -->
"""

MDC_GENERATED_MARKER = """# [generated] from agent-manifest.yaml by cursor adapter
# DO NOT EDIT MANUALLY - run 'python gamedev/adapters/cursor/generate.py' to regenerate
"""


def load_manifest(manifest_path: str) -> dict:
    """Load and parse agent-manifest.yaml."""
    with open(manifest_path, 'r', encoding='utf-8') as f:
        return yaml.safe_load(f)


def generate_rule_file(agent_name: str, agent_config: dict, output_dir: Path, gamedev_dir: Path) -> str:
    """Generate a Cursor rule file (.mdc) for an agent."""
    
    description = agent_config.get('description', f'{agent_name} agent rules')
    managed_paths = agent_config.get('managed_paths', [])
    definition_path = agent_config.get('definition', '')
    
    # Build globs section
    globs_yaml = '\n'.join(f'  - "{p}"' for p in managed_paths) if managed_paths else '  - "**/*"'
    
    # Read agent definition to extract key sections
    agent_md_path = gamedev_dir / definition_path
    agent_summary = ""
    if agent_md_path.exists():
        with open(agent_md_path, 'r', encoding='utf-8') as f:
            content = f.read()
            # Extract first paragraph after the title as summary
            lines = content.split('\n')
            for i, line in enumerate(lines):
                if line.startswith('# ') and i + 2 < len(lines):
                    # Skip empty lines and get the first paragraph
                    for j in range(i + 1, min(i + 10, len(lines))):
                        if lines[j].strip() and not lines[j].startswith('#'):
                            agent_summary = lines[j].strip()
                            break
                    break
    
    rule_content = f"""{MDC_GENERATED_MARKER}---
description: "{description}"
owner: pending-owner
department: client
globs:
{globs_yaml}
---

## {agent_name.title()} Agent

{agent_summary}

### Agent Definition

See `gamedev/{definition_path}` for full agent definition.

### Managed Paths

This rule applies to:
{chr(10).join(f'- `{p}`' for p in managed_paths)}

### Usage

Trigger this agent with `@{agent_name}` commands:
"""
    
    # Add trigger patterns
    triggers = agent_config.get('triggers', [])
    for trigger in triggers:
        pattern = trigger.get('pattern', '')
        scope = trigger.get('scope', 'explicit')
        desc = trigger.get('description', '')
        rule_content += f"- `@{agent_name} {pattern.split('|')[0]}` - {desc} ({scope})\n"
    
    return rule_content


def generate_command_file(agent_name: str, command: str, agent_config: dict, gamedev_dir: Path) -> str:
    """Generate a Cursor command file for an agent command."""
    
    definition_path = agent_config.get('definition', '')
    
    command_content = f"""{GENERATED_MARKER}
# {agent_name}-{command}

## Goal

Execute the `{command}` phase of the {agent_name} agent.

## Instructions

1. Read the agent definition: `gamedev/{definition_path}`
2. Follow the `{command}` phase instructions
3. Load context from `.dna/` files as specified

## Agent Reference

@gamedev/{definition_path}

---
Owner: {agent_name}
"""
    return command_content


def generate_cursor_configs(manifest: dict, output_dir: Path, gamedev_dir: Path, dry_run: bool = False) -> None:
    """Generate all Cursor configurations from manifest."""
    
    rules_dir = output_dir / 'rules'
    commands_dir = output_dir / 'commands'
    
    if not dry_run:
        rules_dir.mkdir(parents=True, exist_ok=True)
        commands_dir.mkdir(parents=True, exist_ok=True)
    
    agents = manifest.get('agents', {})
    generated_files = []
    
    for agent_name, agent_config in agents.items():
        # Generate rule file
        rule_filename = f"{agent_name}-rules.mdc"
        rule_path = rules_dir / rule_filename
        rule_content = generate_rule_file(agent_name, agent_config, output_dir, gamedev_dir)
        
        if dry_run:
            print(f"[DRY-RUN] Would generate: {rule_path}")
            print(f"  Content preview: {len(rule_content)} bytes")
        else:
            with open(rule_path, 'w', encoding='utf-8-sig') as f:
                f.write(rule_content)
            generated_files.append(str(rule_path))
            print(f"Generated: {rule_path}")
        
        # Generate command files from triggers
        triggers = agent_config.get('triggers', [])
        for trigger in triggers:
            pattern = trigger.get('pattern', '')
            commands = pattern.split('|')
            
            for command in commands:
                command = command.strip()
                if not command:
                    continue
                    
                cmd_filename = f"{agent_name}-{command}.md"
                cmd_path = commands_dir / cmd_filename
                cmd_content = generate_command_file(agent_name, command, agent_config, gamedev_dir)
                
                if dry_run:
                    print(f"[DRY-RUN] Would generate: {cmd_path}")
                else:
                    with open(cmd_path, 'w', encoding='utf-8-sig') as f:
                        f.write(cmd_content)
                    generated_files.append(str(cmd_path))
                    print(f"Generated: {cmd_path}")
    
    # Generate index file
    index_path = output_dir / 'GENERATED_INDEX.md'
    index_content = f"""{GENERATED_MARKER}
# Generated Cursor Configurations

Generated at: {datetime.now().isoformat()}
Source: gamedev/agent-manifest.yaml

## Generated Files

### Rules
{chr(10).join(f'- {f}' for f in generated_files if f.endswith('.mdc'))}

### Commands
{chr(10).join(f'- {f}' for f in generated_files if f.endswith('.md') and 'commands' in f)}

## Regenerate

```bash
python gamedev/adapters/cursor/generate.py
```
"""
    
    if not dry_run:
        with open(index_path, 'w', encoding='utf-8-sig') as f:
            f.write(index_content)
        print(f"\nGenerated index: {index_path}")
        print(f"\nTotal files generated: {len(generated_files) + 1}")


def main():
    parser = argparse.ArgumentParser(
        description='Generate Cursor IDE configurations from agent-manifest.yaml'
    )
    parser.add_argument(
        '--manifest', '-m',
        default='gamedev/agent-manifest.yaml',
        help='Path to agent-manifest.yaml (default: gamedev/agent-manifest.yaml)'
    )
    parser.add_argument(
        '--output', '-o',
        default='.cursor',
        help='Output directory for generated files (default: .cursor)'
    )
    parser.add_argument(
        '--gamedev', '-g',
        default='gamedev',
        help='Path to gamedev directory (default: gamedev)'
    )
    parser.add_argument(
        '--dry-run', '-n',
        action='store_true',
        help='Show what would be generated without writing files'
    )
    
    args = parser.parse_args()
    
    # Resolve paths relative to script location or current directory
    script_dir = Path(__file__).parent
    workspace_root = script_dir.parent.parent.parent  # gamedev/adapters/cursor -> workspace root
    
    manifest_path = Path(args.manifest)
    if not manifest_path.is_absolute():
        manifest_path = workspace_root / manifest_path
    
    output_dir = Path(args.output)
    if not output_dir.is_absolute():
        output_dir = workspace_root / output_dir
    
    gamedev_dir = Path(args.gamedev)
    if not gamedev_dir.is_absolute():
        gamedev_dir = workspace_root / gamedev_dir
    
    if not manifest_path.exists():
        print(f"Error: Manifest file not found: {manifest_path}")
        sys.exit(1)
    
    print(f"Loading manifest: {manifest_path}")
    print(f"Output directory: {output_dir}")
    print(f"Gamedev directory: {gamedev_dir}")
    print()
    
    manifest = load_manifest(str(manifest_path))
    generate_cursor_configs(manifest, output_dir, gamedev_dir, args.dry_run)
    
    if args.dry_run:
        print("\n[DRY-RUN] No files were written.")


if __name__ == '__main__':
    main()
