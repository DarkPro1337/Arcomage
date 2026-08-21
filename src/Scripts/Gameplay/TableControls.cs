using Arcomage.UI;
using Godot;

namespace Arcomage.Gameplay;

public partial class Table
{
   private Control _particles;
   private TextureRect _graveyardCardBack;
   private GridContainer _graveyard;
   private Label _drawCardLabel;
   private Control _matchResult;
   private Timer _timeElapsed;
   private HBoxContainer _redDeck;
   private HBoxContainer _blueDeck;
   private ColorRect _deckLocker;
   private Control _cardAnimLayer;
   private InGameMenu _inGameMenu;

   private Control Particles => _particles ??= GetNode<Control>("Particles");
   private TextureRect GraveyardCardBack => _graveyardCardBack ??= GetNode<TextureRect>("Graveyard/CardBack");
   private GridContainer Graveyard => _graveyard ??= GetNode<GridContainer>("Graveyard");
   private Label DrawCardLabel => _drawCardLabel ??= GetNode<Label>("DrawCardLabel");
   private Control MatchResult => _matchResult ??= GetNode<Control>("MatchResult");
   private Timer TimeElapsed => _timeElapsed ??= GetNode<Timer>("TimeElapsed");
   private HBoxContainer RedDeck => _redDeck ??= GetNode<HBoxContainer>("RedDeck");
   private HBoxContainer BlueDeck => _blueDeck ??= GetNode<HBoxContainer>("BlueDeck");
   private ColorRect DeckLocker => _deckLocker ??= GetNode<ColorRect>("DeckLocker");
   private Control CardAnimLayer => _cardAnimLayer ??= GetNode<Control>("CardAnimLayer");
   private InGameMenu InGameMenu => _inGameMenu ??= GetNode<InGameMenu>("InGameMenu");
}
