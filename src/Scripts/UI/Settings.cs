using System;
using System.Globalization;
using System.Linq;
using Arcomage.Core;
using Godot;
using Logger = Arcomage.Logging.Logger;

namespace Arcomage.UI;

public enum BusType
{
   Master,
   Music,
   Sound
}

public partial class Settings : Control
{
   private static readonly Logger _logger = Logger.GetOrCreateLogger("Settings");

   private readonly CultureInfo _invariantCulture = CultureInfo.InvariantCulture;

   #region Control vars

   private AnimationPlayer _anim;
   private Button _reset;
   private Button _close;
   private TabContainer _tab;
   private Button _windowSettingsButton;
   private HBoxContainer _fullscreen;
   private CheckButton _fullscreenButton;
   private CheckButton _borderlessButton;
   private HBoxContainer _windowResolution;
   private LineEdit _windowWidthEdit;
   private LineEdit _windowHeightEdit;
   private Button _windowResolutionApplyButton;
   private CheckButton _vsyncButton;
   private HBoxContainer _introSkip;
   private CheckButton _introSkipButton;
   private Button _soundSettingsButton;
   private HSlider _masterVolume;
   private HSlider _musicVolume;
   private HSlider _soundVolume;
   private CheckBox _muteSound;
   private Button _startingConditionsButton;
   private CheckBox _singleClickButton;
   private SpinBox _towerLevels;
   private SpinBox _wallLevels;
   private SpinBox _quarryLevels;
   private SpinBox _brickQuantity;
   private SpinBox _magicLevels;
   private SpinBox _gemQuantity;
   private SpinBox _dungeonLevels;
   private SpinBox _recruitQuantity;
   private Button _playConditionsButton;
   private SpinBox _autoBricks;
   private SpinBox _autoGems;
   private SpinBox _autoRecruits;
   private SpinBox _cardsInHand;
   private OptionButton _aiMode;
   private Button _victoryConditionsButton;
   private SpinBox _towerVictory;
   private SpinBox _resourceVictory;
   private Button _tavernPresetsButton;
   private OptionButton _tavernPreset;
   private Button _languageSettingsButton;
   private OptionButton _language;
   private Label _translationErrors;
   private Button _playerSettingsButton;
   private LineEdit _nickname;

   private AnimationPlayer Anim => _anim ??= GetNode<AnimationPlayer>("AnimationPlayer");
   private Button Reset => _reset ??= GetNode<Button>("Reset");
   private Button Close => _close ??= GetNode<Button>("Close");
   private TabContainer Tab => _tab ??= GetNode<TabContainer>("Tab");

   private Button WindowSettingsButton => _windowSettingsButton ??= GetNode<Button>("Options/Grid/WindowSettings");
   private HBoxContainer Fullscreen => _fullscreen ??= GetNode<HBoxContainer>("Tab/Graphics/Container/Fullscreen");
   private CheckButton FullscreenButton => _fullscreenButton ??= GetNode<CheckButton>("Tab/Graphics/Container/Fullscreen/Toggle");
   private CheckButton BorderlessButton => _borderlessButton ??= GetNode<CheckButton>("Tab/Graphics/Container/Borderless/Toggle");
   private HBoxContainer WindowResolution => _windowResolution ??= GetNode<HBoxContainer>("Tab/Graphics/Container/WindowResolution");
   private LineEdit WindowWidthEdit => _windowWidthEdit ??= GetNode<LineEdit>("Tab/Graphics/Container/WindowResolution/Width");
   private LineEdit WindowHeightEdit => _windowHeightEdit ??= GetNode<LineEdit>("Tab/Graphics/Container/WindowResolution/Height");
   private Button WindowResolutionApplyButton =>
      _windowResolutionApplyButton ??= GetNode<Button>("Tab/Graphics/Container/WindowResolution/ApplyButton");

   private CheckButton VsyncButton => _vsyncButton ??= GetNode<CheckButton>("Tab/Graphics/Container/Vsync/Toggle");
   private HBoxContainer IntroSkip => _introSkip ??= GetNode<HBoxContainer>("Tab/Graphics/Container/IntroSkip");
   private CheckButton IntroSkipButton => _introSkipButton ??= GetNode<CheckButton>("Tab/Graphics/Container/IntroSkip/Toggle");

   private Button SoundSettingsButton => _soundSettingsButton ??= GetNode<Button>("Options/Grid/SoundSettings");
   private HSlider MasterVolume => _masterVolume ??= GetNode<HSlider>("Tab/Sound/Container/Master/Slider");
   private HSlider MusicVolume => _musicVolume ??= GetNode<HSlider>("Tab/Sound/Container/Music/Slider");
   private HSlider SoundVolume => _soundVolume ??= GetNode<HSlider>("Tab/Sound/Container/Sounds/Slider");
   private CheckBox MuteSound => _muteSound ??= GetNode<CheckBox>("Tab/Sound/Container/Mute/Toggle");

   private Button StartingConditionsButton => _startingConditionsButton ??= GetNode<Button>("Options/Grid/StartingConditions");

   private CheckBox SingleClickButton => _singleClickButton ??= GetNode<CheckBox>("Tab/StartingConditions/Container/Main/Gameplay/SingleClick/Toggle");
   private SpinBox TowerLevels => _towerLevels ??= GetNode<SpinBox>("Tab/StartingConditions/Container/Main/TowersWalls/TowerLevels/Level");
   private SpinBox WallLevels => _wallLevels ??= GetNode<SpinBox>("Tab/StartingConditions/Container/Main/TowersWalls/WallLevels/Level");
   private SpinBox QuarryLevels => _quarryLevels ??= GetNode<SpinBox>("Tab/StartingConditions/Container/ResourceGeneration/Generators/Quarry/Level");
   private SpinBox BrickQuantity => _brickQuantity ??= GetNode<SpinBox>("Tab/StartingConditions/Container/ResourceGeneration/Resources/Bricks/Level");
   private SpinBox MagicLevels => _magicLevels ??= GetNode<SpinBox>("Tab/StartingConditions/Container/ResourceGeneration/Generators/Magic/Level");
   private SpinBox GemQuantity => _gemQuantity ??= GetNode<SpinBox>("Tab/StartingConditions/Container/ResourceGeneration/Resources/Gems/Level");
   private SpinBox DungeonLevels => _dungeonLevels ??= GetNode<SpinBox>("Tab/StartingConditions/Container/ResourceGeneration/Generators/Dungeon/Level");
   private SpinBox RecruitQuantity => _recruitQuantity ??= GetNode<SpinBox>("Tab/StartingConditions/Container/ResourceGeneration/Resources/Recruits/Level");

   private Button PlayConditionsButton => _playConditionsButton ??= GetNode<Button>("Options/Grid/PlayConditions");
   private SpinBox AutoBricks => _autoBricks ??= GetNode<SpinBox>("Tab/PlayConditions/Container/AutoGetter/Bricks/Level");
   private SpinBox AutoGems => _autoGems ??= GetNode<SpinBox>("Tab/PlayConditions/Container/AutoGetter/Gems/Level");
   private SpinBox AutoRecruits => _autoRecruits ??= GetNode<SpinBox>("Tab/PlayConditions/Container/AutoGetter/Recruits/Level");
   private SpinBox CardsInHand => _cardsInHand ??= GetNode<SpinBox>("Tab/PlayConditions/Container/Other/CardsInHand/Level");
   private OptionButton AiMode => _aiMode ??= GetNode<OptionButton>("Tab/PlayConditions/Container/Other/Ai/Mode");

   private Button VictoryConditionsButton => _victoryConditionsButton ??= GetNode<Button>("Options/Grid/VictoryConditions");
   private SpinBox TowerVictory => _towerVictory ??= GetNode<SpinBox>("Tab/VictoryConditions/Container/TowerVictory/Level");
   private SpinBox ResourceVictory => _resourceVictory ??= GetNode<SpinBox>("Tab/VictoryConditions/Container/ResourceVictory/Level");

   private Button TavernPresetsButton => _tavernPresetsButton ??= GetNode<Button>("Options/Grid/TavernPresets");
   private OptionButton TavernPreset => _tavernPreset ??= GetNode<OptionButton>("Tab/TavernPresets/Container/Preset/Option");

   private Button LanguageSettingsButton => _languageSettingsButton ??= GetNode<Button>("Options/Grid/LanguageSettings");
   private OptionButton Language => _language ??= GetNode<OptionButton>("Tab/LanguageSettings/Container/Language/Option");
   private Label TranslationErrors => _translationErrors ??= GetNode<Label>("Tab/LanguageSettings/Container/TranslationErrors");

   private Button PlayerSettingsButton => _playerSettingsButton ??= GetNode<Button>("Options/Grid/PlayerSettings");
   private LineEdit Nickname => _nickname ??= GetNode<LineEdit>("Tab/PlayerSettings/Container/Nickname/Edit");

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

      GenerateTavernPresets(TavernPreset);
   }

   private void SettingsVisibilityChanged()
   {
      if (!Visible)
         return;

      var parentName = GetParent().Name.ToString();
      if (string.IsNullOrEmpty(parentName))
      {
         _logger.Debug("Settings loaded from source with empty name");
         return;
      }
        
      if (parentName == "InGameMenu")
      {
         _logger.Debug("Settings loaded from InGameMenu");
         StartingConditionsButton.Hide();
         PlayConditionsButton.Hide();
         VictoryConditionsButton.Hide();
         TavernPresetsButton.Hide();
         PlayerSettingsButton.Hide();
         IntroSkip.Hide();
         Reset.Hide();
      }
      else if (parentName != "MainMenu")
      {
         _logger.Debug("Settings loaded from unknown source and not shown");
         Hide();
      }
   }

   public override void _Ready()
   {
      UpdateLocale();
      UpdateControls();

      Global.TranslationManager.TranslationChanged += UpdateLocale;
   }

   private async void OnClosePressed()
   {
      try
      {
         Config.SaveSettings();
         Anim.Play("hide");
         await ToSignal(Anim, AnimationMixer.SignalName.AnimationFinished);
         Hide();
      }
      catch (Exception ex)
      {
         _logger.Error(ex, "Failed to save settings");
      }
      finally
      {
         Hide();
      }
   }

   private void OnResetPressed()
   {
      Config.Settings = new GameSettings();
      Config.SaveSettings();
      UpdateLocale();
      UpdateControls();
   }

   public void UpdateControls()
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

      TavernPreset.Selected = Config.Settings.CurrentTavern;
      Language.Selected = Global.TranslationManager.GetLoadedLocaleIndex();

      Nickname.Text = Config.Settings.Nickname;
   }

   private void UpdateLocale()
   {
      Language.Clear();
      foreach (var locale in Global.TranslationManager.LoadedLocales)
         Language.AddItem(locale.DisplayName);

      TranslationServer.SetLocale(Config.Settings.CurrentLocale);
      _logger.Debug("Loaded locale - {Locale}", Config.Settings.CurrentLocale);
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

      _logger.Debug("Fullscreen toggled to " + toggle);
   }

   private void OnBorderlessButtonToggled(bool toggle)
   {
      Config.Settings.Borderless = toggle;
      DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, toggle);
      if (toggle && FullscreenButton.ButtonPressed)
         Fullscreen.Hide();
      else
         Fullscreen.Show();

      _logger.Debug("Borderless toggled to " + toggle);
   }

   private void OnWindowResolutionApplyPressed()
   {
      if (WindowWidthEdit.Text == "" || WindowHeightEdit.Text == "")
      {
         _logger.Error("Window resolution can't be empty");
         return;
      }

      if (!int.TryParse(WindowWidthEdit.Text, out var width) || !int.TryParse(WindowHeightEdit.Text, out var height))
      {
         _logger.Error("Window resolution must be a number");
         return;
      }

      if (DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Maximized)
         DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);

      Config.Settings.WindowWidth = width;
      Config.Settings.WindowHeight = height;
      var resolution = new Vector2I(width, height);
      DisplayServer.WindowSetSize(resolution);
      Config.CenterWindow(GetWindow());

      _logger.Debug("Window resolution applied to " + WindowWidthEdit.Text + "x" + WindowHeightEdit.Text);
   }

   private static void OnVsyncButtonToggled(bool toggle)
   {
      Config.Settings.Vsync = toggle;
      DisplayServer.WindowSetVsyncMode(toggle ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
      _logger.Debug("Vsync toggled to " + toggle);
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

   private void OnTavernPresetChanged(long index) => Config.Settings.CurrentTavern = (int)index;

   private void GenerateTavernPresets(OptionButton tavernsButton)
   {
      tavernsButton.Clear();
      tavernsButton.AddItem("NONE", 0);
      foreach (var tavernPack in Global.TavernManager.TavernPacks)
      {
         tavernsButton.AddSeparator(tavernPack.Name);
         foreach (var tavern in tavernPack.Taverns)
         {
            tavernsButton.AddItem(tavern.Id);
            tavern.Index = tavernsButton.ItemCount - 1;
         }
      }
   }

   private void OnLanguageChanged(long index)
   {
      var locale = Global.TranslationManager.LoadedLocales.ElementAt((int)index);
      Config.Settings.CurrentLocale = locale.Name;
      TranslationServer.SetLocale(locale.Name);
      if (locale.Name != "en")
         TranslationErrors.Show();
      else
         TranslationErrors.Hide();
   }

   private void OnNicknameChanged(string newText) => Config.Settings.Nickname = newText;
}