namespace Arcomage.UI;

public partial class Info : Control
{
   private VBoxContainer _originalContainer;
   private VBoxContainer _remakeContainer;
   private VBoxContainer _translationContainer;
   private Label _authorLabel;
   private TextureButton _engineButton;
   private TextureButton _githubButton;
   private Button _nextButton;

   private VBoxContainer OriginalContainer => _originalContainer ??= GetNode<VBoxContainer>("OriginalInfo");
   private VBoxContainer RemakeContainer => _remakeContainer ??= GetNode<VBoxContainer>("RemakeInfo");
   private VBoxContainer TranslationContainer => _translationContainer ??= GetNode<VBoxContainer>("TranslationInfo");
   private Label AuthorLabel => _authorLabel ??= GetNode<Label>("RemakeInfo/Text/Author");
   private TextureButton EngineButton => _engineButton ??= GetNode<TextureButton>("RemakeInfo/Logos/Engine");
   private TextureButton GithubButton => _githubButton ??= GetNode<TextureButton>("RemakeInfo/Logos/GitHub");
   private Button NextButton => _nextButton ??= GetNode<Button>("Next");

   public override void _EnterTree()
   {
      base._EnterTree();

      AuthorLabel.GuiInput += OnAuthorGuiInput;
      AuthorLabel.MouseEntered += OnAuthorMouseEntered;
      AuthorLabel.MouseExited += OnAuthorMouseExited;
      EngineButton.Pressed += OnEnginePressed;
      GithubButton.Pressed += OnGithubPressed;
      NextButton.Pressed += OnNextPressed;
   }

   public override void _ExitTree()
   {
      if (_authorLabel != null)
      {
         _authorLabel.GuiInput -= OnAuthorGuiInput;
         _authorLabel.MouseEntered -= OnAuthorMouseEntered;
         _authorLabel.MouseExited -= OnAuthorMouseExited;
      }

      if (_engineButton != null)
         _engineButton.Pressed -= OnEnginePressed;
      if (_githubButton != null)
         _githubButton.Pressed -= OnGithubPressed;
      if (_nextButton != null)
         _nextButton.Pressed -= OnNextPressed;

      base._ExitTree();
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
