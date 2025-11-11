using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;

class TrainingPeriod
{
    [JsonPropertyName("user_name")]
    
    public string UserName { get; set; }
    [JsonPropertyName("start_date")]
    public string StartDate { get; set; }

    [JsonPropertyName("end_date")]
    public string EndDate { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = "GEL Training Period";

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    [JsonPropertyName("server_url")]
    public string ServerUrl { get; set; }
}

class Program
{
    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.InputEncoding = System.Text.Encoding.UTF8;
        
        Console.WriteLine("📝 GEL Training Period 設定ツール");
        Console.Write("ユーザー名を入力してください: ");
        string userName = Console.ReadLine()?.Trim();
        
        

        if (string.IsNullOrWhiteSpace(userName))
        {
            Console.WriteLine("ユーザー名が無効です。終了します。");
            return;
        }
        Console.WriteLine($"ユーザー名: {userName}");
        

        // 既定値
        string defaultStart = "2025-05-19";
        string defaultEnd = "2025-05-30";

        Console.WriteLine($"▶ デフォルトの研修期間: {defaultStart} ～ {defaultEnd}");
        Console.Write("そのままでよければ Enter、変更するなら Y を入力してください: ");
        string input = Console.ReadLine()?.Trim().ToLower();

        string startDate = defaultStart;
        string endDate = defaultEnd;

        if (input == "y")
        {
            Console.Write("▶ 開始日を入力（例: 2025-06-01）: ");
            startDate = Console.ReadLine()?.Trim() ?? defaultStart;

            Console.Write("▶ 終了日を入力（例: 2025-06-10）: ");
            endDate = Console.ReadLine()?.Trim() ?? defaultEnd;
        }

        if (!DateTime.TryParse(startDate, out _) || !DateTime.TryParse(endDate, out _))
        {
            Console.WriteLine("❌ 日付の形式が正しくありません。yyyy-MM-dd 形式で入力してください。");
            return;
        }

        // サーバーURL設定
        Console.Write("▶ サーバーURL（未入力の場合はサーバー送信なし）: ");
        string serverUrl = Console.ReadLine()?.Trim();

        var period = new TrainingPeriod
        {
            UserName = userName,
            StartDate = startDate,
            EndDate = endDate,
            ServerUrl = serverUrl
        };
        
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        string json = JsonSerializer.Serialize(period, options);

        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string folder = Path.Combine(desktop, "GEL");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "training_period.json");

       // File.WriteAllText(path, json);
        File.WriteAllText(path, json, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        Console.WriteLine($"\n✅ training_period.json を作成しました：\n{path}");
    }
}