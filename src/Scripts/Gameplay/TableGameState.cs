using System;
using System.Linq;
using Arcomage.Data;
using Godot;

namespace Arcomage.Gameplay;

public partial class Table
{
   public Player GetCurrentPlayer()
   {
      Players.TryGetValue(_turnPlayerId, out var player);
      return player;
   }

   public int GetValue(Player player, ResourceTypes resourceType)
   {
      return resourceType switch
      {
         ResourceTypes.Tower => player.TowerHp,
         ResourceTypes.Wall => player.WallHp,
         ResourceTypes.Quarry => player.Quarries,
         ResourceTypes.Magic => player.Magic,
         ResourceTypes.Dungeon => player.Dungeons,
         ResourceTypes.Bricks => player.Bricks,
         ResourceTypes.Gems => player.Gems,
         ResourceTypes.Recruits => player.Recruits,
         _ => throw new ArgumentOutOfRangeException(nameof(resourceType), resourceType, "Invalid resource type")
      };
   }

   public void GainValue(Player targetPlayer, ResourceTypes resource, int amount)
   {
      switch (resource)
      {
         case ResourceTypes.Tower:
            targetPlayer.TowerHp = AddValue(targetPlayer.TowerHp, amount);
            break;
         case ResourceTypes.Wall:
            targetPlayer.WallHp = AddValue(targetPlayer.WallHp, amount);
            break;
         case ResourceTypes.Quarry:
            targetPlayer.Quarries = AddValue(targetPlayer.Quarries, amount);
            break;
         case ResourceTypes.Magic:
            targetPlayer.Magic = AddValue(targetPlayer.Magic, amount);
            break;
         case ResourceTypes.Dungeon:
            targetPlayer.Dungeons = AddValue(targetPlayer.Dungeons, amount);
            break;
         case ResourceTypes.Bricks:
            targetPlayer.Bricks = AddValue(targetPlayer.Bricks, amount);
            break;
         case ResourceTypes.Gems:
            targetPlayer.Gems = AddValue(targetPlayer.Gems, amount);
            break;
         case ResourceTypes.Recruits:
            targetPlayer.Recruits = AddValue(targetPlayer.Recruits, amount);
            break;
         default:
            throw new ArgumentOutOfRangeException(nameof(resource), resource, "Invalid resource type");
      }
   }

   private static int AddValue(int current, int amount) => Mathf.Max(0, current + amount);

   public void SetValue(Player targetPlayer, ResourceTypes resource, int amount)
   {
      switch (resource)
      {
         case ResourceTypes.Tower:
            targetPlayer.TowerHp = amount;
            break;
         case ResourceTypes.Wall:
            targetPlayer.WallHp = amount;
            break;
         case ResourceTypes.Quarry:
            targetPlayer.Quarries = amount;
            break;
         case ResourceTypes.Magic:
            targetPlayer.Magic = amount;
            break;
         case ResourceTypes.Dungeon:
            targetPlayer.Dungeons = amount;
            break;
         case ResourceTypes.Bricks:
            targetPlayer.Bricks = amount;
            break;
         case ResourceTypes.Gems:
            targetPlayer.Gems = amount;
            break;
         case ResourceTypes.Recruits:
            targetPlayer.Recruits = amount;
            break;
         default:
            throw new ArgumentOutOfRangeException(nameof(resource), resource, "Invalid resource type");
      }
   }

   public Player[] GetTargetPlayer(Player self, TargetType target)
   {
      var players = Players.Values;
      return target switch
      {
         TargetType.Self => [self],
         TargetType.Opponent => [GetOpponent(self)],
         TargetType.All => players.ToArray(),
         TargetType.AllExceptSelf => players.Where(player => player.Id != self.Id).ToArray(),
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

      var opponentId = self.Id == _redPlayerId ? _bluePlayerId : _redPlayerId;
      if (Players.TryGetValue(opponentId, out var opponent))
         return opponent;

      return Players.Values.FirstOrDefault(player => player.Id != self.Id);
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
