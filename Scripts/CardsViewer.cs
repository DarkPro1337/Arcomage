using Godot;

namespace Arcomage.Scripts;

public partial class CardsViewer : Control
{
   public override void _Ready()
   {
      var container = GetNode<GridContainer>("ScrollContainer/GridContainer");
      var card = (PackedScene)ResourceLoader.Load("res://Scenes/Card.tscn");
      for (var i = 0; i < 102; i++)
      {
         var newCard = (Control)card.Instantiate();
         newCard.Set("CardIdx", i);
         newCard.Set("Preview", true);
         container.AddChild(newCard);
      }
   }
}