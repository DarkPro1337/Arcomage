using Godot;

namespace Arcomage.Scripts.UI
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
         var nextButton = GetNode<Button>("Next");

         authorLabel.Connect("gui_input",new Callable(this,nameof(OnAuthorGuiInput)));
         authorLabel.Connect("mouse_entered",new Callable(this,nameof(OnAuthorMouseEntered)));
         authorLabel.Connect("mouse_exited",new Callable(this,nameof(OnAuthorMouseExited)));
         engineButton.Connect("pressed",new Callable(this,nameof(OnEnginePressed)));
         githubButton.Connect("pressed",new Callable(this,nameof(OnGithubPressed)));
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
   }
}