namespace Arcomage.Gameplay;

public partial class Table
{
   public Player GetCurrentPlayer()
   {
      Players.TryGetValue(_turnPlayerId, out var player);
      return player;
   }

   public int GetValue(Player player, ResourceTypes resourceType) => player.Get(resourceType);

   public void GainValue(Player targetPlayer, ResourceTypes resource, int amount) => targetPlayer.Add(resource, amount);

   public void SetValue(Player targetPlayer, ResourceTypes resource, int amount) => targetPlayer.Set(resource, amount);

   public Player[] GetTargetPlayer(Player self, TargetType target)
   {
      var players = LivingPlayers().ToArray();
      return target switch
      {
         TargetType.Self => [self],
         TargetType.Opponent => [GetOpponent(self)],
         TargetType.All => players,
         TargetType.AllExceptSelf => players.Where(player => player.Id != self.Id).ToArray(),
         TargetType.Enemies => EnemiesOf(self).ToArray(),
         TargetType.Allies => AlliesOf(self).ToArray(),
         TargetType.LowestWall => [players.OrderBy(player => GetValue(player, ResourceTypes.Wall)).FirstOrDefault()],
         TargetType.HighestWall => [players.OrderByDescending(player => GetValue(player, ResourceTypes.Wall)).FirstOrDefault()],
         TargetType.LowestTower => [players.OrderBy(player => GetValue(player, ResourceTypes.Tower)).FirstOrDefault()],
         TargetType.HighestTower => [players.OrderByDescending(player => GetValue(player, ResourceTypes.Tower)).FirstOrDefault()],
         _ => throw new ArgumentOutOfRangeException(nameof(target), target, null)
      };
   }

   public Player GetOpponent(Player self)
   {
      if (self == null)
         return null;

      var targetId = self.SelectedTargetId != 0 ? self.SelectedTargetId : GetDefaultEnemyId(self);
      if (Players.TryGetValue(targetId, out var targeted) && !targeted.Eliminated && AreEnemies(self, targeted))
         return targeted;

      return EnemiesOf(self).FirstOrDefault();
   }

   public void Damage(Player target, int amount, ResourceTypes? resource = null)
   {
      if (amount <= 0)
         return;

      if (resource == ResourceTypes.Tower)
      {
         target.TowerHp = Mathf.Max(0, target.TowerHp - amount);
         return;
      }

      if (resource == ResourceTypes.Wall)
      {
         target.WallHp = Mathf.Max(0, target.WallHp - amount);
         return;
      }

      var wallDamage = Mathf.Min(target.WallHp, amount);
      target.WallHp -= wallDamage;

      var towerDamage = amount - wallDamage;
      target.TowerHp = Mathf.Max(0, target.TowerHp - towerDamage);
   }
}
