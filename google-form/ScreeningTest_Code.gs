// Google Apps Script - スクリーニングテスト自動採点スクリプト
// Googleフォームのクイズ機能で自動採点されたスコアをGCPサーバーに送信

// GCPサーバーのURL
// Google Apps ScriptはHTTPSが推奨されていますが、HTTPでも動作するはずです
const SERVER_URL = "http://136.111.186.176:5000/api/user/update-score";

// 総問題数（26問）
const TOTAL_QUESTIONS = 26;

// カテゴリ定義（10カテゴリ）
const CATEGORIES = {
  geometry: { name: "ジオメトリ構成力", questions: [1, 2, 3, 4] },
  data_structure: { name: "データ構造力", questions: [5, 6, 7, 8] },
  attributes: { name: "属性・情報付加力", questions: [9, 10] },
  parametric: { name: "パラメトリック操作力", questions: [11, 12] },
  programming: { name: "プログラミング力", questions: [13, 14] },
  manufacturing: { name: "出力・製造適応力", questions: [15, 16] },
  visualization: { name: "ビジュアライゼーション力", questions: [17, 18] },
  interaction: { name: "インタラクション・体験設計力", questions: [19, 20] },
  management: { name: "モデル管理・整理力", questions: [21, 22, 23, 24] },
  simulation: { name: "シミュレーション力", questions: [25, 26] }
};

/**
 * フォーム送信時のトリガー関数
 * Googleフォームで「送信時」トリガーを設定してください
 *
 * スコアはGoogleフォームの自動採点またはGoogleSheetから取得します
 */
function onScreeningTestSubmit(e) {
  try {
    // スコアが取得できない場合はGoogleSheetから読み取る
    if (!e || !e.response) {
      Logger.log("Event object is invalid. Falling back to sendScoreFromSheet()");
      sendScoreFromSheet();
      return;
    }

    const formResponse = e.response;

    // スコアが取得可能かチェック
    let score;
    try {
      score = formResponse.getScore();
      if (score === null || score === undefined) {
        Logger.log("Score not available from form response. Falling back to sendScoreFromSheet()");
        sendScoreFromSheet();
        return;
      }
    } catch (err) {
      Logger.log("Cannot get score from form response. Falling back to sendScoreFromSheet()");
      sendScoreFromSheet();
      return;
    }

    const itemResponses = formResponse.getItemResponses();

    // メールアドレスと各問題の正誤を取得
    let userEmail = "";
    let questionScores = {}; // Q1-Q26の正誤（0 or 1）

    for (let i = 0; i < itemResponses.length; i++) {
      const itemResponse = itemResponses[i];
      const question = itemResponse.getItem().getTitle();
      const answer = itemResponse.getResponse();

      // メールアドレスの取得
      if (question.includes("メール") || question.includes("Email") || question.includes("email")) {
        userEmail = answer;
        continue;
      }

      // 問題番号を抽出（例: "Q1: ..." → 1）
      const questionMatch = question.match(/Q(\d+)/);
      if (questionMatch) {
        const questionNum = parseInt(questionMatch[1]);
        const itemScore = itemResponse.getScore(); // この問題の得点（正解=1, 不正解=0）
        questionScores[questionNum] = itemScore;
      }
    }

    // メールアドレスが必須
    if (!userEmail) {
      Logger.log("Error: Email address not found");
      return;
    }

    // カテゴリ別スコアを計算
    const categoryScores = {};
    for (const [categoryKey, categoryInfo] of Object.entries(CATEGORIES)) {
      let categoryCorrect = 0;
      let categoryTotal = categoryInfo.questions.length;

      for (const qNum of categoryInfo.questions) {
        if (questionScores[qNum] && questionScores[qNum] > 0) {
          categoryCorrect++;
        }
      }

      categoryScores[categoryKey] = {
        name: categoryInfo.name,
        correct: categoryCorrect,
        total: categoryTotal,
        percentage: (categoryCorrect / categoryTotal) * 100
      };
    }

    // Googleフォームのスコアを100点満点に換算
    const technicalScore = (score / TOTAL_QUESTIONS) * 100;

    Logger.log(`=== スクリーニングテスト結果 ===`);
    Logger.log(`Email: ${userEmail}`);
    Logger.log(`総合: ${score} / ${TOTAL_QUESTIONS} (${technicalScore.toFixed(2)}%)`);
    Logger.log(`\nカテゴリ別得点:`);
    for (const [key, data] of Object.entries(categoryScores)) {
      Logger.log(`  ${data.name}: ${data.correct}/${data.total} (${data.percentage.toFixed(1)}%)`);
    }

    // GCPサーバーにPOST
    const payload = {
      email: userEmail,
      technical_score: technicalScore,
      category_scores: categoryScores,
      question_scores: questionScores
    };

    const options = {
      "method": "post",
      "contentType": "application/json",
      "payload": JSON.stringify(payload),
      "muteHttpExceptions": true
    };

    const response = UrlFetchApp.fetch(SERVER_URL, options);
    const responseCode = response.getResponseCode();

    if (responseCode === 200) {
      const responseData = JSON.parse(response.getContentText());
      Logger.log("\n✓ Success: Score updated");
      Logger.log(`  User: ${responseData.username}`);
      Logger.log(`  User Level: L${responseData.user_level}`);
    } else {
      Logger.log("\n✗ Error: Server returned " + responseCode);
      Logger.log(response.getContentText());
    }

  } catch (error) {
    Logger.log("Error in onScreeningTestSubmit: " + error.toString());
  }
}

/**
 * 手動テスト用関数
 * スクリプトエディタで直接実行して動作確認できます
 */
function testUpdateScore() {
  const testData = {
    email: "test@example.com",
    technical_score: 76.92  // 20/26問正解の例
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

/**
 * 最新の回答を確認する関数（デバッグ用）
 */
function checkLatestResponse() {
  const form = FormApp.getActiveForm();
  const responses = form.getResponses();

  if (responses.length === 0) {
    Logger.log("No responses found");
    return;
  }

  // 最新の回答を取得
  const latestResponse = responses[responses.length - 1];
  const score = latestResponse.getScore();
  const itemResponses = latestResponse.getItemResponses();

  let userEmail = "";

  for (let i = 0; i < itemResponses.length; i++) {
    const itemResponse = itemResponses[i];
    const question = itemResponse.getItem().getTitle();
    const answer = itemResponse.getResponse();

    if (question.includes("メール") || question.includes("Email")) {
      userEmail = answer;
    }
  }

  const technicalScore = (score / TOTAL_QUESTIONS) * 100;

  Logger.log("=== 最新の回答 ===");
  Logger.log(`Email: ${userEmail}`);
  Logger.log(`正解数: ${score} / ${TOTAL_QUESTIONS}`);
  Logger.log(`Technical Score: ${technicalScore.toFixed(2)}`);
}

/**
 * 既存の回答を処理してサーバーに送信する関数
 * 既に回答済みのテストを手動でサーバーに送信する場合に使用
 */
function processExistingResponse() {
  const form = FormApp.getActiveForm();
  const responses = form.getResponses();

  if (responses.length === 0) {
    Logger.log("No responses found");
    return;
  }

  // 最新の回答を取得
  const formResponse = responses[responses.length - 1];

  // スコアが取得できない場合（採点前の回答）は処理をスキップ
  let score;
  try {
    score = formResponse.getScore();
    if (score === null || score === undefined) {
      Logger.log("Error: Score not available yet. Please set up quiz mode and answers first.");
      Logger.log("Then submit a new response after setting up the quiz.");
      return;
    }
  } catch (e) {
    Logger.log("Error: Cannot get score. Quiz mode may not be properly configured.");
    Logger.log("Please ensure:");
    Logger.log("1. Form is set to Quiz mode (Settings > Quizzes > Make this a quiz)");
    Logger.log("2. Each question has correct answers set up");
    Logger.log("3. Submit a NEW response after setting up the quiz");
    return;
  }

  const itemResponses = formResponse.getItemResponses();

  // メールアドレスと各問題の正誤を取得
  let userEmail = "";
  let questionScores = {};

  for (let i = 0; i < itemResponses.length; i++) {
    const itemResponse = itemResponses[i];
    const question = itemResponse.getItem().getTitle();
    const answer = itemResponse.getResponse();

    // メールアドレスの取得
    if (question.includes("メール") || question.includes("Email") || question.includes("email")) {
      userEmail = answer;
      continue;
    }

    // 問題番号を抽出
    const questionMatch = question.match(/Q(\d+)/);
    if (questionMatch) {
      const questionNum = parseInt(questionMatch[1]);
      const itemScore = itemResponse.getScore();
      questionScores[questionNum] = itemScore;
    }
  }

  if (!userEmail) {
    Logger.log("Error: Email address not found");
    return;
  }

  // カテゴリ別スコアを計算
  const categoryScores = {};
  for (const [categoryKey, categoryInfo] of Object.entries(CATEGORIES)) {
    let categoryCorrect = 0;
    let categoryTotal = categoryInfo.questions.length;

    for (const qNum of categoryInfo.questions) {
      if (questionScores[qNum] && questionScores[qNum] > 0) {
        categoryCorrect++;
      }
    }

    categoryScores[categoryKey] = {
      name: categoryInfo.name,
      correct: categoryCorrect,
      total: categoryTotal,
      percentage: (categoryCorrect / categoryTotal) * 100
    };
  }

  const technicalScore = (score / TOTAL_QUESTIONS) * 100;

  Logger.log(`=== スクリーニングテスト結果 ===`);
  Logger.log(`Email: ${userEmail}`);
  Logger.log(`総合: ${score} / ${TOTAL_QUESTIONS} (${technicalScore.toFixed(2)}%)`);
  Logger.log(`\nカテゴリ別得点:`);
  for (const [key, data] of Object.entries(categoryScores)) {
    Logger.log(`  ${data.name}: ${data.correct}/${data.total} (${data.percentage.toFixed(1)}%)`);
  }

  // GCPサーバーにPOST
  const payload = {
    email: userEmail,
    technical_score: technicalScore,
    category_scores: categoryScores,
    question_scores: questionScores
  };

  const options = {
    "method": "post",
    "contentType": "application/json",
    "payload": JSON.stringify(payload),
    "muteHttpExceptions": true
  };

  const response = UrlFetchApp.fetch(SERVER_URL, options);
  const responseCode = response.getResponseCode();

  if (responseCode === 200) {
    const responseData = JSON.parse(response.getContentText());
    Logger.log("\n✓ Success: Score updated");
    Logger.log(`  User: ${responseData.username}`);
    Logger.log(`  User Level: L${responseData.user_level}`);
    Logger.log(`  CAD Experience Score: ${responseData.cad_experience_score}`);
  } else {
    Logger.log("\n✗ Error: Server returned " + responseCode);
    Logger.log(response.getContentText());
  }
}

/**
 * GoogleSheetからスコアを読み取ってサーバーに送信する関数
 * スクリーニングテストの結果がスプレッドシートに記録されている場合に使用
 */
function sendScoreFromSheet() {
  try {
    // フォームに紐づいたスプレッドシートを取得
    const form = FormApp.getActiveForm();
    const sheet = SpreadsheetApp.openById(form.getDestinationId()).getSheets()[0];

    // ヘッダー行を取得
    const headers = sheet.getRange(1, 1, 1, sheet.getLastColumn()).getValues()[0];

    // 必要な列のインデックスを探す
    let emailCol = -1;
    let scoreCol = -1;

    for (let i = 0; i < headers.length; i++) {
      const header = String(headers[i]).toLowerCase();
      if (header.includes('メール') || header.includes('email')) {
        emailCol = i;
      }
      if (header.includes('スコア') || header.includes('score') || header.includes('点数')) {
        scoreCol = i;
      }
    }

    if (emailCol === -1 || scoreCol === -1) {
      Logger.log("Error: Could not find email or score column");
      Logger.log(`Email column index: ${emailCol}, Score column index: ${scoreCol}`);
      Logger.log("Headers: " + headers.join(", "));
      return;
    }

    Logger.log(`Found: Email column=${emailCol+1}, Score column=${scoreCol+1}`);

    // 最新の行（最後の回答）を取得
    const lastRow = sheet.getLastRow();
    if (lastRow <= 1) {
      Logger.log("No data rows found");
      return;
    }

    const rowData = sheet.getRange(lastRow, 1, 1, sheet.getLastColumn()).getValues()[0];
    const userEmail = rowData[emailCol];
    const rawScore = rowData[scoreCol];

    // スコアを数値に変換（26点満点を100点満点に換算）
    let technicalScore;
    if (typeof rawScore === 'number') {
      // 既に数値の場合
      if (rawScore <= 26) {
        // 26点満点の場合は100点満点に変換
        technicalScore = (rawScore / TOTAL_QUESTIONS) * 100;
      } else {
        // 既に100点満点の場合
        technicalScore = rawScore;
      }
    } else {
      // 文字列の場合は数値を抽出
      const match = String(rawScore).match(/[\d.]+/);
      if (match) {
        const score = parseFloat(match[0]);
        technicalScore = score <= 26 ? (score / TOTAL_QUESTIONS) * 100 : score;
      } else {
        Logger.log("Error: Could not parse score: " + rawScore);
        return;
      }
    }

    Logger.log(`\n=== GoogleSheetから取得 ===`);
    Logger.log(`Email: ${userEmail}`);
    Logger.log(`Raw Score: ${rawScore}`);
    Logger.log(`Technical Score: ${technicalScore.toFixed(2)}`);

    if (!userEmail) {
      Logger.log("Error: Email is empty");
      return;
    }

    // サーバーに送信
    const payload = {
      email: userEmail,
      technical_score: technicalScore
    };

    const options = {
      "method": "post",
      "contentType": "application/json",
      "payload": JSON.stringify(payload),
      "muteHttpExceptions": true
    };

    const response = UrlFetchApp.fetch(SERVER_URL, options);
    const responseCode = response.getResponseCode();

    if (responseCode === 200) {
      const responseData = JSON.parse(response.getContentText());
      Logger.log("\n✓ Success: Score updated");
      Logger.log(`  User: ${responseData.username}`);
      Logger.log(`  User Level: L${responseData.user_level}`);
      Logger.log(`  CAD Experience Score: ${responseData.cad_experience_score}`);
    } else {
      Logger.log("\n✗ Error: Server returned " + responseCode);
      Logger.log(response.getContentText());
    }

  } catch (error) {
    Logger.log("Error in sendScoreFromSheet: " + error.toString());
  }
}

/**
 * 手動でスコアを入力してサーバーに送信する関数
 * クイズモード設定前の回答用
 *
 * 使い方：この関数内のメールアドレスとスコアを編集して実行
 */
function manualSendScore() {
  // ===== ここを編集 =====
  const userEmail = "yishizu@geometryengineeringlab.tech";  // メールアドレス
  const technicalScore = 80.77;  // スコア（0-100）
  // ====================

  Logger.log(`手動スコア送信: ${userEmail} = ${technicalScore}点`);

  const payload = {
    email: userEmail,
    technical_score: technicalScore
  };

  const options = {
    "method": "post",
    "contentType": "application/json",
    "payload": JSON.stringify(payload),
    "muteHttpExceptions": true
  };

  const response = UrlFetchApp.fetch(SERVER_URL, options);
  const responseCode = response.getResponseCode();

  if (responseCode === 200) {
    const responseData = JSON.parse(response.getContentText());
    Logger.log("\n✓ Success: Score updated");
    Logger.log(`  User: ${responseData.username}`);
    Logger.log(`  User Level: L${responseData.user_level}`);
    Logger.log(`  CAD Experience Score: ${responseData.cad_experience_score}`);
  } else {
    Logger.log("\n✗ Error: Server returned " + responseCode);
    Logger.log(response.getContentText());
  }
}

/**
 * フォームの設定とクイズモードを確認する関数
 */
function debugFormSettings() {
  const form = FormApp.getActiveForm();
  const responses = form.getResponses();

  Logger.log("=== フォーム情報 ===");
  Logger.log(`フォームタイトル: ${form.getTitle()}`);
  Logger.log(`回答数: ${responses.length}`);
  Logger.log(`クイズモード: ${form.isQuiz()}`);

  if (responses.length > 0) {
    const latest = responses[responses.length - 1];
    Logger.log(`\n最新回答の情報:`);
    Logger.log(`  getScore関数の型: ${typeof latest.getScore}`);
    Logger.log(`  タイムスタンプ: ${latest.getTimestamp()}`);

    try {
      const score = latest.getScore();
      Logger.log(`  スコア: ${score}`);
    } catch (e) {
      Logger.log(`  スコア取得エラー: ${e.message}`);
      Logger.log(`  → フォームがクイズモードになっていない、または採点が設定されていません`);
    }

    const items = latest.getItemResponses();
    Logger.log(`\n回答項目数: ${items.length}`);
    Logger.log(`最初の3項目:`);
    for (let i = 0; i < Math.min(3, items.length); i++) {
      const item = items[i];
      Logger.log(`  ${i+1}. ${item.getItem().getTitle()}`);
      Logger.log(`     回答: ${item.getResponse()}`);
      Logger.log(`     getScore関数の型: ${typeof item.getScore}`);
    }
  } else {
    Logger.log("\n回答がまだありません");
  }
}
