using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace Arcomage.Gameplay;

/// <summary>
/// Card flight animations for <see cref="Table"/>: hand → table center → graveyard,
/// deal from the pile, and sweeping the graveyard at the end of a turn.
/// Uses <see cref="Node.CreateTween"/> (Godot 4 has no scene Tween node).
/// </summary>
public partial class Table
{
   private static readonly Vector2 _cardSize = new(135, 180);
   private static readonly Vector2 _playCenterOffset = new(0, -50);

   private const float MoveToCenterDuration = 1.25f;
   private const float MoveToGraveyardDuration = 1.0f;
   private const float DiscardToGraveyardDuration = 1.25f;
   private const float FadeDuration = 0.25f;
   private const float DealDuration = 1.0f;
   private const float GraveyardMoveDuration = 1.0f;
   private const float GraveyardFadeDuration = 1.5f;

   /// <summary>
   /// Plays the used or discarded card to the graveyard, then deals replacements into the hand.
   /// </summary>
   /// <param name="deck">Hand that currently owns <paramref name="card"/>.</param>
   /// <param name="card">Card being played or discarded.</param>
   /// <param name="discarded"><see langword="true"/> to skip the center pause and stamp DISCARDED.</param>
   /// <param name="replacementId">Card drawn into the emptied slot, or empty when not replacing.</param>
   private async Task AnimateCardPlay(HBoxContainer deck, CardControl card, bool discarded, string replacementId)
   {
      var slotIndex = 0;
      var startPos = deck?.GlobalPosition ?? GetPlayCenterPosition();
      if (card.GetParent() == deck)
      {
         slotIndex = card.GetIndex();
         startPos = card.GlobalPosition;
      }

      card.BeginPlayAnimation(discarded);

      var graveyardCard = CreateGraveyardCopy(card.CardId, discarded);
      if (!IsInsideTree() || !IsInstanceValid(graveyardCard))
         return;

      var graveyardPos = GetGraveyardSlotPosition(graveyardCard.GetIndex());
      var placeholder = CreateHandPlaceholder();
      if (deck != null)
      {
         deck.AddChild(placeholder);
         if (slotIndex >= 0 && slotIndex < deck.GetChildCount())
            deck.MoveChild(placeholder, slotIndex);

         if (card.GetParent() == deck)
            deck.RemoveChild(card);
      }

      if (card.GetParent() != null && card.GetParent() != CardAnimLayer)
         card.GetParent().RemoveChild(card);

      PlaceFlyingCard(card, startPos);

      var tween = CreateCardTween();
      if (!discarded)
      {
         tween.TweenProperty(card, "global_position", GetPlayCenterPosition(), MoveToCenterDuration);
         tween.TweenProperty(card, "global_position", graveyardPos, MoveToGraveyardDuration);
      }
      else
      {
         tween.TweenProperty(card, "global_position", graveyardPos, DiscardToGraveyardDuration);
      }

      tween.TweenProperty(card, "modulate", new Color(1, 1, 1, 0.5f), FadeDuration);
      await ToSignal(tween, Tween.SignalName.Finished);
      if (!IsInsideTree())
         return;

      if (IsInstanceValid(graveyardCard))
         graveyardCard.Modulate = Colors.White;

      if (IsInstanceValid(card))
         card.QueueFree();

      if (!string.IsNullOrEmpty(replacementId) && deck != null)
      {
         await DealCardIntoHand(deck, placeholder, replacementId);
         if (!IsInsideTree())
            return;
      }
      else if (deck != null && IsInstanceValid(placeholder))
      {
         if (placeholder.GetParent() == deck)
            deck.RemoveChild(placeholder);

         placeholder.QueueFree();
      }

      EmitSignal(SignalName.DeckAnimationEnded);
   }

   /// <summary>
   /// Sweeps every played card in the graveyard back onto the draw pile and frees them.
   /// Skipped when only the pile back remains.
   /// </summary>
   private async Task ClearGraveyardAnimated()
   {
      var cards = Graveyard.GetChildren()
         .OfType<Control>()
         .Where(child => child != GraveyardCardBack)
         .ToList();

      if (cards.Count == 0)
      {
         EmitSignal(SignalName.GraveyardAnimationEnded);
         return;
      }

      var target = GraveyardCardBack.GlobalPosition;
      foreach (var played in cards)
      {
         var pos = played.GlobalPosition;
         Graveyard.RemoveChild(played);
         PlaceFlyingCard(played, pos);
      }

      var tween = CreateCardTween();
      tween.SetParallel();
      foreach (var played in cards)
      {
         tween.TweenProperty(played, "global_position", target, GraveyardMoveDuration);
         tween.TweenProperty(played, "modulate", new Color(1, 1, 1, 0), GraveyardFadeDuration);
      }

      await ToSignal(tween, Tween.SignalName.Finished);
      foreach (var played in cards.Where(IsInstanceValid))
         played.QueueFree();

      UpdateGraveyardSize();
      EmitSignal(SignalName.GraveyardAnimationEnded);
   }

   /// <summary>
   /// Deals a face-down dummy from the pile into <paramref name="placeholder"/>'s slot,
   /// then drops that same card into the hand (no second instance, so the landing does not pop).
   /// </summary>
   private async Task DealCardIntoHand(HBoxContainer deck, Control placeholder, string cardId)
   {
      if (!IsInstanceValid(placeholder))
         return;

      var slotIndex = placeholder.GetIndex();
      var slotPos = GetHandSlotPosition(placeholder);
      var faceDown = !ShouldShowHandFaces(deck);

      var dealt = (CardControl)CreateCard(cardId);
      dealt.Preview = true;
      dealt.SetFaceDown(true);
      PlaceFlyingCard(dealt, GraveyardCardBack.GlobalPosition);
      PlayDealSound();

      var tween = CreateCardTween();
      tween.TweenProperty(dealt, "global_position", slotPos, DealDuration);
      await ToSignal(tween, Tween.SignalName.Finished);
      if (!IsInsideTree() || !IsInstanceValid(dealt))
         return;

      if (IsInstanceValid(placeholder))
      {
         slotIndex = placeholder.GetIndex();
         deck.RemoveChild(placeholder);
         placeholder.QueueFree();
      }

      CardAnimLayer.RemoveChild(dealt);
      PrepareCardForHand(dealt);
      deck.AddChild(dealt);

      if (slotIndex >= 0 && slotIndex < deck.GetChildCount() - 1)
         deck.MoveChild(dealt, slotIndex);

      dealt.Preview = false;
      dealt.SetFaceDown(faceDown);
   }

   private CardControl CreateGraveyardCopy(string cardId, bool discarded)
   {
      var copy = (CardControl)CreateCard(cardId);
      copy.Preview = true;
      copy.CustomMinimumSize = _cardSize;
      copy.MouseFilter = MouseFilterEnum.Ignore;
      Graveyard.AddChild(copy);
      copy.BeginPlayAnimation(discarded);

      copy.Modulate = Colors.Transparent;
      UpdateGraveyardSize();

      return copy;
   }

   /// <summary>
   /// World position of graveyard slot <paramref name="index"/> (0 is the draw pile).
   /// Used instead of a freshly added card's <see cref="Control.GlobalPosition"/>, which
   /// still sits on <see cref="GraveyardCardBack"/> until GridContainer finishes sorting.
   /// </summary>
   private Vector2 GetGraveyardSlotPosition(int index)
   {
      var origin = GraveyardCardBack.GlobalPosition;
      var columns = Mathf.Max(1, Graveyard.Columns);
      var hSep = Graveyard.GetThemeConstant("h_separation", "GridContainer");
      var vSep = Graveyard.GetThemeConstant("v_separation", "GridContainer");
      var col = index % columns;
      var row = index / columns;

      return origin + new Vector2(col * (_cardSize.X + hSep), row * (_cardSize.Y + vSep));
   }

   private void UpdateGraveyardSize()
   {
      var columns = Mathf.Max(1, Graveyard.Columns);
      var rows = Mathf.Max(1, Mathf.CeilToInt(Graveyard.GetChildCount() / (float)columns));
      var vSep = Graveyard.GetThemeConstant("v_separation", "GridContainer");
      var height = rows * _cardSize.Y + Mathf.Max(0, rows - 1) * vSep;
      var size = Graveyard.Size;
      size.Y = height;
      Graveyard.Size = size;
   }

   private static Control CreateHandPlaceholder()
   {
      return new Control
      {
         CustomMinimumSize = _cardSize,
         SizeFlagsVertical = SizeFlags.ShrinkCenter,
         MouseFilter = MouseFilterEnum.Ignore,
         Modulate = Colors.Transparent
      };
   }

   /// <summary>
   /// Top-left of where a 180px card sits in a hand slot.
   /// The deck HBox is 200px tall and cards use <see cref="Control.SizeFlags.ShrinkCenter"/>,
   /// so a stretched placeholder's origin is above the real card.
   /// </summary>
   private static Vector2 GetHandSlotPosition(Control slot)
   {
      var rect = slot.GetGlobalRect();
      var position = rect.Position;
      if (rect.Size.Y > _cardSize.Y)
         position.Y += (rect.Size.Y - _cardSize.Y) / 2f;

      return position;
   }

   private static void PrepareCardForHand(CardControl card)
   {
      card.ZIndex = 0;
      card.MouseFilter = MouseFilterEnum.Stop;
      card.SetAnchorsPreset(LayoutPreset.TopLeft);
      card.CustomMinimumSize = _cardSize;
      card.Position = Vector2.Zero;
   }

   private void PlaceFlyingCard(Control card, Vector2 globalPosition)
   {
      CardAnimLayer.AddChild(card);
      card.MouseFilter = MouseFilterEnum.Ignore;
      card.SetAnchorsPreset(LayoutPreset.TopLeft);
      card.CustomMinimumSize = _cardSize;
      card.Size = _cardSize;
      card.GlobalPosition = globalPosition;
      card.ZIndex = 20;
   }

   private Vector2 GetPlayCenterPosition()
   {
      return GetViewportRect().Size / 2f - _cardSize / 2f + _playCenterOffset;
   }

   private bool ShouldShowHandFaces(HBoxContainer deck)
   {
      foreach (var (playerId, hand) in _handByPlayer)
      {
         if (hand == deck)
            return ShouldShowHandFaces(playerId);
      }

      return deck == RedDeck;
   }

   private Tween CreateCardTween()
   {
      var tween = CreateTween();
      tween.SetTrans(Tween.TransitionType.Expo);
      tween.SetEase(Tween.EaseType.InOut);
      return tween;
   }

   private void PlayDealSound() => PlaySfx(DealSoundPath);
}
