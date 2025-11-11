import json

# Simulate server logic
COMMAND_CLASSIFICATION = None
DETAIL_CATEGORY_NAMES = None

# Load classification
try:
    with open('rhino_commands_actions_classified.json', 'r', encoding='utf-8') as f:
        COMMAND_CLASSIFICATION = json.load(f)
        mapping_count = len(COMMAND_CLASSIFICATION.get('classification_mapping', {}))
        print(f"✓ Command classification loaded")
        print(f"  Total commands in mapping: {mapping_count}")
except Exception as e:
    print(f"⚠ Error loading classification: {e}")

# Load detail category names
try:
    with open('detail_category_names.json', 'r', encoding='utf-8') as f:
        DETAIL_CATEGORY_NAMES = json.load(f).get('detail_category_names', {})
        print(f"✓ Detail category names loaded: {len(DETAIL_CATEGORY_NAMES)} categories")
except Exception as e:
    print(f"⚠ Error loading detail names: {e}")

def classify_command(command_name):
    """Classify a Rhino command into workflow and detail categories"""
    if not COMMAND_CLASSIFICATION:
        print("⚠⚠⚠ COMMAND_CLASSIFICATION is not loaded!")
        return None, None

    if not command_name:
        return None, None

    # Search in classification_mapping
    mapping = COMMAND_CLASSIFICATION.get('classification_mapping', {})
    if command_name in mapping:
        cmd_info = mapping[command_name]
        return cmd_info.get('workflow_category'), cmd_info.get('detail_category')

    return None, None

# Test with sample actions
print("\n" + "="*60)
print("Testing classification with sample action data:")
print("="*60)

# Simulate typical action data from database
sample_actions = [
    {'action': 'Command Started', 'detail': 'Box', 'timestamp': '2025-01-15 10:00:00'},
    {'action': 'Command Started', 'detail': 'Move;MoveVertical', 'timestamp': '2025-01-15 10:01:00'},
    {'action': 'Command Started', 'detail': 'Line', 'timestamp': '2025-01-15 10:02:00'},
    {'action': 'Mouse Move', 'detail': '', 'timestamp': '2025-01-15 10:03:00'},
    {'action': 'Command Started', 'detail': 'Circle;CircleCenter', 'timestamp': '2025-01-15 10:04:00'},
]

for action_dict in sample_actions:
    action = action_dict['action']
    detail = action_dict.get('detail', '')

    print(f"\nAction: {action}, Detail: '{detail}'")

    if action == 'Command Started' and detail:
        command_name = detail.split(';')[0].strip()
        workflow_cat, detail_cat = classify_command(command_name)

        print(f"  Command extracted: '{command_name}'")
        print(f"  Workflow: {workflow_cat}")
        print(f"  Detail: {detail_cat}")

        if detail_cat and DETAIL_CATEGORY_NAMES:
            detail_name = DETAIL_CATEGORY_NAMES.get(detail_cat, detail_cat)
            print(f"  Detail Name (JP): {detail_name}")
    else:
        print(f"  → Skipped (not a command or no detail)")
