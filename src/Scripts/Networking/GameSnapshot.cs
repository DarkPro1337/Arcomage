using System.Collections.Generic;
using System.Text.Json;

namespace Arcomage.Networking;

public sealed class GameSnapshot
{
   public long TurnPlayerId { get; set; }
   public MatchMode Mode { get; set; }
   public bool Ranked { get; set; }
   public bool Discarding { get; set; }
   public bool GameOver { get; set; }
   public long WinnerId { get; set; }
   public string WinReason { get; set; } = string.Empty;
   public long[] SeatOrder { get; set; } = [];
   public List<PlayerSnapshot> Players { get; set; } = [];
}

public sealed class PlayerSnapshot
{
   public long Id { get; set; }
   public string Name { get; set; } = string.Empty;
   public int SeatIndex { get; set; }
   public int TeamId { get; set; }
   public bool Eliminated { get; set; }
   public bool Ai { get; set; }
   public bool Host { get; set; }
   public int TowerHp { get; set; }
   public int WallHp { get; set; }
   public int Quarries { get; set; }
   public int Bricks { get; set; }
   public int Magic { get; set; }
   public int Gems { get; set; }
   public int Dungeons { get; set; }
   public int Recruits { get; set; }
   public string[] Hand { get; set; } = [];
   public int HandCount { get; set; }
}

public sealed class CardPlayCue
{
   public long PlayerId { get; set; }
   public int CardIndex { get; set; }
   public bool Discarded { get; set; }
   public string ReplacementId { get; set; } = string.Empty;
   public bool ClearGraveyard { get; set; }
   public string PlayedCardId { get; set; } = string.Empty;
   public List<PlayerSnapshot> Players { get; set; } = [];
}

public static class SnapshotJson
{
   public static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

   public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

   public static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
}
