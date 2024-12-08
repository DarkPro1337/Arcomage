using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Arcomage.Scripts.Data;

public class Card
{
   public string Id { get; set; }
   public string Name { get; set; }
   public string Description { get; set; }
   public CardType Type { get; set; }
   public int Cost { get; set; }
   public string Pic { get; set; }
   public List<ActionBase> Actions { get; set; }
   public List<CardsUse> Uses { get; set; }
   public List<CardFeature> Features { get; set; }
}

public class Deck
{
   public string Name { get; set; }
   public string Description { get; set; }
   public List<Card> Cards { get; set; } = [];
   [YamlIgnore] public bool IsEnabled { get; set; }
}