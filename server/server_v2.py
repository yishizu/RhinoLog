from flask import Flask, request, jsonify
from flask_cors import CORS
import sqlite3
import os
from datetime import datetime
import json
from collections import Counter

app = Flask(__name__)
CORS(app)

DB_PATH = "/home/rhinologs/rhinolog.db"
LOG_BASE_DIR = "/home/rhinologs"

# Load command classification data
COMMAND_CLASSIFICATION = None
def load_command_classification():
    global COMMAND_CLASSIFICATION
    try:
        # Try multiple possible paths (prioritize local server folder)
        possible_paths = [
            os.path.join(os.path.dirname(__file__), 'rhino_commands_actions_classified.json'),  # Same folder as server_v2.py
            '/home/rhinologs/rhino_commands_actions_classified.json',  # Production server path
            'rhino_commands_actions_classified.json'  # Current directory fallback
        ]

        for path in possible_paths:
            if os.path.exists(path):
                with open(path, 'r', encoding='utf-8') as f:
                    COMMAND_CLASSIFICATION = json.load(f)
                    print(f"✓ Command classification loaded from: {path}")
                    return

        print("⚠ Warning: Command classification file not found")
    except Exception as e:
        print(f"⚠ Warning: Could not load command classification: {e}")

# レベル判定ロジック
def get_experience_score(level_str):
    """経験レベルをスコアに変換"""
    mapping = {
        '未経験': 0,
        '初心者': 30,
        '中級者': 70,
        'エキスパート': 100,
        'beginner': 30,
        'intermediate': 70,
        'expert': 100
    }
    level_lower = str(level_str).lower()
    for key, value in mapping.items():
        if key.lower() in level_lower:
            return value
    return 0

def calculate_cad_experience_score(cad_tools, modeling_tools, programming_langs):
    """CAD経験スコアを計算"""
    score = 0

    # Parse tool lists (semicolon or comma separated)
    cad_list = str(cad_tools).replace(',', ';').split(';') if cad_tools else []
    modeling_list = str(modeling_tools).replace(',', ';').split(';') if modeling_tools else []
    prog_list = str(programming_langs).replace(',', ';').split(';') if programming_langs else []

    # Count unique tools: 8 points each
    unique_tools = set()
    for tools in [cad_list, modeling_list]:
        for tool in tools:
            if tool and tool.strip() and tool.strip() != 'nan':
                unique_tools.add(tool.strip())
    score += len(unique_tools) * 8

    # Rhino bonus: +3
    all_tools = ' '.join([str(cad_tools), str(modeling_tools)])
    if 'Rhino' in all_tools:
        score += 3

    # Revit bonus: +3
    if 'Revit' in all_tools:
        score += 3

    # Programming experience: 2 points each, Python and C# get extra bonus
    for prog in prog_list:
        if prog and prog.strip() and prog.strip() != 'nan':
            if 'Python' in prog:
                score += 2 + 3  # Base 2 + Python bonus 3
            elif 'C#' in prog or 'CSharp' in prog:
                score += 2 + 2  # Base 2 + C# bonus 2
            else:
                score += 2

    return score

def calculate_rhino_gh_composite_score(rhino_score, gh_score):
    """RhinoGH複合スコアを計算"""
    if rhino_score == 0 and gh_score == 0:
        return 0
    elif rhino_score > 0 and gh_score == 0:
        return rhino_score * 0.6
    elif rhino_score == 0 and gh_score > 0:
        return gh_score * 0.4
    else:
        return rhino_score * 0.6 + gh_score * 0.4

def calculate_overall_score(technical_score, cad_score, self_learning_score, rhino_gh_score):
    """総合スコアを計算"""
    return (technical_score * 0.25 +
            cad_score * 0.15 +
            self_learning_score * 0.20 +
            rhino_gh_score * 0.40)

def determine_user_level(rhino_score, gh_score, technical_score, self_learning_score, cad_experience_score=0):
    """ユーザーレベルを判定（1-5）

    CAD経験スコアを含めた総合評価でレベルを判定
    """
    # RhinoGH複合スコアを計算
    rhino_gh_score = calculate_rhino_gh_composite_score(rhino_score, gh_score)

    # 総合スコアを計算
    overall_score = calculate_overall_score(technical_score, cad_experience_score,
                                           self_learning_score, rhino_gh_score)

    # Level 5 判定: 最高レベル - 全ての条件を満たす
    if (rhino_score >= 70 and gh_score >= 70 and
        technical_score >= 60 and self_learning_score >= 60):
        return 5

    # Level 4 判定: 上級レベル - 総合スコアと個別条件
    if ((rhino_score >= 70 and gh_score >= 70 and technical_score >= 50) or
        (rhino_score >= 30 and gh_score >= 30 and technical_score >= 77) or
        (rhino_score >= 70 and gh_score >= 30 and technical_score >= 50) or
        (overall_score >= 65)):  # 総合スコアでの判定を追加
        return 4

    # Level 3 判定: 中級レベル
    if ((rhino_score >= 70 and gh_score <= 30) or
        (rhino_score == 30 and gh_score == 30) or
        (overall_score >= 45)):  # 総合スコアでの判定を追加
        return 3

    # Level 2 判定: 初級レベル
    if (rhino_score == 30 and gh_score == 0) or (overall_score >= 25):
        return 2

    # Level 1 判定: 入門レベル
    return 1

# データベース初期化
def init_db():
    os.makedirs(LOG_BASE_DIR, exist_ok=True)
    conn = sqlite3.connect(DB_PATH)
    c = conn.cursor()

    # ユーザーテーブル
    c.execute('''CREATE TABLE IF NOT EXISTS users (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        username TEXT UNIQUE NOT NULL,
        full_name TEXT NOT NULL,
        email TEXT,
        organization TEXT,
        start_date TEXT NOT NULL,
        end_date TEXT NOT NULL,
        created_at TEXT NOT NULL,
        user_level INTEGER DEFAULT 1,
        learning_group TEXT,
        rhino_experience TEXT,
        grasshopper_experience TEXT,
        technical_score REAL DEFAULT 0,
        self_learning_score REAL DEFAULT 60,
        cad_tools TEXT,
        modeling_tools TEXT,
        programming_languages TEXT,
        cad_experience_score INTEGER DEFAULT 0
    )''')

    # ログテーブル
    c.execute('''CREATE TABLE IF NOT EXISTS logs (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        timestamp TEXT NOT NULL,
        username TEXT NOT NULL,
        action TEXT NOT NULL,
        detail TEXT,
        document_name TEXT,
        created_at TEXT NOT NULL,
        FOREIGN KEY (username) REFERENCES users(username)
    )''')

    conn.commit()
    conn.close()

# ユーザー登録（Googleフォームから）
@app.route('/api/user/register', methods=['POST'])
def register_user():
    try:
        data = request.json
        required = ['username', 'full_name', 'start_date', 'end_date']

        for field in required:
            if field not in data:
                return jsonify({'error': f'Missing field: {field}'}), 400

        # レベル判定パラメータを取得
        rhino_exp = data.get('rhino_experience', '未経験')
        gh_exp = data.get('grasshopper_experience', '未経験')
        technical_score = float(data.get('technical_score', 0))
        self_learning = float(data.get('self_learning_score', 60))
        learning_group = data.get('learning_group', '')

        # CAD経験データを取得
        cad_tools = data.get('cad_tools', '')
        modeling_tools = data.get('modeling_tools', '')
        programming_langs = data.get('programming_languages', '')

        # スコア計算
        rhino_score = get_experience_score(rhino_exp)
        gh_score = get_experience_score(gh_exp)
        cad_experience_score = calculate_cad_experience_score(cad_tools, modeling_tools, programming_langs)

        # レベル判定（CAD経験スコアを含む）
        user_level = determine_user_level(rhino_score, gh_score, technical_score, self_learning, cad_experience_score)

        conn = sqlite3.connect(DB_PATH)
        c = conn.cursor()

        # 既存チェック
        c.execute('SELECT username FROM users WHERE username = ?', (data['username'],))
        if c.fetchone():
            # 既存ユーザーの更新
            c.execute('''UPDATE users SET
                full_name = ?, email = ?, organization = ?,
                start_date = ?, end_date = ?, user_level = ?,
                learning_group = ?, rhino_experience = ?, grasshopper_experience = ?,
                technical_score = ?, self_learning_score = ?,
                cad_tools = ?, modeling_tools = ?, programming_languages = ?, cad_experience_score = ?
                WHERE username = ?''',
                (data['full_name'], data.get('email', ''), data.get('organization', ''),
                 data['start_date'], data['end_date'], user_level,
                 learning_group, rhino_exp, gh_exp, technical_score, self_learning,
                 cad_tools, modeling_tools, programming_langs, cad_experience_score,
                 data['username']))
        else:
            # 新規登録
            c.execute('''INSERT INTO users
                (username, full_name, email, organization, start_date, end_date, created_at,
                 user_level, learning_group, rhino_experience, grasshopper_experience,
                 technical_score, self_learning_score,
                 cad_tools, modeling_tools, programming_languages, cad_experience_score)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)''',
                (data['username'], data['full_name'], data.get('email', ''),
                 data.get('organization', ''), data['start_date'], data['end_date'],
                 datetime.now().isoformat(), user_level, learning_group,
                 rhino_exp, gh_exp, technical_score, self_learning,
                 cad_tools, modeling_tools, programming_langs, cad_experience_score))

        conn.commit()
        conn.close()

        return jsonify({
            'status': 'success',
            'message': 'User registered',
            'user_level': user_level,
            'learning_group': learning_group,
            'cad_experience_score': cad_experience_score
        }), 200

    except Exception as e:
        return jsonify({'error': str(e)}), 500

# ユーザー情報取得（Rhinoプラグインが使用）
@app.route('/api/user/<username>', methods=['GET'])
def get_user(username):
    try:
        conn = sqlite3.connect(DB_PATH)
        c = conn.cursor()
        c.execute('''SELECT id, username, full_name, email, organization, start_date, end_date,
                     created_at, user_level, learning_group, rhino_experience, grasshopper_experience,
                     technical_score, self_learning_score, cad_tools, modeling_tools,
                     programming_languages, cad_experience_score
                     FROM users WHERE username = ?''', (username,))
        row = c.fetchone()
        conn.close()

        if not row:
            return jsonify({'error': 'User not found', 'registered': False}), 404

        user = {
            'username': row[1],
            'full_name': row[2],
            'email': row[3],
            'organization': row[4],
            'start_date': row[5],
            'end_date': row[6],
            'registered': True,
            'user_level': row[8],
            'learning_group': row[9],
            'rhino_experience': row[10],
            'grasshopper_experience': row[11],
            'technical_score': row[12],
            'self_learning_score': row[13],
            'cad_tools': row[14],
            'modeling_tools': row[15],
            'programming_languages': row[16],
            'cad_experience_score': row[17]
        }

        return jsonify(user), 200

    except Exception as e:
        return jsonify({'error': str(e)}), 500

# ログアップロード（Rhinoプラグインから）
@app.route('/api/log/upload', methods=['POST'])
def upload_log():
    try:
        data = request.json
        required = ['Timestamp', 'UserID', 'Action', 'Detail', 'DocumentName']

        for field in required:
            if field not in data:
                return jsonify({'error': f'Missing field: {field}'}), 400

        conn = sqlite3.connect(DB_PATH)
        c = conn.cursor()

        # ユーザーが登録されているか確認
        c.execute('SELECT username FROM users WHERE username = ?', (data['UserID'],))
        if not c.fetchone():
            conn.close()
            return jsonify({'error': 'User not registered'}), 403

        # ログ保存
        c.execute('''INSERT INTO logs
            (timestamp, username, action, detail, document_name, created_at)
            VALUES (?, ?, ?, ?, ?, ?)''',
            (data['Timestamp'], data['UserID'], data['Action'], data['Detail'],
             data['DocumentName'], datetime.now().isoformat()))

        conn.commit()
        conn.close()

        return jsonify({'status': 'success'}), 200

    except Exception as e:
        return jsonify({'error': str(e)}), 500

# ユーザー一覧取得（可視化アプリ用）
@app.route('/api/users', methods=['GET'])
def get_users():
    try:
        conn = sqlite3.connect(DB_PATH)
        c = conn.cursor()
        c.execute('''SELECT username, full_name, email, organization, start_date, end_date,
                     user_level, learning_group, rhino_experience, grasshopper_experience,
                     technical_score, self_learning_score, cad_tools, modeling_tools,
                     programming_languages, cad_experience_score
                     FROM users''')
        rows = c.fetchall()
        conn.close()

        users = []
        for row in rows:
            users.append({
                'username': row[0],
                'full_name': row[1],
                'email': row[2],
                'organization': row[3],
                'start_date': row[4],
                'end_date': row[5],
                'user_level': row[6],
                'learning_group': row[7],
                'rhino_experience': row[8],
                'grasshopper_experience': row[9],
                'technical_score': row[10],
                'self_learning_score': row[11],
                'cad_tools': row[12],
                'modeling_tools': row[13],
                'programming_languages': row[14],
                'cad_experience_score': row[15]
            })

        return jsonify(users), 200

    except Exception as e:
        return jsonify({'error': str(e)}), 500

# ログ取得（可視化アプリ用）
@app.route('/api/logs', methods=['GET'])
def get_logs():
    try:
        username = request.args.get('username')
        start_date = request.args.get('start_date')
        end_date = request.args.get('end_date')

        conn = sqlite3.connect(DB_PATH)
        c = conn.cursor()

        query = 'SELECT timestamp, username, action, detail, document_name FROM logs WHERE 1=1'
        params = []

        if username:
            query += ' AND username = ?'
            params.append(username)

        if start_date:
            query += ' AND timestamp >= ?'
            params.append(start_date)

        if end_date:
            query += ' AND timestamp <= ?'
            params.append(end_date)

        query += ' ORDER BY timestamp DESC LIMIT 10000'

        c.execute(query, params)
        rows = c.fetchall()
        conn.close()

        logs = []
        for row in rows:
            logs.append({
                'timestamp': row[0],
                'username': row[1],
                'action': row[2],
                'detail': row[3],
                'document_name': row[4]
            })

        return jsonify(logs), 200

    except Exception as e:
        return jsonify({'error': str(e)}), 500

# 統計情報取得（可視化アプリ用）
@app.route('/api/stats/<username>', methods=['GET'])
def get_stats(username):
    try:
        conn = sqlite3.connect(DB_PATH)
        c = conn.cursor()

        # コマンド使用頻度
        c.execute('''SELECT action, COUNT(*) as count
                     FROM logs
                     WHERE username = ? AND action = 'Command'
                     GROUP BY detail
                     ORDER BY count DESC
                     LIMIT 20''', (username,))

        commands = [{'action': row[0], 'count': row[1]} for row in c.fetchall()]

        # 総操作数
        c.execute('SELECT COUNT(*) FROM logs WHERE username = ?', (username,))
        total_logs = c.fetchone()[0]

        conn.close()

        return jsonify({
            'username': username,
            'total_logs': total_logs,
            'top_commands': commands
        }), 200

    except Exception as e:
        return jsonify({'error': str(e)}), 500

# 学習グループ一覧取得
@app.route('/api/groups', methods=['GET'])
def get_learning_groups():
    try:
        conn = sqlite3.connect(DB_PATH)
        c = conn.cursor()

        # グループ別にユーザーを集計
        c.execute('''SELECT learning_group, COUNT(*) as user_count,
                     GROUP_CONCAT(username) as users,
                     AVG(user_level) as avg_level
                     FROM users
                     WHERE learning_group IS NOT NULL AND learning_group != ''
                     GROUP BY learning_group
                     ORDER BY learning_group DESC''')

        rows = c.fetchall()
        conn.close()

        groups = []
        for row in rows:
            groups.append({
                'group_id': row[0],
                'user_count': row[1],
                'users': row[2].split(',') if row[2] else [],
                'avg_level': round(row[3], 1) if row[3] else 0
            })

        return jsonify(groups), 200

    except Exception as e:
        return jsonify({'error': str(e)}), 500

# 特定グループのユーザー一覧取得
@app.route('/api/group/<group_id>/users', methods=['GET'])
def get_group_users(group_id):
    try:
        conn = sqlite3.connect(DB_PATH)
        c = conn.cursor()

        c.execute('''SELECT username, full_name, user_level,
                     rhino_experience, grasshopper_experience,
                     technical_score, self_learning_score
                     FROM users
                     WHERE learning_group = ?
                     ORDER BY user_level DESC''', (group_id,))

        rows = c.fetchall()
        conn.close()

        users = []
        for row in rows:
            users.append({
                'username': row[0],
                'full_name': row[1],
                'user_level': row[2],
                'rhino_experience': row[3],
                'grasshopper_experience': row[4],
                'technical_score': row[5],
                'self_learning_score': row[6]
            })

        return jsonify(users), 200

    except Exception as e:
        return jsonify({'error': str(e)}), 500

# スクリーニングテストのスコアを更新
@app.route('/api/user/update-score', methods=['POST'])
def update_user_score():
    try:
        data = request.json
        required = ['email', 'technical_score']

        for field in required:
            if field not in data:
                return jsonify({'error': f'Missing field: {field}'}), 400

        email = data['email']
        technical_score = float(data['technical_score'])

        # カテゴリ別スコアと問題別スコア（オプション）
        category_scores = data.get('category_scores', {})
        question_scores = data.get('question_scores', {})

        conn = sqlite3.connect(DB_PATH)
        c = conn.cursor()

        # メールアドレスでユーザーを検索
        c.execute('''SELECT username, rhino_experience, grasshopper_experience, self_learning_score,
                     cad_tools, modeling_tools, programming_languages
                     FROM users WHERE email = ?''', (email,))
        user_row = c.fetchone()

        if not user_row:
            conn.close()
            return jsonify({'error': 'User not found with this email'}), 404

        username = user_row[0]
        rhino_exp = user_row[1] if user_row[1] else '未経験'
        gh_exp = user_row[2] if user_row[2] else '未経験'
        self_learning = user_row[3] if user_row[3] else 60
        cad_tools = user_row[4] if user_row[4] else ''
        modeling_tools = user_row[5] if user_row[5] else ''
        programming_langs = user_row[6] if user_row[6] else ''

        # スコアを計算してレベルを再判定
        rhino_score = get_experience_score(rhino_exp)
        gh_score = get_experience_score(gh_exp)
        cad_experience_score = calculate_cad_experience_score(cad_tools, modeling_tools, programming_langs)
        user_level = determine_user_level(rhino_score, gh_score, technical_score, self_learning, cad_experience_score)

        # ユーザーのtechnical_scoreとuser_levelとcad_experience_scoreを更新
        c.execute('''UPDATE users SET
            technical_score = ?, user_level = ?, cad_experience_score = ?
            WHERE username = ?''',
            (technical_score, user_level, cad_experience_score, username))

        # screening_resultsテーブルに詳細データを保存（存在する場合は更新）
        if category_scores or question_scores:
            # テーブルが存在しない場合は作成
            c.execute('''CREATE TABLE IF NOT EXISTS screening_results (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                username TEXT UNIQUE NOT NULL,
                technical_score REAL NOT NULL,
                category_scores TEXT,
                question_scores TEXT,
                submitted_at TEXT NOT NULL,
                FOREIGN KEY (username) REFERENCES users(username)
            )''')

            # データをJSON文字列に変換
            category_scores_json = json.dumps(category_scores) if category_scores else None
            question_scores_json = json.dumps(question_scores) if question_scores else None

            # 既存データがあれば更新、なければ挿入
            c.execute('SELECT username FROM screening_results WHERE username = ?', (username,))
            if c.fetchone():
                c.execute('''UPDATE screening_results SET
                    technical_score = ?, category_scores = ?, question_scores = ?, submitted_at = ?
                    WHERE username = ?''',
                    (technical_score, category_scores_json, question_scores_json,
                     datetime.now().isoformat(), username))
            else:
                c.execute('''INSERT INTO screening_results
                    (username, technical_score, category_scores, question_scores, submitted_at)
                    VALUES (?, ?, ?, ?, ?)''',
                    (username, technical_score, category_scores_json, question_scores_json,
                     datetime.now().isoformat()))

        conn.commit()
        conn.close()

        return jsonify({
            'status': 'success',
            'message': 'Technical score updated',
            'username': username,
            'technical_score': technical_score,
            'user_level': user_level,
            'cad_experience_score': cad_experience_score
        }), 200

    except Exception as e:
        return jsonify({'error': str(e)}), 500

# スクリーニングテスト結果取得
@app.route('/api/screening/<username>', methods=['GET'])
def get_screening_results(username):
    try:
        conn = sqlite3.connect(DB_PATH)
        c = conn.cursor()

        c.execute('''SELECT technical_score, category_scores, question_scores, submitted_at
                     FROM screening_results
                     WHERE username = ?''', (username,))
        row = c.fetchone()
        conn.close()

        if not row:
            return jsonify({'error': 'Screening results not found'}), 404

        # JSONデータをパース
        category_scores = json.loads(row[1]) if row[1] else {}
        question_scores = json.loads(row[2]) if row[2] else {}

        return jsonify({
            'username': username,
            'technical_score': row[0],
            'category_scores': category_scores,
            'question_scores': question_scores,
            'submitted_at': row[3]
        }), 200

    except Exception as e:
        return jsonify({'error': str(e)}), 500

def classify_command(command_name):
    """Classify a Rhino command into workflow and detail categories"""
    if not COMMAND_CLASSIFICATION or not command_name:
        return None, None

    # Search in commands mapping
    commands = COMMAND_CLASSIFICATION.get('commands', {})
    if command_name in commands:
        cmd_info = commands[command_name]
        return cmd_info.get('workflow_category'), cmd_info.get('detail_category')

    # Search in actions mapping
    actions = COMMAND_CLASSIFICATION.get('actions', {})
    if command_name in actions:
        action_info = actions[command_name]
        return action_info.get('workflow_category'), action_info.get('detail_category')

    return None, None

# 分類済みログ取得（可視化アプリ用）
@app.route('/api/logs/classified', methods=['GET'])
def get_logs_classified():
    """Get logs with workflow and detail category classification"""
    try:
        username = request.args.get('username')
        start_date = request.args.get('start_date')
        end_date = request.args.get('end_date')

        conn = sqlite3.connect(DB_PATH)
        c = conn.cursor()

        query = 'SELECT timestamp, username, action, detail, document_name FROM logs WHERE 1=1'
        params = []

        if username:
            query += ' AND username = ?'
            params.append(username)

        if start_date:
            query += ' AND timestamp >= ?'
            params.append(start_date)

        if end_date:
            query += ' AND timestamp <= ?'
            params.append(end_date)

        query += ' ORDER BY timestamp DESC LIMIT 10000'

        c.execute(query, params)
        rows = c.fetchall()
        conn.close()

        # Classify logs
        classified_logs = []
        workflow_category_counts = Counter()
        detail_category_counts = Counter()

        for row in rows:
            timestamp, username, action, detail, document_name = row

            # Extract command name from detail
            command_name = None
            if action == 'Command Started' and detail:
                command_name = detail.split(';')[0].strip()

            # Classify command
            workflow_cat, detail_cat = classify_command(command_name) if command_name else (None, None)

            classified_logs.append({
                'timestamp': timestamp,
                'username': username,
                'action': action,
                'detail': detail,
                'document_name': document_name,
                'command': command_name,
                'workflow_category': workflow_cat,
                'detail_category': detail_cat
            })

            # Count categories
            if workflow_cat:
                workflow_category_counts[workflow_cat] += 1
            if detail_cat:
                detail_category_counts[detail_cat] += 1

        # Get workflow category info
        workflow_categories_info = {}
        if COMMAND_CLASSIFICATION:
            wf_cats = COMMAND_CLASSIFICATION.get('workflow_categories', {})
            for cat_key, cat_info in wf_cats.items():
                workflow_categories_info[cat_key] = {
                    'name_ja': cat_info.get('name_ja'),
                    'name_en': cat_info.get('name_en'),
                    'description': cat_info.get('description')
                }

        return jsonify({
            'logs': classified_logs,
            'workflow_category_counts': dict(workflow_category_counts),
            'detail_category_counts': dict(detail_category_counts),
            'workflow_categories_info': workflow_categories_info,
            'total_logs': len(classified_logs),
            'classified_logs': sum(1 for log in classified_logs if log['workflow_category'])
        }), 200

    except Exception as e:
        return jsonify({'error': str(e)}), 500

# ワークフロー統計取得
@app.route('/api/stats/workflow/<username>', methods=['GET'])
def get_workflow_stats(username):
    """Get workflow category statistics for a user"""
    try:
        conn = sqlite3.connect(DB_PATH)
        c = conn.cursor()

        c.execute('''SELECT timestamp, action, detail FROM logs
                     WHERE username = ?
                     ORDER BY timestamp ASC''', (username,))
        rows = c.fetchall()
        conn.close()

        if not rows:
            return jsonify({'error': 'No logs found for user'}), 404

        # Classify and analyze
        workflow_stats = Counter()
        detail_stats = Counter()
        timeline = []

        for timestamp, action, detail in rows:
            if action == 'Command Started' and detail:
                command_name = detail.split(';')[0].strip()
                workflow_cat, detail_cat = classify_command(command_name)

                if workflow_cat:
                    workflow_stats[workflow_cat] += 1
                if detail_cat:
                    detail_stats[detail_cat] += 1

                if workflow_cat:
                    timeline.append({
                        'timestamp': timestamp,
                        'workflow_category': workflow_cat,
                        'detail_category': detail_cat,
                        'command': command_name
                    })

        # Get category names
        workflow_names = {}
        if COMMAND_CLASSIFICATION:
            wf_cats = COMMAND_CLASSIFICATION.get('workflow_categories', {})
            for cat_key, cat_info in wf_cats.items():
                workflow_names[cat_key] = cat_info.get('name_ja', cat_key)

        return jsonify({
            'username': username,
            'workflow_category_counts': dict(workflow_stats),
            'detail_category_counts': dict(detail_stats),
            'workflow_category_names': workflow_names,
            'timeline': timeline[-100:],  # Last 100 classified actions
            'total_classified_actions': len(timeline)
        }), 200

    except Exception as e:
        return jsonify({'error': str(e)}), 500

#  Action Groups API (10-minute intervals)
@app.route('/api/action-groups/<username>', methods=['GET'])
def get_action_groups(username):
    """Get user's actions grouped into 10-minute intervals"""
    try:
        conn = sqlite3.connect(DB_PATH)
        c = conn.cursor()

        c.execute('''SELECT timestamp, action, detail, document_name FROM logs
                     WHERE username = ?
                     ORDER BY timestamp ASC''', (username,))
        rows = c.fetchall()
        conn.close()

        if not rows:
            return jsonify({'error': 'No logs found for user'}), 404

        # Group actions into 10-minute intervals
        groups = []
        current_group = []
        group_start_time = None
        idle_threshold_minutes = 10

        for timestamp_str, action, detail, document_name in rows:
            try:
                timestamp = datetime.strptime(timestamp_str, '%Y-%m-%d %H:%M:%S')
            except:
                continue

            if group_start_time is None:
                # Start first group
                group_start_time = timestamp
                current_group = [{
                    'timestamp': timestamp_str,
                    'action': action,
                    'detail': detail,
                    'document_name': document_name
                }]
            else:
                # Check if within 10 minutes of group start
                time_diff_minutes = (timestamp - group_start_time).total_seconds() / 60

                if time_diff_minutes <= idle_threshold_minutes:
                    # Add to current group
                    current_group.append({
                        'timestamp': timestamp_str,
                        'action': action,
                        'detail': detail,
                        'document_name': document_name
                    })
                else:
                    # Save current group and start new one
                    if current_group:
                        groups.append(analyze_action_group(current_group, group_start_time))

                    group_start_time = timestamp
                    current_group = [{
                        'timestamp': timestamp_str,
                        'action': action,
                        'detail': detail,
                        'document_name': document_name
                    }]

        # Don't forget last group
        if current_group:
            groups.append(analyze_action_group(current_group, group_start_time))

        return jsonify({
            'username': username,
            'total_groups': len(groups),
            'groups': groups
        }), 200

    except Exception as e:
        return jsonify({'error': str(e)}), 500

def analyze_action_group(group_actions, start_time):
    """Analyze a single 10-minute action group"""
    if not group_actions:
        return None

    # Calculate basic metrics
    end_time = datetime.strptime(group_actions[-1]['timestamp'], '%Y-%m-%d %H:%M:%S')
    duration_minutes = (end_time - start_time).total_seconds() / 60
    total_actions = len(group_actions)

    # Analyze workflow categories
    workflow_counts = Counter()
    detail_counts = Counter()

    for action_dict in group_actions:
        action = action_dict['action']
        detail = action_dict.get('detail', '')

        # Classify command
        if action == 'Command Started' and detail:
            command_name = detail.split(';')[0].strip()
            workflow_cat, detail_cat = classify_command(command_name)

            if workflow_cat:
                workflow_counts[workflow_cat] += 1
            if detail_cat:
                detail_counts[detail_cat] += 1

    # Get workflow category names
    workflow_names = {}
    if COMMAND_CLASSIFICATION:
        wf_cats = COMMAND_CLASSIFICATION.get('workflow_categories', {})
        for cat_key, cat_info in wf_cats.items():
            workflow_names[cat_key] = cat_info.get('name_ja', cat_key)

    # Determine dominant activity
    dominant_workflow = None
    if workflow_counts:
        dominant_workflow = workflow_counts.most_common(1)[0][0]

    return {
        'start_time': start_time.strftime('%Y-%m-%d %H:%M:%S'),
        'end_time': end_time.strftime('%Y-%m-%d %H:%M:%S'),
        'duration_minutes': round(duration_minutes, 2),
        'total_actions': total_actions,
        'actions_per_minute': round(total_actions / max(duration_minutes, 0.1), 2),
        'workflow_categories': dict(workflow_counts),
        'workflow_category_names': workflow_names,
        'detail_categories': dict(detail_counts),
        'dominant_workflow': workflow_names.get(dominant_workflow, dominant_workflow) if dominant_workflow else 'Unknown',
        'actions': group_actions
    }

# ヘルスチェック
@app.route('/api/health', methods=['GET'])
def health_check():
    return jsonify({'status': 'ok', 'timestamp': datetime.now().isoformat()}), 200

if __name__ == '__main__':
    init_db()
    load_command_classification()  # Load classification data on startup
    app.run(host='0.0.0.0', port=5000, debug=False)
