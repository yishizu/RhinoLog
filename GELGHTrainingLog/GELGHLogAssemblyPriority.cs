using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Rhino;

namespace GELGHTrainingLog
{
    public class GELGHLogAssemblyPriority : GH_AssemblyPriority
    {
        private string _sessionFolder;
        private string _userID = Environment.UserName;
        private string _logFilePath;
        private string _metaFilePath;
        private Dictionary<Guid, List<Guid>> _lastSources = new();
        private HashSet<Guid> _disabledBefore = new();
        private Dictionary<Guid, int> _sliderChangeCounts = new();
        
        private Guid? _lastDocumentGuid = null;
        private string _lastDisplayName = "";
        private bool _isSaveAsTransition = false;
        private List<Guid> _lastObjectIds = new();
        private bool isFirstLoad = true;
        private string _lastKnownPath = "";
        private DateTime _lastWriteTime = DateTime.MinValue;
        private bool _sessionInitialized = false;
        
        private Dictionary<Guid, double> _lastSliderValues = new();
        private Dictionary<Guid, double> _lastRecordedSliderValues = new();
        private Dictionary<Guid, bool> _lastToggleStates = new();
        private Dictionary<Guid, string> _lastPanelTexts = new();
        private Dictionary<Guid, string> _lastRecordedPanelTexts = new();
        private Dictionary<Guid, string> _lastValueListState = new();
        private Dictionary<Guid, string> _lastGraphData = new();
        private Dictionary<Guid, string> _lastMDSliderValue = new();
        private Dictionary<Guid, DateTime> _lastSliderChangeTime = new();
        private Dictionary<Guid, DateTime> _lastToggleChangeTime = new();
        private Dictionary<Guid, DateTime> _lastPanelChangeTime = new();
        private Dictionary<Guid, DateTime> _lastValueListChangeTime = new();
        private Dictionary<Guid, DateTime> _lastGraphChangeTime = new();
        private Dictionary<Guid, DateTime> _lastMDSliderChangeTime = new();

        private double delay = 100;
        
        private System.Windows.Forms.Timer _monitorTimer;
        
        public override GH_LoadingInstruction PriorityLoad()
        {
            if (isFirstLoad)
            {
                //InitializeSession();
                isFirstLoad = false;
                
            }
            Grasshopper.Instances.DocumentServer.DocumentAdded += OnDocumentAdded;
            Grasshopper.Instances.DocumentServer.DocumentRemoved += OnDocumentRemoved;
            
            Rhino.RhinoApp.Closing += OnRhinoClosing;
            
            return GH_LoadingInstruction.Proceed;
        }
        
        private void OnRhinoClosing(object sender, EventArgs e)
        {
            Log("SessionEnd", $"User: {_userID} (via RhinoApp.Closing)");
            CleanUpIfEmpty();
        }
        private void EnsureSessionInitialized()
        {
            if (_sessionInitialized) return;

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            _sessionFolder = Path.Combine(desktop, "GEL", "GH", _userID, timestamp);
            Directory.CreateDirectory(_sessionFolder);
            _logFilePath = Path.Combine(_sessionFolder, $"{_userID}_GHLog.csv");
            File.WriteAllText(_logFilePath, "Timestamp,Action,Detail\n");
            _sessionInitialized = true;

            // 初回ログとしてセッションスタート記録
            Log("SessionStart", $"User: {_userID}");
        }
        private void CleanUpIfEmpty()
        {
            try
            {
                if (File.Exists(_logFilePath))
                {
                    var lines = File.ReadAllLines(_logFilePath);
                    if (lines.Length <= 3)
                    {
                        File.Delete(_logFilePath);
                        if (File.Exists(_metaFilePath)) File.Delete(_metaFilePath);

                        var parentFolder = Path.GetDirectoryName(_logFilePath);
                        if (Directory.Exists(parentFolder) &&
                            Directory.GetFileSystemEntries(parentFolder).Length == 0)
                        {
                            Directory.Delete(parentFolder);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"ログ削除時のエラー: {ex.Message}");
            }
        }
        private void InitializeSession()
        {
            
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            _sessionFolder = Path.Combine(desktop, "GEL", "GH", _userID, timestamp);
            Directory.CreateDirectory(_sessionFolder);
            _logFilePath = Path.Combine(_sessionFolder, $"{_userID}_GHLog.csv");
            File.WriteAllText(_logFilePath, "Timestamp,Action,Detail\n");
            Log("SessionStart", $"User: {_userID}");
        }

        private void Log(string action, string detail)
        {
            EnsureSessionInitialized(); 
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{action},\"{detail}\"\n";
            File.AppendAllText(_logFilePath, line);
        }
        
        private void OnDocumentRemoved(GH_DocumentServer server, GH_Document doc)
        {
            // SaveAs判定用フラグ
            _isSaveAsTransition = true;
            SaveGhMeta(doc);
        }

        private void OnDocumentAdded(GH_DocumentServer server, GH_Document doc)
        {
            _lastKnownPath = doc.FilePath ?? "";
            if (File.Exists(_lastKnownPath))
                _lastWriteTime = File.GetLastWriteTime(_lastKnownPath);
            string newDisplayName = doc.DisplayName ?? "Untitled";

            // ① SaveAs判定ロジック
            if (_lastDocumentGuid.HasValue &&
                AreDocumentsEquivalent(_lastDisplayName, newDisplayName, doc))
            {
                if (_lastDisplayName != newDisplayName)
                {
                    Log("DocumentRenamed", newDisplayName);
                }
                _lastDocumentGuid = doc.DocumentID;
                _lastDisplayName = newDisplayName;
            }
            else
            {
                // 通常の新規ドキュメントとして処理
                if(doc.ObjectCount > 0)
                {
                    Log("DocumentOpened", newDisplayName);
                    foreach (var obj in doc.Objects)
                    {
                        TryRegisterChangeTracking(obj);
                    }
                }
                else
                {
                    Log("DocumentCreated", newDisplayName);
                }
                SaveGhMeta(doc);
                _lastDocumentGuid = doc.DocumentID;
                _lastDisplayName = newDisplayName;
                
            }
            RegisterDocumentEvents(doc);
            _lastObjectIds = new List<Guid>();
            foreach (var obj in doc.Objects)
                _lastObjectIds.Add(obj.InstanceGuid);
        }

        private void TryRegisterChangeTracking(IGH_DocumentObject obj)
        {
            Type type = obj.GetType();
          
            obj.ObjectChanged += (s, e) =>
            {
                Log("ObjectChanged", $"{obj.Name}, {obj.GetType().Name}");
            };
            obj.AttributesChanged += (s, e) =>
            {
                Log("AttributesChanged", $"{obj.Name}, {obj.GetType().Name}");
            };
          
        }

        private bool AreDocumentsEquivalent(string oldName, string newName, GH_Document doc)
        {
            if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
                return false;

            if (oldName == newName) return false;

            if (doc.Objects.Count != _lastObjectIds.Count)
                return false;

            // 全オブジェクトのGUIDが一致するなら、内容は同じとみなす（SaveAsの可能性大）
            for (int i = 0; i < doc.Objects.Count; i++)
            {
                if (doc.Objects[i].InstanceGuid != _lastObjectIds[i])
                    return false;
            }

            return true;
        }
        
        private void RegisterDocumentEvents(GH_Document doc)
        {
            doc.ContextChanged += (sender, args) =>
            {
                Log("ContextChanged", $"Context: {args.Context.ToString()}");
            };
            doc.ModifiedChanged += (sender, args) =>
            {
                Log("ModifiedChanged", $"Modified: {args.Modified.ToString()}  {sender.ToString()} ");
                
            };
           
            doc.ObjectsAdded += (sender, args) =>
            {
                foreach (var obj in args.Objects)
                {
                    string typeName = obj.GetType().Name;
                    Log("ObjectAdded", $"{typeName}, {obj.Name}");
                    TryRegisterChangeTracking(obj);
                    
                }
            };

            doc.ObjectsDeleted += (sender, args) =>
            {
                if (_isSaveAsTransition)
                {
                    _isSaveAsTransition = false; // 一度だけ抑制
                    return;
                }

                foreach (var obj in args.Objects)
                {
                    Log("ObjectDeleted", $"{obj.GetType().Name}, {obj.Name}");
                }
            };

            doc.SolutionEnd += (sender, args) =>
            { 
                var now = DateTime.Now;
                foreach (var obj in doc.Objects)
                {
                    // Slider
                    if (obj is GH_NumberSlider slider)
                    {
                        var id = slider.InstanceGuid;
                        var val = (double)slider.CurrentValue;
                        if (!_lastSliderValues.ContainsKey(id) || Math.Abs(_lastSliderValues[id] - val) > 0.0001)
                        {
                            _lastSliderValues[id] = val;
                            _lastSliderChangeTime[id] = now;
                        }

                        if (_lastSliderChangeTime.ContainsKey(id) &&
                            (now - _lastSliderChangeTime[id]).TotalMilliseconds >= delay &&  // delay
                            (!_lastRecordedSliderValues.ContainsKey(id) || Math.Abs(_lastRecordedSliderValues[id] - val) > 0.0001))
                        {
                            Log("SliderChanged", $"{slider.Name}, {val}");
                            _lastRecordedSliderValues[id] = val;
                        }
                    }

                    // Toggle
                    if (obj is GH_BooleanToggle toggle)
                    {
                        var id = toggle.InstanceGuid;
                        bool val = toggle.Value;

                        if (!_lastToggleStates.ContainsKey(id) || _lastToggleStates[id] != val)
                        {
                            if (!_lastToggleChangeTime.ContainsKey(id))
                                _lastToggleChangeTime[id] = now;
                            else if ((now - _lastToggleChangeTime[id]).TotalMilliseconds >= delay)
                            {
                                Log("ToggleChanged", $"{toggle.Name}, {val}");
                                _lastToggleStates[id] = val;
                                _lastToggleChangeTime[id] = now;
                            }
                        }
                    }

                    // Panel
                    if (obj is GH_Panel panel)
                    {
                        var id = panel.InstanceGuid;
                        string text = panel.UserText;

                        if (!_lastPanelTexts.ContainsKey(id) || _lastPanelTexts[id] != text)
                        {
                            if (!_lastPanelChangeTime.ContainsKey(id))
                                _lastPanelChangeTime[id] = now;
                            else if ((now - _lastPanelChangeTime[id]).TotalMilliseconds >= delay)
                            {
                                Log("PanelChanged", $"{panel.Name}, {text}");
                                _lastPanelTexts[id] = text;
                                _lastPanelChangeTime[id] = now;
                            }
                        }
                    }

                    // ValueList
                    if (obj is GH_ValueList valueList)
                    {
                        var id = valueList.InstanceGuid;
                        string joined = string.Join(";", valueList.ListItems.Select(i => i.Name + "=" + i.Value));

                        if (!_lastValueListState.ContainsKey(id) || _lastValueListState[id] != joined)
                        {
                            if (!_lastValueListChangeTime.ContainsKey(id))
                                _lastValueListChangeTime[id] = now;
                            else if ((now - _lastValueListChangeTime[id]).TotalMilliseconds >= delay)
                            {
                                Log("ValueListChanged", $"{valueList.Name}, Items: {joined}");
                                _lastValueListState[id] = joined;
                                _lastValueListChangeTime[id] = now;
                            }
                        }
                    }

                    // GraphMapper
                    if (obj.GetType().Name.Contains("GH_GraphMapper"))
                    {
                        var id = obj.InstanceGuid;
                        var serialized = JsonSerializer.Serialize(obj);

                        if (!_lastGraphData.ContainsKey(id) || _lastGraphData[id] != serialized)
                        {
                            if (!_lastGraphChangeTime.ContainsKey(id))
                                _lastGraphChangeTime[id] = now;
                            else if ((now - _lastGraphChangeTime[id]).TotalMilliseconds >= delay)
                            {
                                Log("GraphMapperChanged", $"{obj.Name}");
                                _lastGraphData[id] = serialized;
                                _lastGraphChangeTime[id] = now;
                            }
                        }
                    }

                    // MDSlider
                    if (obj.GetType().Name.Contains("GH_MultiDimensionalSlider"))
                    {
                        var id = obj.InstanceGuid;
                        var serialized = JsonSerializer.Serialize(obj);

                        if (!_lastMDSliderValue.ContainsKey(id) || _lastMDSliderValue[id] != serialized)
                        {
                            if (!_lastMDSliderChangeTime.ContainsKey(id))
                                _lastMDSliderChangeTime[id] = now;
                            else if ((now - _lastMDSliderChangeTime[id]).TotalMilliseconds >= delay)
                            {
                                Log("MDSliderChanged", $"{obj.Name}");
                                _lastMDSliderValue[id] = serialized;
                                _lastMDSliderChangeTime[id] = now;
                            }
                        }
                    }
                    if (obj is IGH_Param param)
                    {
                        var currentSources = new List<Guid>();
                        foreach (var src in param.Sources)
                            currentSources.Add(src.InstanceGuid);

                        if (_lastSources.TryGetValue(param.InstanceGuid, out var oldSources))
                        {
                            foreach (var added in currentSources)
                                if (!oldSources.Contains(added))
                                    Log("WireConnected", $"{added} → {param.InstanceGuid}");

                            foreach (var removed in oldSources)
                                if (!currentSources.Contains(removed))
                                    Log("WireDisconnected", $"{removed} -/→ {param.InstanceGuid}");
                        }

                        _lastSources[param.InstanceGuid] = currentSources;
                    }
                    
                }

                var currentlyDisabled = new HashSet<Guid>();
                foreach (var obj in doc.DisabledObjects())
                    currentlyDisabled.Add(obj.InstanceGuid);

                foreach (var id in currentlyDisabled)
                    if (!_disabledBefore.Contains(id))
                        Log("ComponentDisabled", id.ToString());

                foreach (var id in _disabledBefore)
                    if (!currentlyDisabled.Contains(id))
                        Log("ComponentEnabled", id.ToString());

                _disabledBefore = currentlyDisabled;
                DetectDocumentSaved(doc);
                SaveGhMeta(doc);
            };
        }
        
        

        private void DetectDocumentSaved(GH_Document doc)
        {
            string path = doc.FilePath;

            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                var currentWriteTime = File.GetLastWriteTime(path);

                if (_lastKnownPath != path)
                {
                    Log("DocumentSavedAs", Path.GetFileName(path));
                    _lastKnownPath = path;
                    _lastWriteTime = currentWriteTime;
                }
                else if (currentWriteTime > _lastWriteTime.AddSeconds(1)) // 少しのバッファを入れる
                {
                    Log("DocumentSaved", Path.GetFileName(path));
                    _lastWriteTime = currentWriteTime;
                }
            }
        }

        private void SaveGhMeta(GH_Document doc)
        {
            int sliderCount = 0, noteCount = 0, compCount = 0, groupCount = 0, scriptCount = 0;

            foreach (var obj in doc.Objects)
            {
                compCount++;
                if (obj is GH_NumberSlider) sliderCount++;
                if (obj is GH_Scribble) noteCount++;
                if (obj is GH_Group) groupCount++;
                if (obj.Name.Contains("Python") || obj.Name.Contains("CSharp") || obj.Name.Contains("CScript")) scriptCount++;
            }

            var meta = new
            {
                User = _userID,
                DocumentName = doc.DisplayName ?? "Untitled",
                Created = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ComponentCount = compCount,
                SliderCount = sliderCount,
                NoteCount = noteCount,
                GroupCount = groupCount,
                ScriptCount = scriptCount
            };

            // GHファイル名に基づいたファイル名
            string baseName = string.IsNullOrEmpty(doc.DisplayName) ? "Untitled" : Path.GetFileNameWithoutExtension(doc.DisplayName);
            string metaFilePath = Path.Combine(_sessionFolder, $"{_userID}_{baseName}_Meta.json");

            string json = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(metaFilePath, json);
        }
    }
}