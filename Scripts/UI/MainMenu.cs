using Arcomage.Scripts.Core;
using Arcomage.Scripts.Logging;
using Godot;

namespace Arcomage.Scripts.UI;

public partial class MainMenu : Control
{
   private static readonly Logger _Logger = Logger.GetOrCreateLogger("MainMenu");

   private Control Settings => GetNode<Control>("Settings");
   private Control NetworkSetup => GetNode<Control>("NetworkSetup");
   private AnimationPlayer StartupAnim => GetNode<AnimationPlayer>("StartupAnim");
   private AnimationPlayer MenuAnim => GetNode<AnimationPlayer>("MenuAnim");
   private Control Credits => GetNode<Control>("Credits");
   private Label Version => GetNode<Label>("Logo/Ver");
   private Label BuildNumber => GetNode<Label>("BuildNumber");

   public override void _EnterTree()
   {
      base._EnterTree();

      var newGameButton = GetNode<Button>("MenuGrid/NewGame");
      var multiplayerGameButton = GetNode<Button>("MenuGrid/MultiplayerGame");
      var settingsButton = GetNode<Button>("MenuGrid/Settings");
      var creditsButton = GetNode<Button>("MenuGrid/Credits");
      var devToolsButton = GetNode<Button>("MenuGrid/DevTools");
      var exitButton = GetNode<Button>("MenuGrid/Exit");

      newGameButton.Pressed += OnNewGamePressed;
      multiplayerGameButton.Pressed += OnMultiplayerGamePressed;
      settingsButton.Pressed += OnSettingsPressed;
      creditsButton.Pressed += OnCreditsPressed;
      devToolsButton.Pressed += OnDevToolsPressed;
      exitButton.Pressed += OnExitPressed;
        
      if (OS.IsDebugBuild())
      {
         devToolsButton.Visible = true;
         devToolsButton.Disabled = false;
      }
      else
      {
         devToolsButton.Visible = false;
         devToolsButton.Disabled = true;
      }
   }

   public override void _Ready()
   {
      Version.Text = $"{ProjectSettings.GetSetting("application/config/version")}";
      BuildNumber.Text = $"Build: {Global.BuildNumber}";
      if (OS.IsDebugBuild()) 
         BuildNumber.Text += "-dev";

      GetTree().Paused = false;
      _Logger.Debug("Main menu loaded.");
      ReadCommandLine();
   }

   private async void OnNewGamePressed()
   {
      MenuAnim.Play("fade_out");
      await ToSignal(MenuAnim, "animation_finished");
      GetTree().ChangeSceneToFile("res://Scenes/Gameplay/Table.tscn");
   }

   private void OnSettingsPressed()
   {
      Settings.Show();
      MenuAnim.Play("settings_show");
   }

   private void OnMultiplayerGamePressed() => NetworkSetup.Show();
   private void OnCreditsPressed() => Credits.Show();
   private void OnDevToolsPressed() => GetTree().ChangeSceneDeferred("res://Scenes/UI/Debug/CardsViewer.tscn");
   private void OnExitPressed() => GetTree().Quit();

   private void ReadCommandLine()
   {
      var args = Global.GetCommandLineArgs();
      if (!args.TryGetValue("playerName", out var name))
         return;

      _Logger.Debug("Player name from command line: " + name);
      Config.Settings.Nickname = name;
      Settings.Call("UpdateControls");
      DisplayServer.WindowSetTitle($"Arcomage - {name}");
   }
}