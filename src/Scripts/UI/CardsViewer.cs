namespace Arcomage.UI;

public partial class CardsViewer : Control
{
   public override void _Ready()
   {
      var container = GetNode<GridContainer>("ScrollContainer/GridContainer");
      var card = ResourceLoader.Load<PackedScene>("res://Scenes/Gameplay/Card.tscn");
      for (var i = 0; i < Global.DeckManager.GetAllCardsCount(); i++)
      {
         var newCard = card.Instantiate<CardControl>();
         newCard.CardIdx = i;
         newCard.Preview = true;
         container.AddChild(newCard);
      }
   }
}
