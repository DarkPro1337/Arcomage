using System.Globalization;
using Godot;

namespace Arcomage.Scripts;

public enum BusType
{
   Master,
   Music,
   Sound
}

public partial class Settings : Control
{
   private static readonly Logger _Logger = Logger.GetOrCreateLogger("Settings");

   private readonly CultureInfo _invariantCulture = CultureInfo.InvariantCulture;

   #region Control vars

   private AnimationPlayer Anim => GetNode<AnimationPlayer>("AnimationPlayer");
   private Button Reset => GetNode<Button>("Reset");
   private Button Close => GetNode<Button>("Close");
   private TabContainer Tab => GetNode<TabContainer>("Tab");

   private Button WindowSettingsButton => GetNode<Button>("Options/Grid/WindowSettings");
   private HBoxContainer Fullscreen => GetNode<HBoxContainer>("Tab/Graphics/Container/Fullscreen");
   private CheckButton FullscreenButton => GetNode<CheckButton>("Tab/Graphics/Container/Fullscreen/Toggle");
   private CheckButton BorderlessButton => GetNode<CheckButton>("Tab/Graphics/Container/Borderless/Toggle");
   private HBoxContainer WindowResolution => GetNode<HBoxContainer>("Tab/Graphics/Container/WindowResolution");
   private LineEdit WindowWidthEdit => GetNode<LineEdit>("Tab/Graphics/Container/WindowResolution/Width");
   private LineEdit WindowHeightEdit => GetNode<LineEdit>("Tab/Graphics/Container/WindowResolution/Height");

   private Button WindowResolutionApplyButton =>
      GetNode<Button>("Tab/Graphics/Container/WindowResolution/ApplyButton");

   private CheckButton VsyncButton => GetNode<CheckButton>("Tab/Graphics/Container/Vsync/Toggle");
   private HBoxContainer IntroSkip => GetNode<HBoxContainer>("Tab/Graphics/Container/IntroSkip");
   private CheckButton IntroSkipButton => GetNode<CheckButton>("Tab/Graphics/Container/IntroSkip/Toggle");

   private Button SoundSettingsButton => GetNode<Button>("Options/Grid/SoundSettings");
   private HSlider MasterVolume => GetNode<HSlider>("Tab/Sound/Container/Master/Slider");
   private HSlider MusicVolume => GetNode<HSlider>("Tab/Sound/Container/Music/Slider");
   private HSlider SoundVolume => GetNode<HSlider>("Tab/Sound/Container/Sounds/Slider");
   private CheckBox MuteSound => GetNode<CheckBox>("Tab/Sound/Container/Mute/Toggle");

   private Button StartingConditionsButton => GetNode<Button>("Options/Grid/StartingConditions");

   private CheckBox SingleClickButton => GetNode<CheckBox>("Tab/StartingConditions/Container/Main/Gameplay/SingleClick/Toggle");
   private SpinBox TowerLevels => GetNode<SpinBox>("Tab/StartingConditions/Container/Main/TowersWalls/TowerLevels/Level");
   private SpinBox WallLevels => GetNode<SpinBox>("Tab/StartingConditions/Container/Main/TowersWalls/WallLevels/Level");
   private SpinBox QuarryLevels => GetNode<SpinBox>("Tab/StartingConditions/Container/ResourceGeneration/Generators/Quarry/Level");
   private SpinBox BrickQuantity => GetNode<SpinBox>("Tab/StartingConditions/Container/ResourceGeneration/Resources/Bricks/Level");
   private SpinBox MagicLevels => GetNode<SpinBox>("Tab/StartingConditions/Container/ResourceGeneration/Generators/Magic/Level");
   private SpinBox GemQuantity => GetNode<SpinBox>("Tab/StartingConditions/Container/ResourceGeneration/Resources/Gems/Level");
   private SpinBox DungeonLevels => GetNode<SpinBox>("Tab/StartingConditions/Container/ResourceGeneration/Generators/Dungeon/Level");
   private SpinBox RecruitQuantity => GetNode<SpinBox>("Tab/StartingConditions/Container/ResourceGeneration/Resources/Recruits/Level");

   private Button PlayConditionsButton => GetNode<Button>("Options/Grid/PlayConditions");
   private SpinBox AutoBricks => GetNode<SpinBox>("Tab/PlayConditions/Container/AutoGetter/Bricks/Level");
   private SpinBox AutoGems => GetNode<SpinBox>("Tab/PlayConditions/Container/AutoGetter/Gems/Level");
   private SpinBox AutoRecruits => GetNode<SpinBox>("Tab/PlayConditions/Container/AutoGetter/Recruits/Level");
   private SpinBox CardsInHand => GetNode<SpinBox>("Tab/PlayConditions/Container/Other/CardsInHand/Level");
   private OptionButton AiMode => GetNode<OptionButton>("Tab/PlayConditions/Container/Other/Ai/Mode");

   private Button VictoryConditionsButton => GetNode<Button>("Options/Grid/VictoryConditions");
   private SpinBox TowerVictory => GetNode<SpinBox>("Tab/VictoryConditions/Container/TowerVictory/Level");
   private SpinBox ResourceVictory => GetNode<SpinBox>("Tab/VictoryConditions/Container/ResourceVictory/Level");

   private Button TavernPresetsButton => GetNode<Button>("Options/Grid/TavernPresets");
   private OptionButton TavernPreset => GetNode<OptionButton>("Tab/TavernPresets/Container/Preset/Option");

   private Button LanguageSettingsButton => GetNode<Button>("Options/Grid/LanguageSettings");
   private OptionButton Language => GetNode<OptionButton>("Tab/LanguageSettings/Container/Language/Option");
   private Label TranslationErrors => GetNode<Label>("Tab/LanguageSettings/Container/TranslationErrors");

   private Button PlayerSettingsButton => GetNode<Button>("Options/Grid/PlayerSettings");
   private LineEdit Nickname => GetNode<LineEdit>("Tab/PlayerSettings/Container/Nickname/Edit");

   #endregion

   public override void _EnterTree()
   {
      base._EnterTree();

      Close.Pressed += OnClosePressed;
      Reset.Pressed += OnResetPressed;

      WindowSettingsButton.Pressed += OnWindowSettingsPressed;
      SoundSettingsButton.Pressed += OnSoundSettingsPressed;
      StartingConditionsButton.Pressed += OnStartingConditionsPressed;
      PlayConditionsButton.Pressed += OnPlayConditionsPressed;
      VictoryConditionsButton.Pressed += OnVictoryConditionsPressed;
      TavernPresetsButton.Pressed += OnTavernPresetsPressed;
      LanguageSettingsButton.Pressed += OnLanguageSettingsPressed;
      PlayerSettingsButton.Pressed += OnPlayerSettingsPressed;

      FullscreenButton.Toggled += OnFullscreenButtonToggled;
      BorderlessButton.Toggled += OnBorderlessButtonToggled;
      WindowResolutionApplyButton.Pressed += OnWindowResolutionApplyPressed;
      VsyncButton.Toggled += OnVsyncButtonToggled;
      IntroSkipButton.Toggled += OnIntroSkipButtonToggled;

      MasterVolume.ValueChanged += OnMasterVolumeValueChanged;
      MusicVolume.ValueChanged += OnMusicVolumeValueChanged;
      SoundVolume.ValueChanged += OnSoundVolumeValueChanged;
      MuteSound.Toggled += OnMuteSoundToggled;

      SingleClickButton.Toggled += OnSingleClickButtonToggled;
      TowerLevels.ValueChanged += OnTowerLevelsValueChanged;
      WallLevels.ValueChanged += OnWallLevelsValueChanged;
      QuarryLevels.ValueChanged += OnQuarryLevelsValueChanged;
      BrickQuantity.ValueChanged += OnBrickQuantityValueChanged;
      MagicLevels.ValueChanged += OnMagicLevelsValueChanged;
      GemQuantity.ValueChanged += OnGemQuantityValueChanged;
      DungeonLevels.ValueChanged += OnDungeonLevelsValueChanged;
      RecruitQuantity.ValueChanged += OnRecruitQuantityValueChanged;

      AutoBricks.ValueChanged += OnAutoBricksValueChanged;
      AutoGems.ValueChanged += OnAutoGemsValueChanged;
      AutoRecruits.ValueChanged += OnAutoRecruitsValueChanged;
      CardsInHand.ValueChanged += OnCardsInHandValueChanged;
      AiMode.ItemSelected += OnAiModeChanged;

      TowerVictory.ValueChanged += OnTowerVictoryValueChanged;
      ResourceVictory.ValueChanged += OnResourceVictoryValueChanged;

      TavernPreset.ItemSelected += OnTavernPresetChanged;
      Language.ItemSelected += OnLanguageChanged;
      Nickname.TextChanged += OnNicknameChanged;
        
      VisibilityChanged += SettingsVisibilityChanged;
   }

   private void SettingsVisibilityChanged()
   {
      if (!Visible)
         return;

      var parentName = GetParent().Name.ToString();
      if (string.IsNullOrEmpty(parentName))
      {
         _Logger.Debug("Settings loaded from source with empty name");
         return;
      }
        
      if (parentName == "InGameMenu")
      {
         _Logger.Debug("Settings loaded from InGameMenu");
         StartingConditionsButton.Hide();
         PlayConditionsButton.Hide();
         VictoryConditionsButton.Hide();
         TavernPresetsButton.Hide();
         PlayerSettingsButton.Hide();
         LanguageSettingsButton.Hide();
         IntroSkip.Hide();
         Reset.Hide();
      }
      else if (parentName != "MainMenu")
      {
         _Logger.Debug("Settings loaded from unknown source and not shown");
         Hide();
      }
   }

   public override void _Ready()
   {
      UpdateControls();
      UpdateLocale();
   }

   private async void OnClosePressed()
   {
      Config.SaveSettings();
      Anim.Play("hide");
      await ToSignal(Anim, "animation_finished");
      Hide();
   }

   private void OnResetPressed()
   {
      Config.Settings = new GameSettings();
      Config.SaveSettings();
      UpdateControls();
      UpdateLocale();
   }

   private void UpdateControls()
   {
      FullscreenButton.ButtonPressed = Config.Settings.Fullscreen;
      BorderlessButton.ButtonPressed = Config.Settings.Borderless;
      WindowWidthEdit.Text = Config.Settings.WindowWidth.ToString(_invariantCulture);
      WindowHeightEdit.Text = Config.Settings.WindowHeight.ToString(_invariantCulture);
      VsyncButton.ButtonPressed = Config.Settings.Vsync;
      IntroSkipButton.ButtonPressed = Config.Settings.IntroSkip;

      MasterVolume.Value = Config.Settings.MasterVolume;
      MusicVolume.Value = Config.Settings.MusicVolume;
      SoundVolume.Value = Config.Settings.SoundVolume;
      MuteSound.ButtonPressed = Config.Settings.MuteSound;

      SingleClickButton.ButtonPressed = Config.Settings.SingleClick;
      TowerLevels.Value = Config.Settings.TowerLevels;
      WallLevels.Value = Config.Settings.WallLevels;
      QuarryLevels.Value = Config.Settings.QuarryLevels;
      BrickQuantity.Value = Config.Settings.BrickQuantity;
      MagicLevels.Value = Config.Settings.MagicLevels;
      GemQuantity.Value = Config.Settings.GemQuantity;
      DungeonLevels.Value = Config.Settings.DungeonLevels;
      RecruitQuantity.Value = Config.Settings.RecruitQuantity;

      AutoBricks.Value = Config.Settings.AutoBricks;
      AutoGems.Value = Config.Settings.AutoGems;
      AutoRecruits.Value = Config.Settings.AutoRecruits;
      CardsInHand.Value = Config.Settings.CardsInHand;
      AiMode.Selected = (int)Config.Settings.CurrentAiType;

      TowerVictory.Value = Config.Settings.TowerVictory;
      ResourceVictory.Value = Config.Settings.ResourceVictory;

      TavernPreset.Selected = (int)Config.Settings.CurrentTavern;
      Language.Selected = (int)Config.Settings.CurrentLocale;

      Nickname.Text = Config.Settings.Nickname;
   }

   private static void UpdateLocale()
   {
      switch (Config.Settings.CurrentLocale)
      {
         case Locale.En:
            TranslationServer.SetLocale("en");
            break;
         case Locale.Ru:
            TranslationServer.SetLocale("ru");
            break;
         case Locale.Uk:
            TranslationServer.SetLocale("uk");
            break;
         case Locale.Pl:
            TranslationServer.SetLocale("pl");
            break;
         case Locale.Da:
            TranslationServer.SetLocale("da");
            break;
         case Locale.De:
            TranslationServer.SetLocale("de");
            break;
         case Locale.Fr:
            TranslationServer.SetLocale("fr");
            break;
         default:
            _Logger.Warn("Unknown locale - {Locale}. Fallback to English.", Config.Settings.CurrentLocale);
            TranslationServer.SetLocale("en");
            break;
      }

      _Logger.Debug("Loaded locale - {Locale}", Config.Settings.CurrentLocale);
   }

   private void OnWindowSettingsPressed() => Tab.CurrentTab = 0;
   private void OnSoundSettingsPressed() => Tab.CurrentTab = 1;
   private void OnStartingConditionsPressed() => Tab.CurrentTab = 2;
   private void OnPlayConditionsPressed() => Tab.CurrentTab = 3;
   private void OnVictoryConditionsPressed() => Tab.CurrentTab = 4;
   private void OnTavernPresetsPressed() => Tab.CurrentTab = 5;
   private void OnLanguageSettingsPressed() => Tab.CurrentTab = 6;
   private void OnPlayerSettingsPressed() => Tab.CurrentTab = 7;

   private void OnFullscreenButtonToggled(bool toggle)
   {
      Config.Settings.Fullscreen = toggle;
      if (toggle)
      {
         DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
         WindowResolution.Hide();
      }
      else
      {
         DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
         WindowResolution.Show();
      }

      _Logger.Debug("Fullscreen toggled to " + toggle);
   }

   private void OnBorderlessButtonToggled(bool toggle)
   {
      Config.Settings.Borderless = toggle;
      DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, toggle);
      if (toggle && FullscreenButton.ButtonPressed)
         Fullscreen.Hide();
      else
         Fullscreen.Show();

      _Logger.Debug("Borderless toggled to " + toggle);
   }

   private void OnWindowResolutionApplyPressed()
   {
      if (WindowWidthEdit.Text == "" || WindowHeightEdit.Text == "")
      {
         _Logger.Error("Window resolution can't be empty");
         return;
      }

      if (!int.TryParse(WindowWidthEdit.Text, out var width) || !int.TryParse(WindowHeightEdit.Text, out var height))
      {
         _Logger.Error("Window resolution must be a number");
         return;
      }

      if (DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Maximized)
         DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);

      Config.Settings.WindowWidth = width;
      Config.Settings.WindowHeight = height;
      var resolution = new Vector2I(width, height);
      DisplayServer.WindowSetSize(resolution);
      Config.CenterWindow(GetWindow());

      _Logger.Debug("Window resolution applied to " + WindowWidthEdit.Text + "x" + WindowHeightEdit.Text);
   }

   private static void OnVsyncButtonToggled(bool toggle)
   {
      Config.Settings.Vsync = toggle;
      DisplayServer.WindowSetVsyncMode(toggle ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
      _Logger.Debug("Vsync toggled to " + toggle);
   }

   private static void OnIntroSkipButtonToggled(bool toggle) => Config.Settings.IntroSkip = toggle;

   private static void OnMasterVolumeValueChanged(double value)
   {
      Config.Settings.MasterVolume = value;
      AudioServer.SetBusVolumeDb((int)BusType.Master, (float)Mathf.LinearToDb(value));
   }

   private static void OnMusicVolumeValueChanged(double value)
   {
      Config.Settings.MusicVolume = value;
      AudioServer.SetBusVolumeDb((int)BusType.Music, (float)Mathf.LinearToDb(value));
   }

   private static void OnSoundVolumeValueChanged(double value)
   {
      Config.Settings.SoundVolume = value;
      AudioServer.SetBusVolumeDb((int)BusType.Sound, (float)Mathf.LinearToDb(value));
   }

   private static void OnMuteSoundToggled(bool toggle)
   {
      Config.Settings.MuteSound = toggle;
      AudioServer.SetBusMute((int)BusType.Master, toggle);
      AudioServer.SetBusMute((int)BusType.Music, toggle);
      AudioServer.SetBusMute((int)BusType.Sound, toggle);
   }

   private static void OnSingleClickButtonToggled(bool toggle) => Config.Settings.SingleClick = toggle;
   private static void OnTowerLevelsValueChanged(double value) => Config.Settings.TowerLevels = (int)value;
   private static void OnWallLevelsValueChanged(double value) => Config.Settings.WallLevels = (int)value;
   private static void OnQuarryLevelsValueChanged(double value) => Config.Settings.QuarryLevels = (int)value;
   private static void OnMagicLevelsValueChanged(double value) => Config.Settings.MagicLevels = (int)value;
   private static void OnDungeonLevelsValueChanged(double value) => Config.Settings.DungeonLevels = (int)value;
   private static void OnBrickQuantityValueChanged(double value) => Config.Settings.BrickQuantity = (int)value;
   private static void OnGemQuantityValueChanged(double value) => Config.Settings.GemQuantity = (int)value;
   private static void OnRecruitQuantityValueChanged(double value) => Config.Settings.RecruitQuantity = (int)value;

   private static void OnAutoBricksValueChanged(double value) => Config.Settings.AutoBricks = (int)value;
   private static void OnAutoGemsValueChanged(double value) => Config.Settings.AutoGems = (int)value;
   private static void OnAutoRecruitsValueChanged(double value) => Config.Settings.AutoRecruits = (int)value;
   private static void OnCardsInHandValueChanged(double value) => Config.Settings.CardsInHand = (int)value;
   private static void OnAiModeChanged(long index) => Config.Settings.CurrentAiType = (AiType)index;

   private static void OnTowerVictoryValueChanged(double value) => Config.Settings.TowerVictory = (int)value;
   private static void OnResourceVictoryValueChanged(double value) => Config.Settings.ResourceVictory = (int)value;

   private static void OnTavernPresetChanged(long index) => Config.Settings.CurrentTavern = (Tavern)index;

   private void OnLanguageChanged(long index)
   {
      Config.Settings.CurrentLocale = (Locale)index;
      switch ((Locale)index)
      {
         case Locale.En:
            TranslationServer.SetLocale("en");
            TranslationErrors.Hide();
            break;
         case Locale.Ru:
            TranslationServer.SetLocale("ru");
            TranslationErrors.Show();
            break;
         case Locale.Uk:
            TranslationServer.SetLocale("uk");
            TranslationErrors.Show();
            break;
         case Locale.Pl:
            TranslationServer.SetLocale("pl");
            TranslationErrors.Show();
            break;
         case Locale.Da:
            TranslationServer.SetLocale("da");
            TranslationErrors.Show();
            break;
         case Locale.De:
            TranslationServer.SetLocale("de");
            TranslationErrors.Show();
            break;
         case Locale.Fr:
            TranslationServer.SetLocale("fr");
            TranslationErrors.Show();
            break;
         default:
            TranslationServer.SetLocale("en");
            TranslationErrors.Show();
            break;
      }
   }

   private void OnNicknameChanged(string newText) => Config.Settings.Nickname = newText;
}