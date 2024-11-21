using Godot;

namespace Arcomage.Scripts
{
   public partial class Info : Control
   {
      private VBoxContainer OriginalContainer => GetNode<VBoxContainer>("OriginalInfo");
      private VBoxContainer RemakeContainer => GetNode<VBoxContainer>("RemakeInfo");
      private VBoxContainer TranslationContainer => GetNode<VBoxContainer>("TranslationInfo");
      private Label AuthorLabel => GetNode<Label>("RemakeInfo/Text/Author");

      public override void _EnterTree()
      {
         base._EnterTree();

         var authorLabel = GetNode<Label>("RemakeInfo/Text/Author");
         var engineButton = GetNode<TextureButton>("RemakeInfo/Logos/Engine");
         var githubButton = GetNode<TextureButton>("RemakeInfo/Logos/GitHub");
         var trelloButton = GetNode<TextureButton>("RemakeInfo/Logos/Trello");
         var itchButton = GetNode<TextureButton>("RemakeInfo/Logos/Itch");
         var gameJoltButton = GetNode<TextureButton>("RemakeInfo/Logos/GameJolt");
         var bmcButton = GetNode<TextureButton>("RemakeInfo/Logos/Bmc");
         var patronButton = GetNode<TextureButton>("RemakeInfo/Logos/Patreon");
         var kofiButton = GetNode<TextureButton>("RemakeInfo/Logos/Kofi");
         var nextButton = GetNode<Button>("Next");

         authorLabel.Connect("gui_input",new Callable(this,nameof(OnAuthorGuiInput)));
         authorLabel.Connect("mouse_entered",new Callable(this,nameof(OnAuthorMouseEntered)));
         authorLabel.Connect("mouse_exited",new Callable(this,nameof(OnAuthorMouseExited)));
         engineButton.Connect("pressed",new Callable(this,nameof(OnEnginePressed)));
         githubButton.Connect("pressed",new Callable(this,nameof(OnGithubPressed)));
         trelloButton.Connect("pressed",new Callable(this,nameof(OnTrelloPressed)));
         itchButton.Connect("pressed",new Callable(this,nameof(OnItchPressed)));
         gameJoltButton.Connect("pressed",new Callable(this,nameof(OnGameJoltPressed)));
         bmcButton.Connect("pressed",new Callable(this,nameof(OnBmcPressed)));
         patronButton.Connect("pressed",new Callable(this,nameof(OnPatronPressed)));
         kofiButton.Connect("pressed",new Callable(this,nameof(OnKofiPressed)));
         nextButton.Connect("pressed",new Callable(this,nameof(OnNextPressed)));
      }

      public override void _Ready()
      {
         OriginalContainer.Show();
         RemakeContainer.Hide();
         TranslationContainer.Hide();
      }

      private void OnNextPressed()
      {
         if (OriginalContainer.Visible)
         {
            OriginalContainer.Hide();
            RemakeContainer.Show();
         }
         else if (RemakeContainer.Visible)
         {
            TranslationContainer.Show();
            RemakeContainer.Hide();
         }
         else if (TranslationContainer.Visible)
         {
            OriginalContainer.Show();
            TranslationContainer.Hide();
            Hide();
         }
      }
    
      private void OnAuthorGuiInput(InputEvent @event)
      {
         if (@event is InputEventMouseButton btn && btn.IsPressed() && btn.ButtonIndex == MouseButton.Left)
            OS.ShellOpen("https://darkpro1337.github.io");
      }

      private void OnAuthorMouseEntered() => AuthorLabel.Modulate = new Color("#ff993f");
      private void OnAuthorMouseExited() => AuthorLabel.Modulate = new Color("#ffffff");
    
      private void OnEnginePressed() => OS.ShellOpen("https://godotengine.org");
      private void OnGithubPressed() => OS.ShellOpen("https://github.com/DarkPro1337/arcomage");
      private void OnTrelloPressed() => OS.ShellOpen("https://trello.com/b/nQuzlNk5/arcomage-remake");
      private void OnItchPressed() => OS.ShellOpen("https://darkpro1337.itch.io/arcomage");
      private void OnGameJoltPressed() => OS.ShellOpen("https://gamejolt.com/games/arcomage/537808");
      private void OnBmcPressed() => OS.ShellOpen("https://www.buymeacoffee.com/darkpro1337");
      private void OnPatronPressed() => OS.ShellOpen("https://patreon.com/darkpro1337");
      private void OnKofiPressed() => OS.ShellOpen("https://ko-fi.com/darkpro1337");
   }
}