namespace Arcomage.Networking;

public enum MatchMode
{
   OneVsOne = 0,
   FreeForAll = 1,
   TwoVsTwo = 2
}

public static class MatchModeRules
{
   public static int MinPlayers(MatchMode mode) => mode switch
   {
      MatchMode.FreeForAll => 3,
      MatchMode.TwoVsTwo => 4,
      _ => 2
   };

   public static int MaxPlayers(MatchMode mode) => mode switch
   {
      MatchMode.FreeForAll => 4,
      MatchMode.TwoVsTwo => 4,
      _ => 2
   };

   public static string QueueName(MatchMode mode, bool ranked) => ranked ? $"ranked_{mode}" : $"casual_{mode}";
}
