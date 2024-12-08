using Arcomage.Scripts.Core;
using Godot;

namespace Arcomage.Scripts.UI;

public partial class CardsViewer : Control
{
   public override void _Ready()
   {
      var container = GetNode<GridContainer>("ScrollContainer/GridContainer");
      var card = (PackedScene)ResourceLoader.Load("res://Scenes/Gameplay/Card.tscn");
      for (var i = 0; i < Global.DeckManager.GetAllCardsCount(); i++)
      {
         var newCard = (Control)card.Instantiate();
         newCard.Set("CardIdx", i);
         newCard.Set("Preview", true);
         container.AddChild(newCard);
      }
   }
}