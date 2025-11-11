from flask import Flask, request, jsonify
from flask_cors import CORS
import os
from datetime import datetime
import csv

app = Flask(__name__)
CORS(app)  # クロスオリジンリクエストを許可

# ログ保存先のベースディレクトリ
LOG_BASE_DIR = "/home/rhinologs"

@app.route('/api/log/upload', methods=['POST'])
def upload_log():
    try:
        data = request.json

        # 必須フィールドの確認
        required_fields = ['Timestamp', 'UserID', 'Action', 'Detail', 'DocumentName']
        for field in required_fields:
            if field not in data:
                return jsonify({'error': f'Missing field: {field}'}), 400

        user_id = data['UserID']
        doc_name = data['DocumentName']

        # ユーザーごとのディレクトリを作成
        user_dir = os.path.join(LOG_BASE_DIR, user_id)
        os.makedirs(user_dir, exist_ok=True)

        # ログファイルパス
        log_file = os.path.join(user_dir, f"{user_id}_{doc_name}_Log.csv")

        # ファイルが存在しない場合はヘッダーを書き込む
        file_exists = os.path.exists(log_file)

        with open(log_file, 'a', newline='', encoding='utf-8') as f:
            writer = csv.writer(f)
            if not file_exists:
                writer.writerow(['Timestamp', 'UserID', 'Action', 'Detail'])

            writer.writerow([
                data['Timestamp'],
                data['UserID'],
                data['Action'],
                data['Detail']
            ])

        return jsonify({'status': 'success', 'message': 'Log saved'}), 200

    except Exception as e:
        return jsonify({'error': str(e)}), 500

@app.route('/api/health', methods=['GET'])
def health_check():
    return jsonify({'status': 'ok', 'timestamp': datetime.now().isoformat()}), 200

if __name__ == '__main__':
    # ログディレクトリを作成
    os.makedirs(LOG_BASE_DIR, exist_ok=True)

    # 0.0.0.0 でリッスンして外部からアクセス可能にする
    app.run(host='0.0.0.0', port=5000, debug=False)
