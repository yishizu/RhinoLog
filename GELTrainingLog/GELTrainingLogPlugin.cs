using System;
using System.IO;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.PlugIns;
using Environment = System.Environment;


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
    public class GELTrainingLogPlugin : Rhino.PlugIns.PlugIn
    {
        private static string _userID = Environment.UserName; 
        private static string _logFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        private static string _sessionLogFile;
        
        public GELTrainingLogPlugin()
        {
            Instance = this;
        }
        
        ///<summary>Gets the only instance of the GELTrainingLogPlugin plug-in.</summary>
        public static GELTrainingLogPlugin Instance { get; private set; }
        

        public void Log(string action, string detail)
        {
          var logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{_userID},{action},\"{detail}\"\n";
          File.AppendAllText(_sessionLogFile, logLine);
        }

        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
          RhinoApp.WriteLine("Rhino Operation Logger Loaded");

          // 初期ログフォルダはセッションごとのサブフォルダ（デスクトップ/UserID/YYYYMMDD_HHmmss）
          var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
          var sessionFolder = Path.Combine(_logFolder, _userID, timestamp);
          Directory.CreateDirectory(sessionFolder);

          string documentName = "Untitled";
          var doc = RhinoDoc.ActiveDoc;
          if (doc != null && !string.IsNullOrEmpty(doc.Name))
          {
            documentName = Path.GetFileNameWithoutExtension(doc.Name);
          }

          _sessionLogFile = Path.Combine(sessionFolder, $"{_userID}_{documentName}_Log.csv");

          // ヘッダー出力
          File.WriteAllText(_sessionLogFile, "Timestamp,UserID,Action,Detail\n");

          RhinoDoc.AddRhinoObject += OnAddObject;
          RhinoDoc.DeleteRhinoObject += OnDeleteObject;
          Command.BeginCommand += OnCommandBegin;
          Command.EndCommand += OnCommandEnd;
          RhinoDoc.CloseDocument += OnCloseDocument;
          RhinoDoc.BeginOpenDocument  += OnOpenDocument;

          return LoadReturnCode.Success;
        }

        protected override void OnShutdown()
        {
          RhinoDoc.AddRhinoObject -= OnAddObject;
          RhinoDoc.DeleteRhinoObject -= OnDeleteObject;
          Command.BeginCommand -= OnCommandBegin;
          Command.EndCommand -= OnCommandEnd;
          RhinoDoc.CloseDocument -= OnCloseDocument;
          RhinoDoc.BeginOpenDocument  -= OnOpenDocument;
        }

        private void OnAddObject(object sender, RhinoObjectEventArgs e)
        {
          var obj = e.TheObject;
          Log("Object Added", $"ID:{obj.Id}, Type:{obj.ObjectType}, Layer:{obj.Attributes.LayerIndex}");
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
          if (e.CommandEnglishName.Equals("Save", StringComparison.OrdinalIgnoreCase) ||
              e.CommandEnglishName.Equals("SaveAs", StringComparison.OrdinalIgnoreCase))
          {
            var doc = RhinoDoc.ActiveDoc;
            if (doc != null && !string.IsNullOrEmpty(doc.Path))
            {
              var fileInfo = new FileInfo(doc.Path);
              var created = fileInfo.CreationTime;
              var modified = fileInfo.LastWriteTime;
              Log("File Saved", $"Path:{doc.Path}, Created:{created:yyyy-MM-dd HH:mm:ss}, Modified:{modified:yyyy-MM-dd HH:mm:ss}");
            }
            else
            {
              Log("File Saved", "(No document path available)");
            }
          }
        }

        private void OnCloseDocument(object sender, DocumentEventArgs e)
        {
          Log("Document Closed", e.Document.Name);
        }

        private void OnOpenDocument(object sender, DocumentOpenEventArgs e)
        {
          var fileInfo = new FileInfo(e.Document.Path);
          var created = fileInfo.CreationTime;
          var modified = fileInfo.LastWriteTime;
          Log("Document Opened", $"Path:{e.Document.Path}, Created:{created:yyyy-MM-dd HH:mm:ss}, Modified:{modified:yyyy-MM-dd HH:mm:ss}");
        }
    }
}