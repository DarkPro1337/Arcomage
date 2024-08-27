using Godot;

namespace Arcomage.Scripts;

public enum Tavern
{
    None,
    Harmondale,
    Erathia,
    TulareanForest,
    Deyja,
    BracadaDesert,
    Celeste,
    ThePit,
    EvermornIsland,
    Nighon,
    BarrowDowns,
    Tidewater,
    Avlee,
    StoneCity
}
    
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
    
    public Tavern CurrentTavern { get; set; } = Tavern.None;

    public Locale CurrentLocale { get; set; } = Locale.En;

    public string Nickname { get; set; } = "Player";
}

public partial class Config : Node
{
    public static GameSettings Settings = new();
}