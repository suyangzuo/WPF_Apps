using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace WPF_Typing
{
    /// <summary>
    /// 错误字符信息
    /// </summary>
    public class ErrorCharInfo
    {
        public char ExpectedChar { get; set; }
        public char ActualChar { get; set; }
    }

    /// <summary>
    /// 测试结果数据模型
    /// </summary>
    public class TestResult
    {
        public string TesterName { get; set; } = string.Empty;
        public string FolderName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public double CompletionRate { get; set; }
        public int TotalChars { get; set; }
        public int CorrectChars { get; set; }
        public int IncorrectChars { get; set; }
        public List<ErrorCharInfo> ErrorChars { get; set; } = new();
        public double Accuracy { get; set; }
        public double Speed { get; set; }
        public int BackspaceCount { get; set; }
        public DateTime TestStartTime { get; set; }
        public DateTime TestEndTime { get; set; }
        public DateTime TestTime { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// SQLite 数据库操作辅助类
    /// </summary>
    public static class DatabaseHelper
    {
        private static string GetDatabasePath()
        {
            // 数据库文件放在项目根目录（WPF_Typing 文件夹）
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // 尝试找到项目根目录（WPF_Typing 文件夹）
            var dir = new DirectoryInfo(baseDir);

            // 如果在 bin 目录下，向上查找项目根目录
            while (dir != null)
            {
                // 检查是否是项目根目录（包含 WPF_Typing.csproj 文件）
                var csprojFile = Path.Combine(dir.FullName, "WPF_Typing.csproj");
                if (File.Exists(csprojFile))
                {
                    return Path.Combine(dir.FullName, "typing_results.db");
                }

                // 如果当前目录名是 WPF_Typing，也认为是项目根目录
                if (dir.Name == "WPF_Typing")
                {
                    return Path.Combine(dir.FullName, "typing_results.db");
                }

                dir = dir.Parent;
            }

            // 如果找不到项目根目录，使用 BaseDirectory
            return Path.Combine(baseDir, "typing_results.db");
        }

        /// <summary>
        /// 初始化数据库，创建表结构
        /// </summary>
        public static void InitializeDatabase()
        {
            var dbPath = GetDatabasePath();
            var connectionString = $"Data Source={dbPath}";

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var createTableCommand = connection.CreateCommand();
            createTableCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS TestResults (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    TesterName TEXT NOT NULL,
                    FolderName TEXT NOT NULL,
                    FileName TEXT NOT NULL,
                    CompletionRate REAL NOT NULL,
                    TotalChars INTEGER NOT NULL,
                    CorrectChars INTEGER NOT NULL,
                    IncorrectChars INTEGER NOT NULL,
                    ErrorCharsInfo TEXT,
                    Accuracy REAL NOT NULL,
                    Speed REAL NOT NULL,
                    BackspaceCount INTEGER NOT NULL,
                    TestStartTime DATETIME NOT NULL,
                    TestEndTime DATETIME NOT NULL,
                    TestTime DATETIME NOT NULL
                )";

            createTableCommand.ExecuteNonQuery();
        }

        /// <summary>
        /// 保存测试结果到数据库
        /// </summary>
        public static void SaveTestResult(TestResult result)
        {
            try
            {
                InitializeDatabase();

                var dbPath = GetDatabasePath();
                var connectionString = $"Data Source={dbPath}";

                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                // 将错误字符信息序列化为 JSON
                string errorCharsJson = string.Empty;
                if (result.ErrorChars != null && result.ErrorChars.Count > 0)
                {
                    var errorList = result.ErrorChars.Select(e => new
                    {
                        Expected = e.ExpectedChar.ToString(),
                        Actual = e.ActualChar.ToString()
                    }).ToList();
                    errorCharsJson = System.Text.Json.JsonSerializer.Serialize(errorList);
                }

                var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = @"
                    INSERT INTO TestResults 
                    (TesterName, FolderName, FileName, CompletionRate, TotalChars, CorrectChars, 
                     IncorrectChars, ErrorCharsInfo, Accuracy, Speed, BackspaceCount, TestStartTime, TestEndTime, TestTime)
                    VALUES 
                    (@TesterName, @FolderName, @FileName, @CompletionRate, @TotalChars, @CorrectChars,
                     @IncorrectChars, @ErrorCharsInfo, @Accuracy, @Speed, @BackspaceCount, @TestStartTime, @TestEndTime, @TestTime)";

                insertCommand.Parameters.AddWithValue("@TesterName", result.TesterName);
                insertCommand.Parameters.AddWithValue("@FolderName", result.FolderName);
                insertCommand.Parameters.AddWithValue("@FileName", result.FileName);
                insertCommand.Parameters.AddWithValue("@CompletionRate", result.CompletionRate);
                insertCommand.Parameters.AddWithValue("@TotalChars", result.TotalChars);
                insertCommand.Parameters.AddWithValue("@CorrectChars", result.CorrectChars);
                insertCommand.Parameters.AddWithValue("@IncorrectChars", result.IncorrectChars);
                insertCommand.Parameters.AddWithValue("@ErrorCharsInfo", errorCharsJson);
                insertCommand.Parameters.AddWithValue("@Accuracy", result.Accuracy);
                insertCommand.Parameters.AddWithValue("@Speed", result.Speed);
                insertCommand.Parameters.AddWithValue("@BackspaceCount", result.BackspaceCount);
                insertCommand.Parameters.AddWithValue("@TestStartTime", result.TestStartTime);
                insertCommand.Parameters.AddWithValue("@TestEndTime", result.TestEndTime);
                insertCommand.Parameters.AddWithValue("@TestTime", result.TestTime);

                insertCommand.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                // 记录错误但不中断程序
                System.Diagnostics.Debug.WriteLine($"保存测试结果失败: {ex.Message}");
            }
        }
    }
}

