namespace Arcomage.Networking;

public sealed class GameSnapshot
{
   public long TurnPlayerId { get; init; }
   public MatchMode Mode { get; init; }
   public bool Ranked { get; init; }
   public bool Discarding { get; init; }
   public bool GameOver { get; init; }
   public long WinnerId { get; init; }
   public string WinReason { get; init; } = string.Empty;
   public long[] SeatOrder { get; init; } = [];
   public List<PlayerSnapshot> Players { get; init; } = [];
}

public sealed class PlayerSnapshot
{
   public long Id { get; init; }
   public string Name { get; init; } = string.Empty;
   public int SeatIndex { get; init; }
   public int TeamId { get; init; }
   public bool Eliminated { get; init; }
   public bool Ai { get; init; }
   public bool Host { get; init; }
   public int TowerHp { get; init; }
   public int WallHp { get; init; }
   public int Quarries { get; init; }
   public int Bricks { get; init; }
   public int Magic { get; init; }
   public int Gems { get; init; }
   public int Dungeons { get; init; }
   public int Recruits { get; init; }
   public string[] Hand { get; init; } = [];
   public int HandCount { get; init; }
}

public sealed class CardPlayCue
{
   public long PlayerId { get; init; }
   public long TargetId { get; init; }
   public int CardIndex { get; init; }
   public bool Discarded { get; init; }
   public string ReplacementId { get; init; } = string.Empty;
   public bool ClearGraveyard { get; init; }
   public string PlayedCardId { get; init; } = string.Empty;
   public List<PlayerSnapshot> Players { get; init; } = [];
}

public static class SnapshotJson
{
   private static readonly JsonSerializerOptions _options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

   public static string Serialize<T>(T value)
   {
      return JsonSerializer.Serialize(value, _options);
   }

   public static T Deserialize<T>(string json)
   {
      return JsonSerializer.Deserialize<T>(json, _options);
   }
}
