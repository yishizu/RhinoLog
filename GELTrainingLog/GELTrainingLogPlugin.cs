using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
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
        private class TrainingPeriod
        {
            public string user_name { get; set; }
            public string start_date { get; set; }
            public string end_date { get; set; }
        }
        
        private DateTime? _periodStart = new DateTime(2025, 9, 8);
        private DateTime? _periodEnd = new DateTime(2025, 9, 12);

        private static string _userID;
        private static  string _logFolder;
        private static string _sessionLogFile;
        private static string _sessionMetaFile;
        private Plane _lastCPlane = Plane.WorldXY;
        private string _lastViewName = "";
        public override PlugInLoadTime LoadTime => PlugInLoadTime.AtStartup;
        
        private readonly object _logLock = new();
        private readonly Queue<string> _logQueue = new();
        
        private bool _isOpening = false;
        private bool _firstViewInitialized = false;
        private bool _logWriterRunning = true;
        private readonly List<string> _openedDocumentNames = new();
        private DateTime _sessionStartTime;
        private DateTime _sessionEndTime;
        public GELTrainingLogPlugin()
        {
            Instance = this;
        }


        public static GELTrainingLogPlugin Instance { get; private set; }

        public void Log(string action, string detail)
        {
            
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
            _sessionStartTime = DateTime.Now;
            _userID = Environment.UserName;
            _logFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "GEL", "RH", _userID);
            
            if (!Directory.Exists(_logFolder))
            {
                Directory.CreateDirectory(_logFolder);
            }

            LoadTrainingPeriod();
            
            RhinoApp.WriteLine("GEL Rhino Operation Logger Loaded");

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var sessionFolder = Path.Combine(_logFolder,  timestamp);
            Directory.CreateDirectory(sessionFolder);
            

            _sessionLogFile = Path.Combine(sessionFolder, $"{_userID}_Log.csv");
            _sessionMetaFile = Path.Combine(sessionFolder, $"{_userID}_Meta.json");

            File.WriteAllText(_sessionLogFile, "Timestamp,UserID,Action,Detail\n", Encoding.UTF8);

            var meta = new
            {
                UserID = _userID,
                SessionStart = _sessionStartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                SessionEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                OpenedDocumentNames = _openedDocumentNames
            };
            File.WriteAllText(_sessionMetaFile, JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }));
            Log("Session Started", "Rhino logging plugin initialized");
            RhinoDoc.AddRhinoObject += OnAddObject;
            RhinoDoc.DeleteRhinoObject += OnDeleteObject;
            Command.BeginCommand += OnCommandBegin;
            Command.EndCommand += OnCommandEnd;
            RhinoDoc.CloseDocument += OnCloseDocument;
            RhinoDoc.BeginOpenDocument += OnBeginOpenDocument;
            RhinoApp.Idle += OnIdle;

            // Start the log writer in a separate thread    
            StartLogWriter();
            return LoadReturnCode.Success;
        }
        
        
        private void LoadTrainingPeriod()
        {
            RhinoApp.WriteLine("Loading training period...");
            try
            {
                string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "GEL", "training_period.json");

                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var period = JsonSerializer.Deserialize<TrainingPeriod>(json);
                    if (period != null )
                    {
                        if (DateTime.TryParse(period.start_date, out var start) &&
                            DateTime.TryParse(period.end_date, out var end))
                        {
                            _periodStart = start;
                            _periodEnd = end;
                            RhinoApp.WriteLine($"Training period loaded: {_periodStart:yyyy-MM-dd} to {_periodEnd:yyyy-MM-dd}");
                        }
                        else
                        {
                            RhinoApp.WriteLine("⚠ Invalid date format in training_period.json");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine("⚠ Training period could not be loaded: " + ex.Message);
            }
        }

        protected override void OnShutdown()
        {
            Log("Session Ended", "Rhino is shutting down");
            _logWriterRunning = false;
            while (_logQueue.Count > 0)
            {
                Thread.Sleep(100); // 少しずつ待つ
            }
            var sessionMeta = new
            {
                UserID = _userID,
                SessionStart = _sessionStartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                SessionEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                OpenedDocumentNames = _openedDocumentNames
            };

            File.WriteAllText(_sessionMetaFile, JsonSerializer.Serialize(sessionMeta, new JsonSerializerOptions { WriteIndented = true }));

            RhinoDoc.AddRhinoObject -= OnAddObject;
            RhinoDoc.DeleteRhinoObject -= OnDeleteObject;
            Command.BeginCommand -= OnCommandBegin;
            Command.EndCommand -= OnCommandEnd;
            RhinoDoc.CloseDocument -= OnCloseDocument;
            RhinoDoc.BeginOpenDocument -= OnBeginOpenDocument;
            RhinoApp.Idle -= OnIdle;
        }

        
        private void OnAddObject(object sender, RhinoObjectEventArgs e)
        {
            if(_isOpening) return;
            if (e == null || e.TheObject == null) return;
            
            var obj = e.TheObject;
            var doc = RhinoDoc.ActiveDoc;
            string layerName = "(unknown)";
            if (doc != null && obj.Attributes.LayerIndex >= 0 && obj.Attributes.LayerIndex < doc.Layers.Count)
            {
                layerName = doc.Layers[obj.Attributes.LayerIndex].Name;
            }

            Log("Object Added", $"ID:{obj.Id}, Type:{obj.ObjectType}, Layer:{layerName} (Index:{obj.Attributes.LayerIndex})");
        }

        private void OnDeleteObject(object sender, RhinoObjectEventArgs e)
        {
            var obj = e.TheObject;
            Log("Object Deleted", $"ID:{obj.Id}, Type:{obj.ObjectType}");
        }

        private void OnCommandBegin(object sender, CommandEventArgs e)
        {
            Log("Command Started", e.CommandEnglishName);
        }

        private void OnCommandEnd(object sender, CommandEventArgs e)
        {
            var doc = RhinoDoc.ActiveDoc;

            if (e.CommandEnglishName.Equals("NamedView", StringComparison.OrdinalIgnoreCase))
            {
                if (doc != null && doc.NamedViews.Count > 0)
                {
                    foreach (var namedView in doc.NamedViews)
                    {
                        Log("NamedView Saved", namedView.Name);
                    }
                }
                else
                {
                    Log("NamedView Accessed", "No named views found");
                }
            }

            if (e.CommandEnglishName.Equals("Worksession", StringComparison.OrdinalIgnoreCase))
            {
                var linkedFiles = doc?.Worksession?.ModelPaths;
                if (linkedFiles != null && linkedFiles.Length > 0)
                {
                    foreach (var file in linkedFiles)
                    {
                        Log("Linked Model", file);
                    }
                }
                else
                {
                    Log("Worksession Accessed", "No linked models found");
                }
            }

            if (e.CommandEnglishName.Equals("Save", StringComparison.OrdinalIgnoreCase) ||
                e.CommandEnglishName.Equals("SaveAs", StringComparison.OrdinalIgnoreCase))
            {
                if (doc != null && !string.IsNullOrEmpty(doc.Path))
                {
                    //SetLogFileFromDocName(doc);
                    var newPath = doc.Path;
                    
                    _openedDocumentNames.RemoveAll(name =>
                        string.Equals(Path.GetFileNameWithoutExtension(name), Path.GetFileNameWithoutExtension(newPath), StringComparison.OrdinalIgnoreCase) == false
                        && Path.GetExtension(name) == ".3dm");

                    if (!_openedDocumentNames.Contains(newPath))
                    {
                        _openedDocumentNames.Add(newPath);
                    }

                    var fileInfo = new FileInfo(doc.Path);
                    var created = fileInfo.CreationTime;
                    var modified = fileInfo.LastWriteTime;
                    var sizeMB = fileInfo.Length / (1024.0 * 1024.0);
                    var objCount = doc.Objects.Count;
                    Log("File Saved", $"Path:{doc.Path}, Created:{created:yyyy-MM-dd HH:mm:ss}, Modified:{modified:yyyy-MM-dd HH:mm:ss}, Objects:{objCount}, FileSizeMB:{sizeMB:F2}");
                    SaveMetaAtSave(doc);
                }
                else
                {
                    Log("File Saved", "(No document path available)");
                }
            }
        }

        private void OnCloseDocument(object sender, DocumentEventArgs e)
        {
            _isOpening = false;
            Log("Document Closed", e.Document.Name);
        }

        private void OnBeginOpenDocument(object sender, DocumentOpenEventArgs e)
        {
            _isOpening = true;
            if (string.IsNullOrEmpty(e.FileName))
            {
                Log("New Document", "User created a new blank document");
                return;
            }
            if (!string.IsNullOrEmpty(e.FileName))
            {
                if (!_openedDocumentNames.Contains(Path.GetFileName(e.FileName))) // 重複防止（必要なら）
                {
                    _openedDocumentNames.Add(e.FileName);
                }
            }
            var fileInfo = new FileInfo(e.FileName);
            var created = fileInfo.CreationTime;
            var modified = fileInfo.LastWriteTime;
            var fileSizeMB = fileInfo.Length / (1024.0 * 1024.0);

            Log("Document Opened", $"Path:{e.FileName}, Created:{created:yyyy-MM-dd HH:mm:ss}, Modified:{modified:yyyy-MM-dd HH:mm:ss}, FileSizeMB:{fileSizeMB:F2}");

            var doc = RhinoDoc.ActiveDoc;
            //SetLogFileFromDocName(doc);
            int objectCount = 0, layerCount = 0, blockCount = 0;
            if (doc != null)
            {
                objectCount = doc.Objects.Count;
                layerCount = doc.Layers.Count;
                blockCount = doc.InstanceDefinitions.Count;
                Log("Document Stats", $"Objects:{objectCount}, Layers:{layerCount}, Blocks:{blockCount}");
                SaveMetaAtSave(doc);
            }
        }

        private void OnIdle(object sender, EventArgs e)
        {
            if (_isOpening)
            {
                _isOpening = false;
                RhinoApp.WriteLine("Document opened. Starting logging...");
            }
            
            var view = RhinoDoc.ActiveDoc?.Views.ActiveView;
            if (view != null)
            {
                if (!_firstViewInitialized)
                {
                    _lastViewName = view.ActiveViewport.Name;
                    _firstViewInitialized = true;
                    return;
                }
                if (view.ActiveViewport == null) return;
                string currentViewName = view.ActiveViewport.Name;
                if (_lastViewName != currentViewName)
                {
                    Log("View Changed", currentViewName);
                    _lastViewName = currentViewName;
                }

                var currentCPlane = view.ActiveViewport.ConstructionPlane();
                if (!_lastCPlane.Equals(currentCPlane))
                {
                    Log("CPlane Changed",
                        $"Origin: {currentCPlane.Origin}, X: {currentCPlane.XAxis}, Y: {currentCPlane.YAxis}");
                    _lastCPlane = currentCPlane;
                }
            }
        }

        private void SaveMetaAtSave(RhinoDoc doc)
        {
            if (doc == null || string.IsNullOrEmpty(doc.Path)) return;
            
            var meta = new
            {
                UserID = _userID,
                SessionStart = _sessionStartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                SessionEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                OpenedDocumentNames = _openedDocumentNames
            };
            
            File.WriteAllText(_sessionMetaFile, JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }));
        }
  
        private void StartLogWriter()
        {
            _logWriterRunning = true;
            Task.Run((() =>
            {
                while (_logWriterRunning || _logQueue.Count > 0)
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
                            File.AppendAllText(_sessionLogFile, logLine, System.Text.Encoding.UTF8);
                        }
                        catch (Exception e)
                        {
                            RhinoApp.WriteLine("⚠ ログ書き込み中にエラー: " + e.Message);
                            throw;
                        }
                    }
                    else
                    {
                        Thread.Sleep(100); // Sleep for a short time to avoid busy waiting
                    }
                    
                }
            }));
        }
    }
}