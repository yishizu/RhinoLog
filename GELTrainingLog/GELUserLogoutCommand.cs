using System;
using Rhino;
using Rhino.Commands;

namespace GELTrainingLog
{
    public class GELUserLogoutCommand : Command
    {
        public GELUserLogoutCommand()
        {
            Instance = this;
        }

        public static GELUserLogoutCommand Instance { get; private set; }

        public override string EnglishName => "GELUserLogout";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            try
            {
                // 設定ファイルのパス
                string configPath = System.IO.Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                    "GEL", "user_config.txt");

                if (!System.IO.File.Exists(configPath))
                {
                    RhinoApp.WriteLine("⚠ ログインしていません。");
                    return Result.Nothing;
                }

                // 現在のユーザーIDを確認
                string currentUserID = System.IO.File.ReadAllText(configPath).Trim();

                // 確認ダイアログ
                var result = Rhino.UI.Dialogs.ShowMessage(
                    $"ユーザー「{currentUserID}」をログアウトしますか？\n\n" +
                    "ログアウト後、ログ記録は停止されます。\n" +
                    "再度ログインするには、Rhinoを再起動して\n" +
                    "GELUserLoginコマンドを実行してください。",
                    "GEL User Logout",
                    Rhino.UI.ShowMessageButton.OKCancel,
                    Rhino.UI.ShowMessageIcon.Question);

                if (result == Rhino.UI.ShowMessageResult.OK)
                {
                    // 設定ファイルを削除
                    System.IO.File.Delete(configPath);

                    RhinoApp.WriteLine("========================================");
                    RhinoApp.WriteLine("✓ ログアウトしました");
                    RhinoApp.WriteLine("========================================");
                    RhinoApp.WriteLine($"User ID: {currentUserID}");
                    RhinoApp.WriteLine("");
                    RhinoApp.WriteLine("ログ記録は停止されました。");
                    RhinoApp.WriteLine("再度ログインするには、Rhinoを再起動してください。");
                    RhinoApp.WriteLine("========================================");

                    return Result.Success;
                }
                else
                {
                    RhinoApp.WriteLine("ログアウトをキャンセルしました。");
                    return Result.Cancel;
                }
            }
            catch (System.Exception ex)
            {
                RhinoApp.WriteLine("⚠ エラー: " + ex.Message);
                return Result.Failure;
            }
        }
    }
}
