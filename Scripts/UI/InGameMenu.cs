using Arcomage.Scripts.Core;
using Godot;

namespace Arcomage.Scripts.UI;

public partial class InGameMenu : Control
{
   private Settings Settings => GetNode<Settings>("Settings");
   private Button ResumeButton => GetNode<Button>("Container/Resume");
   private Button SettingsButton => GetNode<Button>("Container/Settings");
   private Button StatsButton => GetNode<Button>("Container/Stats");
   private Button ExitButton => GetNode<Button>("Container/Exit");

   public override void _Ready()
   {
      TopLevel = true;

      ResumeButton.Pressed += ResumeButtonOnPressed;
      StatsButton.Pressed += StatsButtonOnPressed;
      SettingsButton.Pressed += SettingsButtonOnPressed;
      ExitButton.Pressed += ExitButtonOnPressed;
   }

   private void ResumeButtonOnPressed()
   {
      Hide();
      Global.Table.GetTree().Paused = false;
   }

   private void StatsButtonOnPressed()
   {
   }

   private void SettingsButtonOnPressed() => Settings.Show();
   private void ExitButtonOnPressed() => GetTree().ChangeSceneToFile("res://Scenes/Main/MainMenu.tscn");
}