using Godot;

namespace Arcomage.Scripts
{
	public partial class MainMenu : Control
	{
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
			var leaderboardButton = GetNode<Button>("MenuGrid/Leaderboard");
			var creditsButton = GetNode<Button>("MenuGrid/Credits");
			var devToolsButton = GetNode<Button>("MenuGrid/DevTools");
			var exitButton = GetNode<Button>("MenuGrid/Exit");

			newGameButton.Pressed += OnNewGamePressed;
			multiplayerGameButton.Pressed += OnMultiplayerGamePressed;
			settingsButton.Pressed += OnSettingsPressed;
			leaderboardButton.Pressed += OnLeaderboardPressed;
			creditsButton.Pressed += OnCreditsPressed;
			devToolsButton.Pressed += OnDevToolsPressed;
			exitButton.Pressed += OnExitPressed;
		}

		public override void _Ready()
		{
			Version.Text = $"v. {ProjectSettings.GetSetting("application/config/version")}";
			BuildNumber.Text = $"Build: {Global.BuildNumber}";
			GetTree().Paused = false;
			Global.Log("Main menu loaded.");
		}

		private async void OnNewGamePressed()
		{
			MenuAnim.Play("fade_out");
			await ToSignal(MenuAnim, "animation_finished");
			GetTree().ChangeSceneToFile("res://Scenes/Table.tscn");
		}

		private void OnSettingsPressed()
		{
			Settings.Show();
			MenuAnim.Play("settings_show");
		}
		
		private void OnMultiplayerGamePressed() => NetworkSetup.Show();
		private void OnLeaderboardPressed() => Alert(Tr("WORK_IN_PROGRESS"), "Oops");
		private void OnCreditsPressed() => Credits.Show();
		private void OnDevToolsPressed() => GetTree().ChangeSceneToFile("res://Scenes/CardsViewer.tscn");
		private void OnExitPressed() => GetTree().Quit();

		private void Alert(string text, string title = "Alert")
		{
			var dialog = new AcceptDialog();
			dialog.DialogText = text;
			dialog.Title = title;
			dialog.Connect("modal_connected",new Callable(dialog,"queue_free"));
			AddChild(dialog);
			dialog.PopupCentered();
		}
	}
}
