using Arcomage.Core;

namespace Arcomage.Gameplay;

public class Player
{
   public long Id { get; init; }
   public string Name { get; init; }
   public bool Host { get; init; }
   public bool Ai { get; set; }
   public bool Ready { get; set; }

   public bool PlayAgain { get; set; } = false;
   public bool Discarding { get; set; } = false;
   public bool DrawCard { get; set; } = false;

   public int TowerHp { get; set; } = Config.Settings.TowerLevels;
   public int WallHp { get; set; } = Config.Settings.WallLevels;

   public int Quarries { get; set; } = Config.Settings.QuarryLevels;
   public int Bricks { get; set; } = Config.Settings.BrickQuantity;
   public int Magic { get; set; } = Config.Settings.MagicLevels;
   public int Gems { get; set; } = Config.Settings.GemQuantity;
   public int Dungeons { get; set; } = Config.Settings.DungeonLevels;
   public int Recruits { get; set; } = Config.Settings.RecruitQuantity;

   public override string ToString() => $"{Name} ({Id})";
}
