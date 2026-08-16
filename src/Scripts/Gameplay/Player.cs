using Arcomage.Core;

namespace Arcomage.Gameplay;

public class Player
{
   public long Id { get; init; }
   public string Name { get; set; }
   public bool Host { get; init; }
   public bool Ai { get; init; }
   public bool Ready { get; set; }
   public int SeatIndex { get; set; }
   public int TeamId { get; set; }
   public bool Eliminated { get; set; }
   public long SelectedTargetId { get; set; }

   public bool PlayAgain { get; set; }
   public bool Discarding { get; set; }
   public bool DrawCard { get; set; }

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
