using Arcomage.Core;
using Godot;

namespace Arcomage.UI;

public partial class InGameMenu : Control
{
   private Settings _settings;
   private Button _resumeButton;
   private Button _settingsButton;
   private Button _statsButton;
   private Button _exitButton;
   private Label _pauseLabel;

   private Settings Settings => _settings ??= GetNode<Settings>("Settings");
   private Button ResumeButton => _resumeButton ??= GetNode<Button>("Container/Resume");
   private Button SettingsButton => _settingsButton ??= GetNode<Button>("Container/Settings");
   private Button StatsButton => _statsButton ??= GetNode<Button>("Container/Stats");
   private Button ExitButton => _exitButton ??= GetNode<Button>("Container/Exit");
   private Label PauseLabel => _pauseLabel ??= GetNode<Label>("Container/PauseLabel");

   public override void _Ready()
   {
      TopLevel = true;

      ResumeButton.Pressed += ResumeButtonOnPressed;
      StatsButton.Pressed += StatsButtonOnPressed;
      SettingsButton.Pressed += SettingsButtonOnPressed;
      ExitButton.Pressed += ExitButtonOnPressed;
   }

   public void Open(bool pauseTree)
   {
      PauseLabel.Visible = pauseTree;
      Show();

      if (pauseTree)
         GetTree().Paused = true;
   }

   public void Close()
   {
      Hide();
      GetTree().Paused = false;
   }

   private void ResumeButtonOnPressed() => Close();

   private void StatsButtonOnPressed()
   {
   }

   private void SettingsButtonOnPressed() => Settings.Show();

   private async void ExitButtonOnPressed()
   {
      GetTree().Paused = false;
      if (Global.Table != null)
      {
         await Global.Table.ReturnToMenu();
         return;
      }

      if (Global.Online != null)
         await Global.Online.LeaveMatch();

      GetTree().ChangeSceneToFile("res://Scenes/Main/MainMenu.tscn");
   }
}
