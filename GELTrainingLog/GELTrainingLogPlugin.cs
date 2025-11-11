using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using Eto.Threading;
using Rhino;
using Rhino.Commands;
using Rhino.PlugIns;
using Rhino.DocObjects;
using Rhino.Geometry;
using Environment = System.Environment;
using Thread = System.Threading.Thread;


namespace GELTrainingLog
{
    
    ///<summary>
    /// <para>Every RhinoCommon .rhp assembly must have one and only one PlugIn-derived
    /// class. DO NOT create instances of this class yourself. It is the
    /// responsibility of Rhino to create an instance of this class.</para>
    /// <para>To complete plug-in information, please also see all PlugInDescription
    /// attributes in AssemblyInfo.cs (you might need to click "Project" ->
    /// "Show All Files" to see it in the "Solution Explorer" window).</para>
    ///</summary>
    ///
    ///
    ///
    

    
    public class GELTrainingLogPlugin : PlugIn
    {
        private class UserInfo
        {
            public string username { get; set; }
            public string full_name { get; set; }
            public string email { get; set; }
            public string organization { get; set; }
            public string start_date { get; set; }
            public string end_date { get; set; }
            public bool registered { get; set; }
        }

        private DateTime? _periodStart;
        private DateTime? _periodEnd;
        private bool _isRegisteredUser = false;

        private static string _userID;
        private static  string _logFolder;
        private static string _sessionLogFile;
        private const string SERVER_URL = "http://136.111.186.176:5000";
        private static readonly HttpClient _httpClient = new HttpClient();

        public override PlugInLoadTime LoadTime => PlugInLoadTime.AtStartup;

        private readonly object _logLock = new();
        private readonly Queue<string> _logQueue = new();
        
        public GELTrainingLogPlugin()
        {
            Instance = this;
        }


        public static GELTrainingLogPlugin Instance { get; private set; }

        public void Log(string action, string detail)
        {
            // 未登録ユーザーはログを記録しない
            if (!_isRegisteredUser)
                return;

            var now = DateTime.Now;

            // 期間が設定されている場合だけチェック
            if (_periodStart.HasValue && _periodEnd.HasValue)
            {
                if (now < _periodStart.Value || now > _periodEnd.Value)
                    return; // ログを記録しない
            }

            var logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{_userID},{action},\"{detail}\"\n";
            lock (_logLock)
            {
                _logQueue.Enqueue(logLine);
            }
        }

        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            // ユーザーIDをローカル設定ファイルから読み込み、なければダイアログで入力
            _userID = LoadOrPromptUserID();

            if (string.IsNullOrWhiteSpace(_userID))
            {
                RhinoApp.WriteLine("⚠ User ID was not provided. Logging disabled.");
                return LoadReturnCode.Success;
            }

            // ここで明示的に RH フォルダを入れる
            _logFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "GEL", "RH", _userID);

            if (!Directory.Exists(_logFolder))
            {
                Directory.CreateDirectory(_logFolder);
            }

            LoadUserInfoFromServer();

            RhinoApp.WriteLine("GEL Rhino Operation Logger Loaded");

            string documentName = "Untitled";
            var doc = RhinoDoc.ActiveDoc;
            if (doc != null && !string.IsNullOrEmpty(doc.Name))
            {
                documentName = Path.GetFileNameWithoutExtension(doc.Name);
            }

            _sessionLogFile = Path.Combine(_logFolder, $"{_userID}_{documentName}_Log.csv");

            // ファイルが存在しない場合のみヘッダーを書き込む
            if (!File.Exists(_sessionLogFile))
            {
                File.WriteAllText(_sessionLogFile, "Timestamp,UserID,Action,Detail\n");
            }

            Command.BeginCommand += OnCommandBegin;
            RhinoDoc.CloseDocument += OnCloseDocument;
            RhinoDoc.BeginOpenDocument += OnBeginOpenDocument;

            // レイヤー操作の記録
            RhinoDoc.LayerTableEvent += OnLayerTableEvent;

            // グループ操作の記録
            RhinoDoc.GroupTableEvent += OnGroupTableEvent;

            // Start the log writer in a separate thread    
            StartLogWriter();
            return LoadReturnCode.Success;
        }

        private string LoadOrPromptUserID()
        {
            // 設定ファイルのパス
            string configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GEL", "user_config.txt");

            // 既存の設定があれば読み込み
            if (File.Exists(configPath))
            {
                try
                {
                    string savedUserID = File.ReadAllText(configPath).Trim();
                    if (!string.IsNullOrWhiteSpace(savedUserID))
                    {
                        RhinoApp.WriteLine($"User ID loaded: {savedUserID}");
                        return savedUserID;
                    }
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine("⚠ Failed to load user config: " + ex.Message);
                }
            }

            // 未登録の場合は案内メッセージ
            RhinoApp.WriteLine("========================================");
            RhinoApp.WriteLine("⚠ GEL Training Log - ユーザー未登録");
            RhinoApp.WriteLine("========================================");
            RhinoApp.WriteLine("Googleフォームから送られた「.geluser」ファイルを使用して登録してください。");
            RhinoApp.WriteLine("");
            RhinoApp.WriteLine("【登録手順】");
            RhinoApp.WriteLine("1. Rhinoコマンドラインで「GELUserLogin」と入力");
            RhinoApp.WriteLine("2. メールで送られた「.geluser」ファイルを選択");
            RhinoApp.WriteLine("3. Rhinoを再起動");
            RhinoApp.WriteLine("========================================");

            return null;
        }

        private void LoadUserInfoFromServer()
        {
            RhinoApp.WriteLine("Checking user registration...");
            try
            {
                var response = _httpClient.GetAsync($"{SERVER_URL}/api/user/{_userID}").Result;

                if (response.IsSuccessStatusCode)
                {
                    var json = response.Content.ReadAsStringAsync().Result;
                    var userInfo = JsonSerializer.Deserialize<UserInfo>(json);

                    if (userInfo != null && userInfo.registered)
                    {
                        _isRegisteredUser = true;

                        if (DateTime.TryParse(userInfo.start_date, out var start) &&
                            DateTime.TryParse(userInfo.end_date, out var end))
                        {
                            _periodStart = start;
                            _periodEnd = end;
                            RhinoApp.WriteLine($"✓ User registered: {userInfo.full_name}");
                            RhinoApp.WriteLine($"✓ Training period: {_periodStart:yyyy-MM-dd} to {_periodEnd:yyyy-MM-dd}");
                        }
                        else
                        {
                            RhinoApp.WriteLine("⚠ Invalid date format from server");
                            _isRegisteredUser = false;
                        }
                    }
                }
                else
                {
                    RhinoApp.WriteLine("⚠ User not registered. Please register via Google Form first.");
                    RhinoApp.WriteLine($"⚠ Username: {_userID}");
                    _isRegisteredUser = false;
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine("⚠ Could not connect to server: " + ex.Message);
                RhinoApp.WriteLine("⚠ Logging disabled. Please check your internet connection.");
                _isRegisteredUser = false;
            }
        }

        protected override void OnShutdown()
        {
            Command.BeginCommand -= OnCommandBegin;
            RhinoDoc.CloseDocument -= OnCloseDocument;
            RhinoDoc.BeginOpenDocument -= OnBeginOpenDocument;

            RhinoDoc.LayerTableEvent -= OnLayerTableEvent;
            RhinoDoc.GroupTableEvent -= OnGroupTableEvent;
        }

        private void OnCommandBegin(object sender, CommandEventArgs e)
        {
            Log("Command", e.CommandEnglishName);
        }

        private void OnCloseDocument(object sender, DocumentEventArgs e)
        {
            Log("Document Closed", e.Document.Name);
        }

        private void OnBeginOpenDocument(object sender, DocumentOpenEventArgs e)
        {
            Log("Document Opened", e.FileName);

            var doc = RhinoDoc.ActiveDoc;
            SetLogFileFromDocName(doc);
        }

        private void OnLayerTableEvent(object sender, Rhino.DocObjects.Tables.LayerTableEventArgs e)
        {
            if (e.EventType == Rhino.DocObjects.Tables.LayerTableEventType.Added)
            {
                var layer = e.NewState;
                Log("Layer Created", layer.Name);
            }
            else if (e.EventType == Rhino.DocObjects.Tables.LayerTableEventType.Deleted)
            {
                var layer = e.OldState;
                Log("Layer Deleted", layer.Name);
            }
            else if (e.EventType == Rhino.DocObjects.Tables.LayerTableEventType.Modified)
            {
                var layer = e.NewState;
                Log("Layer Modified", layer.Name);
            }
        }

        private void OnGroupTableEvent(object sender, Rhino.DocObjects.Tables.GroupTableEventArgs e)
        {
            if (e.EventType == Rhino.DocObjects.Tables.GroupTableEventType.Added)
            {
                var group = e.NewState;
                Log("Group Created", group.Name);
            }
            else if (e.EventType == Rhino.DocObjects.Tables.GroupTableEventType.Deleted)
            {
                var group = e.OldState;
                Log("Group Deleted", group.Name);
            }
            else if (e.EventType == Rhino.DocObjects.Tables.GroupTableEventType.Modified)
            {
                var group = e.NewState;
                Log("Group Modified", group.Name);
            }
        }

        private void SetLogFileFromDocName(RhinoDoc doc)
        {
            if (doc == null) return;

            string docName = string.IsNullOrEmpty(doc.Name) ? "Untitled" : Path.GetFileNameWithoutExtension(doc.Name);
            _sessionLogFile = Path.Combine(_logFolder, $"{_userID}_{docName}_Log.csv");

            // ファイルが存在しない場合のみヘッダーを書き込む
            if (!File.Exists(_sessionLogFile))
            {
                File.WriteAllText(_sessionLogFile, "Timestamp,UserID,Action,Detail\n");
            }
        }

        private async Task SendLogToServerAsync(string timestamp, string action, string detail)
        {
            if (!_isRegisteredUser)
                return;

            try
            {
                var doc = RhinoDoc.ActiveDoc;
                string docName = "Untitled";
                if (doc != null && !string.IsNullOrEmpty(doc.Name))
                {
                    docName = Path.GetFileNameWithoutExtension(doc.Name);
                }

                var logData = new
                {
                    Timestamp = timestamp,
                    UserID = _userID,
                    Action = action,
                    Detail = detail,
                    DocumentName = docName
                };

                var jsonContent = JsonSerializer.Serialize(logData);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{SERVER_URL}/api/log/upload", content);

                if (!response.IsSuccessStatusCode)
                {
                    RhinoApp.WriteLine($"⚠ Server log failed: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                // サーバーへの送信が失敗してもローカルログは残る
                RhinoApp.WriteLine($"⚠ Server connection error: {ex.Message}");
            }
        }

        private void StartLogWriter()
        {
            Task.Run((async () =>
            {
                while (true)
                {
                    string logLine = null;
                    lock (_logLock)
                    {
                        if (_logQueue.Count > 0)
                        {
                            logLine = _logQueue.Dequeue();
                        }
                    }

                    if(logLine != null)
                    {
                        try
                        {
                            // ローカルCSVに書き込み
                            File.AppendAllText(_sessionLogFile, logLine);

                            // CSVログから情報を抽出してサーバーに送信
                            var parts = logLine.Split(',');
                            if (parts.Length >= 3)
                            {
                                string timestamp = parts[0];
                                string userId = parts[1];
                                string action = parts[2];
                                string detail = parts.Length > 3 ? parts[3].Trim().Trim('"') : "";

                                // サーバーに非同期送信（awaitせずに fire-and-forget）
                                _ = SendLogToServerAsync(timestamp, action, detail);
                            }
                        }
                        catch (Exception e)
                        {
                            RhinoApp.WriteLine("⚠ ログ書き込み中にエラー: " + e.Message);
                        }
                    }
                    else
                    {
                        await Task.Delay(100); // Sleep for a short time to avoid busy waiting
                    }
                }
            }));
        }
    }
}