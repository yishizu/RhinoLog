import json

# Load classification file
with open('rhino_commands_actions_classified.json', 'r', encoding='utf-8') as f:
    data = json.load(f)

mapping = data.get('classification_mapping', {})

# Create simplified command -> detail_category mapping
command_detail_map = {}

for cmd_name, cmd_info in sorted(mapping.items()):
    workflow_cat = cmd_info.get('workflow_category', 'Unknown')
    detail_cat = cmd_info.get('detail_category', 'Unknown')

    command_detail_map[cmd_name] = {
        'workflow_category': workflow_cat,
        'detail_category': detail_cat
    }

# Save to new file
output = {
    'description': 'Simple command to detail_category mapping',
    'total_commands': len(command_detail_map),
    'commands': command_detail_map
}

with open('command_detail_mapping.json', 'w', encoding='utf-8') as f:
    json.dump(output, f, ensure_ascii=False, indent=2)

print(f'✓ Created mapping for {len(command_detail_map)} commands')

# Print first 50 for verification
print('\nFirst 50 commands:')
for i, (cmd, info) in enumerate(list(command_detail_map.items())[:50], 1):
    print(f"{i}. {cmd}: {info['detail_category']}")

# Print detail_category distribution
from collections import Counter
detail_counts = Counter(info['detail_category'] for info in command_detail_map.values())
print(f'\nDetail category distribution:')
for detail_cat, count in detail_counts.most_common():
    print(f'  {detail_cat}: {count} commands')
