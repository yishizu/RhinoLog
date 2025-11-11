using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace GELTrainingLog
{
    
    public class GELTrainingLogCommand : Command
    {
        public GELTrainingLogCommand()
        {
            // Rhino only creates one instance of each command class defined in a
            // plug-in, so it is safe to store a refence in a static property.
            Instance = this;
        }

        ///<summary>The only instance of this command.</summary>
        public static GELTrainingLogCommand Instance { get; private set; }

        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName => "GELUserLogin";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // .geluser ファイルを選択
            var openFileDialog = new Rhino.UI.OpenFileDialog
            {
                Filter = "GEL User Config (*.geluser)|*.geluser",
                Title = "GEL User Config File を選択してください"
            };

            if (!openFileDialog.ShowOpenDialog())
            {
                RhinoApp.WriteLine("Cancelled.");
                return Result.Cancel;
            }

            string filePath = openFileDialog.FileName;

            try
            {
                // ファイルを読み込み
                string jsonContent = System.IO.File.ReadAllText(filePath);
                var userConfig = System.Text.Json.JsonSerializer.Deserialize<UserConfig>(jsonContent);

                if (userConfig == null || string.IsNullOrWhiteSpace(userConfig.user_id))
                {
                    RhinoApp.WriteLine("⚠ 無効な設定ファイルです。");
                    return Result.Failure;
                }

                // 設定を保存
                string configPath = System.IO.Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                    "GEL", "user_config.txt");

                string configDir = System.IO.Path.GetDirectoryName(configPath);
                if (!System.IO.Directory.Exists(configDir))
                {
                    System.IO.Directory.CreateDirectory(configDir);
                }

                System.IO.File.WriteAllText(configPath, userConfig.user_id);

                RhinoApp.WriteLine("✓ ユーザー登録が完了しました！");
                RhinoApp.WriteLine($"✓ User ID: {userConfig.user_id}");
                RhinoApp.WriteLine($"✓ 氏名: {userConfig.full_name}");
                RhinoApp.WriteLine("✓ Rhinoを再起動してください。");

                return Result.Success;
            }
            catch (System.Exception ex)
            {
                RhinoApp.WriteLine("⚠ エラー: " + ex.Message);
                return Result.Failure;
            }
        }

        private class UserConfig
        {
            public string user_id { get; set; }
            public string full_name { get; set; }
            public string email { get; set; }
            public string organization { get; set; }
            public string start_date { get; set; }
            public string end_date { get; set; }
            public string registered_at { get; set; }
        }
    }
}
