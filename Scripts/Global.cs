using System;
using System.Linq;
using Godot;
using Godot.Collections;

namespace Arcomage.Scripts
{
    public partial class Global : Node
    {
        public static Table Table { get; set; }
        public static Settings Settings { get; set; }
        public static NetworkSetup NetworkSetup { get; set; }
        public static string BuildNumber { get; private set; }

        public override void _Ready()
        {
            OS.LowProcessorUsageMode = true;
            BuildNumber = LoadBuildNumber();
        }
        
        public static Dictionary<string, string> GetCommandLineArgs()
        {
            return new Dictionary<string, string>(OS.GetCmdlineArgs()
                .Where(arg => arg.StartsWith("--"))
                .Select(arg => arg[2..].Split("=", 2))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0], parts => parts[1]));
        }

        private static string GetTime() => DateTime.Now.ToString("HH:mm:ss");

        private static string LoadBuildNumber()
        {
            const string fileName = "res://build.tres";
            if (FileAccess.FileExists(fileName))
            {
                using var file = FileAccess.Open(fileName, FileAccess.ModeFlags.Read);
                var content = file.GetAsText();
                file.Close();
                return content.Trim();
            }

            Logger.Error("Build.tres file missing");
            return "00000000";
        }
    }
}