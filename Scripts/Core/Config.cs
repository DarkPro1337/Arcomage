using System;
using System.IO;
using System.Text.Json;
using Arcomage.Scripts.Logging;
using Godot;
using FileAccess = Godot.FileAccess;

namespace Arcomage.Scripts.Core;

public enum AiType
{
   Auto,
   Attack,
   Defence,
   Random
}

public enum Locale
{
   En,
   Ru,
   Uk,
   Pl,
   Da,
   De,
   Fr
}

public class GameSettings
{
   public bool Fullscreen { get; set; }
   public bool Borderless { get; set; }
   public int WindowWidth { get; set; } = 960;
   public int WindowHeight { get; set; } = 540;
   public bool Vsync { get; set; }
   public bool IntroSkip { get; set; }

   public double MasterVolume { get; set; } = 0.5;
   public double MusicVolume { get; set; } = 1;
   public double SoundVolume { get; set; } = 1;
   public bool MuteSound { get; set; }
    
   public bool Singleplayer { get; set; } = true;
   public bool SingleClick { get; set; } = true;
   public int TowerLevels { get; set; } = 50;
   public int WallLevels { get; set; } = 50;
   public int QuarryLevels { get; set; } = 5;
   public int BrickQuantity { get; set; } = 20;
   public int MagicLevels { get; set; } = 3;
   public int GemQuantity { get; set; } = 10;
   public int DungeonLevels { get; set; } = 5;
   public int RecruitQuantity { get; set; } = 20;

   public int AutoBricks { get; set; }
   public int AutoGems { get; set; }
   public int AutoRecruits { get; set; }
   public int CardsInHand { get; set; } = 6;
   public AiType CurrentAiType { get; set; } = AiType.Auto;

   public int TowerVictory { get; set; } = 100;
   public int ResourceVictory { get; set; } = 300;

   public int CurrentTavern { get; set; }

   public Locale CurrentLocale { get; set; } = Locale.En;

   public string Nickname { get; set; } = "Player";
}

public partial class Config : Node
{
   private static readonly Logger _Logger = Logger.GetOrCreateLogger("Config");
   private static readonly JsonSerializerOptions _SerializerOptions = new() { WriteIndented = true };

   private const string ConfigPath = "user://settings.json";

   public static GameSettings Settings = new();

   public override void _EnterTree()
   {
      base._EnterTree();
      Settings = LoadSettings();
      DisplayServer.WindowSetSize(new Vector2I(Settings.WindowWidth, Settings.WindowHeight));
      CenterWindow(GetWindow());
   }

   public static GameSettings LoadSettings()
   {
      try
      {
         if (!FileAccess.FileExists(ConfigPath))
         {
            var defaults = JsonSerializer.Serialize(Settings, _SerializerOptions);
            using var file = FileAccess.Open(ConfigPath, FileAccess.ModeFlags.Write);
            file.StoreString(defaults);
            file.Close();
         }

         using var resource = FileAccess.Open(ConfigPath, FileAccess.ModeFlags.Read);
         var filePath = resource.GetPathAbsolute();
         resource.Close();
         using var stream = new StreamReader(filePath);
         var content = stream.ReadToEnd();
         return JsonSerializer.Deserialize<GameSettings>(content);
      }
      catch (Exception ex)
      {
         _Logger.Error(ex, "Failed to load settings. Fallback to defaults.");
         return new GameSettings();
      }
   }

   public static void SaveSettings()
   {
      try
      {
         var content = JsonSerializer.Serialize(Settings, _SerializerOptions);
         using var file = FileAccess.Open(ConfigPath, FileAccess.ModeFlags.Write);
         file.StoreString(content);
         file.Close();
      }
      catch (Exception ex)
      {
         _Logger.Error(ex, "Failed to save settings.");
      }
   }

   /// <summary>
   /// Centers the specified <see cref="Window"/> on its current screen.
   /// </summary>
   /// <param name="window">The <see cref="Window"/> instance to be centered.</param>
   public static void CenterWindow(Window window)
   {
      var screenSize = DisplayServer.ScreenGetSize(window.CurrentScreen);
      var screenPosition = DisplayServer.ScreenGetPosition(window.CurrentScreen);

      var windowSize = DisplayServer.WindowGetSize();
      var newWindowPosition = screenPosition + (screenSize - windowSize) / 2;
        
      window.Position = newWindowPosition;
   }
}