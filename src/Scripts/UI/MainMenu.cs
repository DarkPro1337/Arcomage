namespace Arcomage.UI;

public partial class MainMenu : Control
{
   private static readonly Logger _logger = Logger.GetOrCreateLogger("MainMenu");

   private Settings _settingsMenu;
   private Control _networkSetup;
   private AnimationPlayer _startupAnim;
   private AnimationPlayer _menuAnim;
   private Control _credits;
   private Label _version;
   private Label _buildNumber;

   private Settings SettingsMenu => _settingsMenu ??= GetNode<Settings>("Settings");
   private Control NetworkSetup => _networkSetup ??= GetNode<Control>("NetworkSetup");
   private AnimationPlayer StartupAnim => _startupAnim ??= GetNode<AnimationPlayer>("StartupAnim");
   private AnimationPlayer MenuAnim => _menuAnim ??= GetNode<AnimationPlayer>("MenuAnim");
   private Control Credits => _credits ??= GetNode<Control>("Credits");
   private Label Version => _version ??= GetNode<Label>("Logo/Ver");
   private Label BuildNumber => _buildNumber ??= GetNode<Label>("BuildNumber");

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
      _logger.Debug("Main menu loaded.");
      ReadCommandLine();
   }

   private void OnNewGamePressed() => _ = FadeThenStartGame();

   private async Task FadeThenStartGame()
   {
      try
      {
         MenuAnim.Play("fade_out");
         await ToSignal(MenuAnim, AnimationMixer.SignalName.AnimationFinished);
         if (!IsInsideTree())
            return;

         GetTree().ChangeSceneToFile("res://Scenes/Gameplay/Table.tscn");
      }
      catch (Exception ex)
      {
         _logger.Error(ex, "Failed to start a new game");
      }
   }

   private void OnSettingsPressed()
   {
      SettingsMenu.Show();
      MenuAnim.Play("settings_show");
   }

   private void OnMultiplayerGamePressed() => NetworkSetup.Show();
   private void OnCreditsPressed() => Credits.Show();
   private void OnDevToolsPressed() => GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, "res://Scenes/UI/Debug/CardsViewer.tscn");
   private void OnExitPressed() => GetTree().Quit();

   private void ReadCommandLine()
   {
      var args = Global.GetCommandLineArgs();
      if (!args.TryGetValue("playerName", out var name) || string.IsNullOrWhiteSpace(name))
         return;

      SettingsMenu.UpdateControls();
      DisplayServer.WindowSetTitle($"Arcomage - {name}");
   }
}
