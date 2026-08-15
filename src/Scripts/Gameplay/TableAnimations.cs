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
   private static readonly Vector2 CardSize = new(135, 180);
   private static readonly Vector2 PlayCenterOffset = new(0, -50);

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
   private async Task AnimateCardPlay(
      HBoxContainer deck,
      CardControl card,
      bool discarded,
      string replacementId)
   {
      var slotIndex = card.GetIndex();
      var startPos = card.GlobalPosition;
      card.BeginPlayAnimation(discarded);

      var graveyardCard = CreateGraveyardCopy(card.CardId, discarded);
      await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
      if (!IsInsideTree() || !GodotObject.IsInstanceValid(graveyardCard))
         return;

      var graveyardPos = graveyardCard.GlobalPosition;
      var placeholder = CreateHandPlaceholder();
      deck.AddChild(placeholder);
      deck.MoveChild(placeholder, slotIndex);
      deck.RemoveChild(card);
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

      if (GodotObject.IsInstanceValid(graveyardCard))
         graveyardCard.Modulate = Colors.White;

      if (GodotObject.IsInstanceValid(card))
         card.QueueFree();

      if (!string.IsNullOrEmpty(replacementId))
      {
         await DealCardIntoHand(deck, placeholder, replacementId);
         if (!IsInsideTree())
            return;
      }
      else if (GodotObject.IsInstanceValid(placeholder))
      {
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
      foreach (var played in cards)
      {
         if (GodotObject.IsInstanceValid(played))
            played.QueueFree();
      }

      EmitSignal(SignalName.GraveyardAnimationEnded);
   }

   /// <summary>
   /// Deals a face-down dummy from the pile into <paramref name="placeholder"/>'s slot,
   /// then swaps it for the real card.
   /// </summary>
   private async Task DealCardIntoHand(HBoxContainer deck, Control placeholder, string cardId)
   {
      if (!GodotObject.IsInstanceValid(placeholder))
         return;

      var slotIndex = placeholder.GetIndex();
      var slotPos = placeholder.GlobalPosition;
      var faceDown = !ShouldShowHandFaces(deck);

      var dummy = (CardControl)CreateCard(cardId);
      dummy.Preview = true;
      dummy.SetFaceDown(true);
      PlaceFlyingCard(dummy, GraveyardCardBack.GlobalPosition);
      PlayDealSound();

      var tween = CreateCardTween();
      tween.TweenProperty(dummy, "global_position", slotPos, DealDuration);
      await ToSignal(tween, Tween.SignalName.Finished);
      if (!IsInsideTree())
         return;

      if (GodotObject.IsInstanceValid(placeholder))
      {
         deck.RemoveChild(placeholder);
         placeholder.QueueFree();
      }

      var dealt = (CardControl)CreateCard(cardId);
      deck.AddChild(dealt);
      if (slotIndex >= 0 && slotIndex < deck.GetChildCount() - 1)
         deck.MoveChild(dealt, slotIndex);
      dealt.SetFaceDown(faceDown);

      if (GodotObject.IsInstanceValid(dummy))
         dummy.QueueFree();
   }

   private CardControl CreateGraveyardCopy(string cardId, bool discarded)
   {
      var copy = (CardControl)CreateCard(cardId);
      copy.Preview = true;
      copy.Modulate = Colors.Transparent;
      Graveyard.AddChild(copy);
      copy.BeginPlayAnimation(discarded);
      return copy;
   }

   private static Control CreateHandPlaceholder()
   {
      return new Control
      {
         CustomMinimumSize = CardSize,
         Size = CardSize,
         MouseFilter = MouseFilterEnum.Ignore,
         Modulate = Colors.Transparent
      };
   }

   private void PlaceFlyingCard(Control card, Vector2 globalPosition)
   {
      CardAnimLayer.AddChild(card);
      card.MouseFilter = MouseFilterEnum.Ignore;
      card.SetAnchorsPreset(LayoutPreset.TopLeft);
      card.CustomMinimumSize = CardSize;
      card.Size = CardSize;
      card.GlobalPosition = globalPosition;
      card.ZIndex = 20;
   }

   private Vector2 GetPlayCenterPosition()
   {
      return GetViewportRect().Size / 2f - CardSize / 2f + PlayCenterOffset;
   }

   private bool ShouldShowHandFaces(HBoxContainer deck)
   {
      var localId = Multiplayer.GetUniqueId();
      return deck == RedDeck ? _redPlayerId == localId : _bluePlayerId == localId;
   }

   private Tween CreateCardTween()
   {
      var tween = CreateTween();
      tween.SetTrans(Tween.TransitionType.Expo);
      tween.SetEase(Tween.EaseType.InOut);
      return tween;
   }

   private void PlayDealSound()
   {
      var player = new AudioStreamPlayer
      {
         Stream = GD.Load<AudioStream>("res://Sounds/deal.ogg"),
         Bus = "Sounds"
      };
      AddChild(player);
      player.Finished += player.QueueFree;
      player.Play();
   }
}
