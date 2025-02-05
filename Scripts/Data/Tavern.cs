using System.Collections.Generic;
using YamlDotNet.Serialization;
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedMember.Global

namespace Arcomage.Scripts.Data;

public class TavernPack
{
   public string Name { get; set; }
   public string Description { get; set; }
   public List<Tavern> Taverns { get; set; }
}

public class Tavern
{
   [YamlIgnore] public int Index { get; set; }
   public string Id { get; set; }
   public string Name { get; set; }
   public int StartingTower { get; set; }
   public int StartingWall { get; set; }
   public int StartingQuarry { get; set; }
   public int StartingMagic { get; set; }
   public int StartingDungeon { get; set; }
   public int StartingBricks { get; set; }
   public int StartingGems { get; set; }
   public int StartingBeasts { get; set; }
   public int WinningTower { get; set; }
   public int WinningResources { get; set; }
}