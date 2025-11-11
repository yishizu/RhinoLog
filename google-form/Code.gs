// Google Apps Script - Googleフォーム連携スクリプト
// フォーム送信時に自動的にGCPサーバーにユーザー登録を行う

// GCPサーバーのURL
const SERVER_URL = "http://136.111.186.176:5000/api/user/register";

// 研修期間の設定（講師が変更してください）
const TRAINING_START_DATE = "2025-05-01";
const TRAINING_END_DATE = "2025-05-31";

/**
 * フォーム送信時のトリガー関数
 * Googleフォームで「送信時」トリガーを設定してください
 */
function onFormSubmit(e) {
  try {
    // Googleシートから最新の回答を取得
    const form = FormApp.getActiveForm();
    const sheet = SpreadsheetApp.openById(form.getDestinationId()).getSheets()[0];
    const lastRow = sheet.getLastRow();
    const rowData = sheet.getRange(lastRow, 1, 1, sheet.getLastColumn()).getValues()[0];

    // ヘッダー行を取得（列名）
    const headers = sheet.getRange(1, 1, 1, sheet.getLastColumn()).getValues()[0];

    // 列名から値を取得するヘルパー関数
    function getColumnValue(columnName) {
      const index = headers.findIndex(h =>
        h.includes(columnName) ||
        h.toLowerCase().includes(columnName.toLowerCase())
      );
      return index >= 0 ? rowData[index] : null;
    }

    // ユーザーデータを構築
    let userData = {
      username: "",
      full_name: "",
      email: "",
      rhino_experience: "未経験",
      grasshopper_experience: "未経験",
      self_learning_score: 60,
      learning_group: "",
      cad_tools: "",
      modeling_tools: "",
      programming_languages: ""
    };

    // 基本情報
    userData.full_name = getColumnValue("お名前") || getColumnValue("氏名") || getColumnValue("名前") || "";
    userData.email = getColumnValue("メールアドレス") || getColumnValue("メール") || getColumnValue("Email") || "";

    // Rhino経験レベル
    const rhinoExp = getColumnValue("Rhino") || getColumnValue("rhino");
    if (rhinoExp) {
      userData.rhino_experience = rhinoExp;
    }

    // Grasshopper経験レベル
    const ghExp = getColumnValue("Grasshopper") || getColumnValue("grasshopper");
    if (ghExp) {
      userData.grasshopper_experience = ghExp;
    }

    // CAD/BIM経験
    const cadTools = getColumnValue("CAD") || getColumnValue("BIM") || getColumnValue("モデリングツール");
    if (cadTools) {
      userData.cad_tools = String(cadTools);
    }

    // 3Dモデリング/レンダリング経験
    const modelingTools = getColumnValue("3Dモデリング") || getColumnValue("レンダリング");
    if (modelingTools) {
      userData.modeling_tools = String(modelingTools);
    }

    // プログラミング言語経験
    const progLangs = getColumnValue("プログラミング言語") || getColumnValue("プログラミング");
    if (progLangs) {
      userData.programming_languages = String(progLangs);
    }

    // 自己学習能力
    const selfLearning = getColumnValue("新しいソフトウェア") || getColumnValue("自己学習");
    if (selfLearning) {
      if (typeof selfLearning === 'number') {
        userData.self_learning_score = selfLearning * 20; // 1-5 → 20-100
      } else if (String(selfLearning).includes("⭐")) {
        const starCount = (String(selfLearning).match(/⭐/g) || []).length;
        userData.self_learning_score = starCount * 20;
      } else {
        const numbers = String(selfLearning).match(/\d+/);
        if (numbers) {
          userData.self_learning_score = parseInt(numbers[0]) * 20;
        }
      }
    }

    // 学習グループを生成（送信日ベース: YYYYMMDD）
    const timestamp = rowData[0]; // 最初の列はタイムスタンプ
    const submitDate = new Date(timestamp);
    const year = submitDate.getFullYear();
    const month = String(submitDate.getMonth() + 1).padStart(2, '0');
    const day = String(submitDate.getDate()).padStart(2, '0');
    userData.learning_group = `${year}${month}${day}`;

    // 必須フィールドのチェック
    if (!userData.full_name || !userData.email) {
      Logger.log("Error: Missing required fields");
      Logger.log(userData);
      return;
    }

    // ユニークなユーザーIDを自動生成
    userData.username = generateUniqueUserId();

    // デフォルト期間を取得（管理者が設定した期間）
    const defaultPeriod = getDefaultPeriod();
    userData.start_date = defaultPeriod.start_date;
    userData.end_date = defaultPeriod.end_date;

    // ログ出力（確認用）
    Logger.log("=== ユーザー登録データ ===");
    Logger.log(`氏名: ${userData.full_name}`);
    Logger.log(`Email: ${userData.email}`);
    Logger.log(`Username: ${userData.username}`);
    Logger.log(`Rhino経験: ${userData.rhino_experience}`);
    Logger.log(`Grasshopper経験: ${userData.grasshopper_experience}`);
    Logger.log(`自己学習スコア: ${userData.self_learning_score}`);
    Logger.log(`学習グループ: ${userData.learning_group}`);

    // GCPサーバーにPOST
    const options = {
      "method": "post",
      "contentType": "application/json",
      "payload": JSON.stringify(userData),
      "muteHttpExceptions": true
    };

    const response = UrlFetchApp.fetch(SERVER_URL, options);
    const responseCode = response.getResponseCode();

    if (responseCode === 200) {
      const responseData = JSON.parse(response.getContentText());
      Logger.log("\n✓ Success: User registered");
      Logger.log(`  Username: ${userData.username}`);
      Logger.log(`  User Level: L${responseData.user_level}`);
      Logger.log(`  Learning Group: ${responseData.learning_group}`);
    } else {
      Logger.log("\n✗ Error: Server returned " + responseCode);
      Logger.log(response.getContentText());
    }

    // 設定ファイルを生成してメール送信
    sendConfigFile(userData);

  } catch (error) {
    Logger.log("Error in onFormSubmit: " + error.toString());
  }
}

/**
 * 設定ファイルを生成してメール送信
 */
function sendConfigFile(userData) {
  try {
    // 設定ファイルの内容（JSON形式）
    const configContent = JSON.stringify({
      user_id: userData.username,
      full_name: userData.full_name,
      email: userData.email,
      start_date: userData.start_date,
      end_date: userData.end_date,
      learning_group: userData.learning_group,
      rhino_experience: userData.rhino_experience,
      grasshopper_experience: userData.grasshopper_experience,
      registered_at: new Date().toISOString()
    }, null, 2);

    // ファイル名
    const fileName = `${userData.username}.geluser`;

    // Blobを作成
    const blob = Utilities.newBlob(configContent, "application/json", fileName);

    // メール送信
    const subject = "GEL Training Log - 設定ファイル";
    const body = `
${userData.full_name} 様

GEL Training Log システムへのご登録ありがとうございます。

━━━━━━━━━━━━━━━━━━━━━━━━━━━━
今回の講習ユーザー名: ${userData.username}
━━━━━━━━━━━━━━━━━━━━━━━━━━━━

添付の設定ファイル「${fileName}」をダウンロードして、
Rhinocerosで登録を完了してください。

【手順】
1. このメールの添付ファイル「${fileName}」をダウンロード
2. Rhinocerosを起動
3. Rhinoコマンドラインで「GELUserLogin」と入力
4. ダウンロードした「${fileName}」ファイルを選択
5. Rhinocerosを再起動
6. 登録完了！

【ログアウト方法】
ログ記録を停止したい場合は、Rhinoコマンドラインで「GELUserLogout」と入力してください。

問題がある場合は管理者までお問い合わせください。

---
GEL Training Log System
    `.trim();

    MailApp.sendEmail({
      to: userData.email,
      subject: subject,
      body: body,
      attachments: [blob]
    });

    Logger.log("Config file sent to: " + userData.email);

  } catch (error) {
    Logger.log("Error in sendConfigFile: " + error.toString());
  }
}

/**
 * ユニークなユーザーIDを生成
 * 形式: user_YYYYMMDD_XXXXXX (例: user_20250511_a3f9c2)
 */
function generateUniqueUserId() {
  const now = new Date();
  const year = now.getFullYear();
  const month = String(now.getMonth() + 1).padStart(2, '0');
  const day = String(now.getDate()).padStart(2, '0');

  // ランダムな6文字の英数字を生成
  const chars = 'abcdefghijklmnopqrstuvwxyz0123456789';
  let randomStr = '';
  for (let i = 0; i < 6; i++) {
    randomStr += chars.charAt(Math.floor(Math.random() * chars.length));
  }

  return `user_${year}${month}${day}_${randomStr}`;
}

/**
 * デフォルトの研修期間を取得
 * スクリプト冒頭で設定した期間を返す
 */
function getDefaultPeriod() {
  return {
    start_date: TRAINING_START_DATE,
    end_date: TRAINING_END_DATE
  };
}

/**
 * アンケート結果をスプレッドシートから取得
 * ダッシュボード等で利用可能
 */
function getSurveyResponses() {
  // フォームに紐づいたスプレッドシートを取得
  const form = FormApp.getActiveForm();
  const responses = form.getResponses();

  const surveyData = [];

  responses.forEach(response => {
    const itemResponses = response.getItemResponses();
    const email = response.getRespondentEmail();
    const timestamp = response.getTimestamp();

    const record = {
      timestamp: timestamp,
      email: email,
      answers: {}
    };

    itemResponses.forEach(itemResponse => {
      const question = itemResponse.getItem().getTitle();
      const answer = itemResponse.getResponse();
      record.answers[question] = answer;
    });

    surveyData.push(record);
  });

  return surveyData;
}

/**
 * 特定ユーザーのアンケート結果を取得
 */
function getSurveyByEmail(userEmail) {
  const allResponses = getSurveyResponses();
  return allResponses.find(r => r.email === userEmail);
}

/**
 * 手動テスト用関数
 * スクリプトエディタで直接実行して動作確認できます
 */
function testRegisterUser() {
  const testData = {
    username: "testuser",
    full_name: "Test User",
    email: "test@example.com",
    start_date: "2025-05-19",
    end_date: "2025-05-30"
  };

  const options = {
    "method": "post",
    "contentType": "application/json",
    "payload": JSON.stringify(testData),
    "muteHttpExceptions": true
  };

  const response = UrlFetchApp.fetch(SERVER_URL, options);
  Logger.log("Response Code: " + response.getResponseCode());
  Logger.log("Response Body: " + response.getContentText());
}
