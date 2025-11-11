using System;
using System.Diagnostics;
using System.IO;
using Rhino;
using Rhino.Commands;

namespace GELTrainingLog
{
    public class GELShowLogCommand : Command
    {
        public GELShowLogCommand()
        {
            Instance = this;
        }

        public static GELShowLogCommand Instance { get; private set; }

        public override string EnglishName => "GELShowLog";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            try
            {
                // ユーザーIDを取得
                string configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "GEL", "user_config.txt");

                if (!File.Exists(configPath))
                {
                    RhinoApp.WriteLine("⚠ ユーザーが登録されていません。");
                    RhinoApp.WriteLine("GELUserLogin コマンドでログインしてください。");
                    return Result.Failure;
                }

                string userId = File.ReadAllText(configPath).Trim();

                if (string.IsNullOrWhiteSpace(userId))
                {
                    RhinoApp.WriteLine("⚠ ユーザーIDが無効です。");
                    return Result.Failure;
                }

                // 可視化ページのURLを構築
                string url = $"http://136.111.186.176:5000/dashboard.html?user={userId}";

                // デフォルトブラウザで開く
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });

                RhinoApp.WriteLine($"✓ ログ可視化ページを開きました");
                RhinoApp.WriteLine($"URL: {url}");
                return Result.Success;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"⚠ エラー: {ex.Message}");
                return Result.Failure;
            }
        }
    }
}
