namespace Arcomage.Gameplay;

public class Player
{
   public static bool TryRegister(IDictionary<long, Player> players, long id, string name)
   {
      if (players.ContainsKey(id))
         return false;

      players.Add(id, new Player { Id = id, Name = name, Host = id == 1, Ai = false });
      return true;
   }

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

   public int ResourceTotal => Bricks + Gems + Recruits;

   public int Get(ResourceTypes resource)
   {
      return resource switch
      {
         ResourceTypes.Tower => TowerHp,
         ResourceTypes.Wall => WallHp,
         ResourceTypes.Quarry => Quarries,
         ResourceTypes.Magic => Magic,
         ResourceTypes.Dungeon => Dungeons,
         ResourceTypes.Bricks => Bricks,
         ResourceTypes.Gems => Gems,
         ResourceTypes.Recruits => Recruits,
         _ => throw new ArgumentOutOfRangeException(nameof(resource), resource, "Invalid resource type")
      };
   }

   public void Add(ResourceTypes resource, int amount) => Set(resource, Math.Max(0, Get(resource) + amount));

   public void Set(ResourceTypes resource, int amount)
   {
      switch (resource)
      {
         case ResourceTypes.Tower:
            TowerHp = amount;
            break;
         case ResourceTypes.Wall:
            WallHp = amount;
            break;
         case ResourceTypes.Quarry:
            Quarries = amount;
            break;
         case ResourceTypes.Magic:
            Magic = amount;
            break;
         case ResourceTypes.Dungeon:
            Dungeons = amount;
            break;
         case ResourceTypes.Bricks:
            Bricks = amount;
            break;
         case ResourceTypes.Gems:
            Gems = amount;
            break;
         case ResourceTypes.Recruits:
            Recruits = amount;
            break;
         default:
            throw new ArgumentOutOfRangeException(nameof(resource), resource, "Invalid resource type");
      }
   }

   public override string ToString() => $"{Name} ({Id})";
}
