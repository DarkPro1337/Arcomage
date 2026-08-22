namespace Arcomage.Gameplay;

internal sealed class ResourceHud
{
   public Panel Panel { get; init; }
   public Label PerTurn { get; init; }
   public Label Total { get; init; }
   public Panel AltPanel { get; init; }
   public Label AltPerTurn { get; init; }
   public Label AltTotal { get; init; }

   public void Set(string perTurn, string total)
   {
      PerTurn?.Text = perTurn;
      AltPerTurn?.Text = perTurn;
      Total?.Text = total;
      AltTotal?.Text = total;
   }

   public void ShowPrimary(bool showPrimary)
   {
      if (showPrimary)
      {
         Panel?.Show();
         AltPanel?.Hide();
      }
      else
      {
         Panel?.Hide();
         AltPanel?.Show();
      }
   }

   public Control VisiblePerTurn => VisibleOf(PerTurn, AltPerTurn);
   public Control VisibleTotal => VisibleOf(Total, AltTotal);

   private static Control VisibleOf(Control primary, Control alt)
   {
      if (primary != null && primary.IsVisibleInTree())
         return primary;

      if (alt != null && alt.IsVisibleInTree())
         return alt;

      return primary ?? alt;
   }
}
