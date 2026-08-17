using Godot;

namespace Arcomage.Gameplay;

public partial class Table
{
   private Control _particles;
   private TextureRect _graveyardCardBack;
   private GridContainer _graveyard;
   private Label _drawCardLabel;
   private Control _matchResult;
   private Timer _timeElapsed;

   private HBoxContainer _redDeck;
   private HBoxContainer _blueDeck;
   private Label _redNamePanel;
   private Label _blueNamePanel;
   private ColorRect _deckLocker;
   private Control _cardAnimLayer;

   private Panel _redBricksPanel;
   private Label _redBricksPerTurn;
   private Label _redBricksTotal;
   private Panel _redGemsPanel;
   private Label _redGemsPerTurn;
   private Label _redGemsTotal;
   private Panel _redRecruitsPanel;
   private Label _redRecruitsPerTurn;
   private Label _redRecruitsTotal;

   private Panel _blueBricksPanel;
   private Label _blueBricksPerTurn;
   private Label _blueBricksTotal;
   private Panel _blueGemsPanel;
   private Label _blueGemsPerTurn;
   private Label _blueGemsTotal;
   private Panel _blueRecruitsPanel;
   private Label _blueRecruitsPerTurn;
   private Label _blueRecruitsTotal;

   private Panel _redBricksAltPanel;
   private Label _redBricksAltPerTurn;
   private Label _redBricksAltTotal;
   private Panel _redGemsAltPanel;
   private Label _redGemsAltPerTurn;
   private Label _redGemsAltTotal;
   private Panel _redRecruitsAltPanel;
   private Label _redRecruitsAltPerTurn;
   private Label _redRecruitsAltTotal;

   private Panel _blueBricksAltPanel;
   private Label _blueBricksAltPerTurn;
   private Label _blueBricksAltTotal;
   private Panel _blueGemsAltPanel;
   private Label _blueGemsAltPerTurn;
   private Label _blueGemsAltTotal;
   private Panel _blueRecruitsAltPanel;
   private Label _blueRecruitsAltPerTurn;
   private Label _blueRecruitsAltTotal;

   private Control _redTower;
   private Control _redWall;
   private Label _redTowerHpPanel;
   private Label _redWallHpPanel;
   private Control _blueTower;
   private Control _blueWall;
   private Label _blueTowerHpPanel;
   private Label _blueWallHpPanel;

   private Control _inGameMenu;

   private Control Particles => _particles ??= GetNode<Control>("Particles");
   private TextureRect GraveyardCardBack => _graveyardCardBack ??= GetNode<TextureRect>("Graveyard/CardBack");
   private GridContainer Graveyard => _graveyard ??= GetNode<GridContainer>("Graveyard");
   private Label DrawCardLabel => _drawCardLabel ??= GetNode<Label>("DrawCardLabel");
   private Control MatchResult => _matchResult ??= GetNode<Control>("MatchResult");
   private Timer TimeElapsed => _timeElapsed ??= GetNode<Timer>("TimeElapsed");

   private HBoxContainer RedDeck => _redDeck ??= GetNode<HBoxContainer>("RedDeck");
   private HBoxContainer BlueDeck => _blueDeck ??= GetNode<HBoxContainer>("BlueDeck");
   private Label RedNamePanel => _redNamePanel ??= GetNode<Label>("RedPanel/Name");
   private Label BlueNamePanel => _blueNamePanel ??= GetNode<Label>("BluePanel/Name");
   private ColorRect DeckLocker => _deckLocker ??= GetNode<ColorRect>("DeckLocker");
   private Control CardAnimLayer => _cardAnimLayer ??= GetNode<Control>("CardAnimLayer");

   private Panel RedBricksPanel => _redBricksPanel ??= GetNode<Panel>("RedBricksPanel");
   private Label RedBricksPerTurn => _redBricksPerTurn ??= GetNode<Label>("RedBricksPanel/PerTurn");
   private Label RedBricksTotal => _redBricksTotal ??= GetNode<Label>("RedBricksPanel/Total");
   private Panel RedGemsPanel => _redGemsPanel ??= GetNode<Panel>("RedGemsPanel");
   private Label RedGemsPerTurn => _redGemsPerTurn ??= GetNode<Label>("RedGemsPanel/PerTurn");
   private Label RedGemsTotal => _redGemsTotal ??= GetNode<Label>("RedGemsPanel/Total");
   private Panel RedRecruitsPanel => _redRecruitsPanel ??= GetNode<Panel>("RedRecruitsPanel");
   private Label RedRecruitsPerTurn => _redRecruitsPerTurn ??= GetNode<Label>("RedRecruitsPanel/PerTurn");
   private Label RedRecruitsTotal => _redRecruitsTotal ??= GetNode<Label>("RedRecruitsPanel/Total");

   private Panel BlueBricksPanel => _blueBricksPanel ??= GetNode<Panel>("BlueBricksPanel");
   private Label BlueBricksPerTurn => _blueBricksPerTurn ??= GetNode<Label>("BlueBricksPanel/PerTurn");
   private Label BlueBricksTotal => _blueBricksTotal ??= GetNode<Label>("BlueBricksPanel/Total");
   private Panel BlueGemsPanel => _blueGemsPanel ??= GetNode<Panel>("BlueGemsPanel");
   private Label BlueGemsPerTurn => _blueGemsPerTurn ??= GetNode<Label>("BlueGemsPanel/PerTurn");
   private Label BlueGemsTotal => _blueGemsTotal ??= GetNode<Label>("BlueGemsPanel/Total");
   private Panel BlueRecruitsPanel => _blueRecruitsPanel ??= GetNode<Panel>("BlueRecruitsPanel");
   private Label BlueRecruitsPerTurn => _blueRecruitsPerTurn ??= GetNode<Label>("BlueRecruitsPanel/PerTurn");
   private Label BlueRecruitsTotal => _blueRecruitsTotal ??= GetNode<Label>("BlueRecruitsPanel/Total");

   private Panel RedBricksAltPanel => _redBricksAltPanel ??= GetNode<Panel>("RedBricksPanelAlt");
   private Label RedBricksAltPerTurn => _redBricksAltPerTurn ??= GetNode<Label>("RedBricksPanelAlt/PerTurn");
   private Label RedBricksAltTotal => _redBricksAltTotal ??= GetNode<Label>("RedBricksPanelAlt/Total");
   private Panel RedGemsAltPanel => _redGemsAltPanel ??= GetNode<Panel>("RedGemsPanelAlt");
   private Label RedGemsAltPerTurn => _redGemsAltPerTurn ??= GetNode<Label>("RedGemsPanelAlt/PerTurn");
   private Label RedGemsAltTotal => _redGemsAltTotal ??= GetNode<Label>("RedGemsPanelAlt/Total");
   private Panel RedRecruitsAltPanel => _redRecruitsAltPanel ??= GetNode<Panel>("RedRecruitsPanelAlt");
   private Label RedRecruitsAltPerTurn => _redRecruitsAltPerTurn ??= GetNode<Label>("RedRecruitsPanelAlt/PerTurn");
   private Label RedRecruitsAltTotal => _redRecruitsAltTotal ??= GetNode<Label>("RedRecruitsPanelAlt/Total");

   private Panel BlueBricksAltPanel => _blueBricksAltPanel ??= GetNode<Panel>("BlueBricksPanelAlt");
   private Label BlueBricksAltPerTurn => _blueBricksAltPerTurn ??= GetNode<Label>("BlueBricksPanelAlt/PerTurn");
   private Label BlueBricksAltTotal => _blueBricksAltTotal ??= GetNode<Label>("BlueBricksPanelAlt/Total");
   private Panel BlueGemsAltPanel => _blueGemsAltPanel ??= GetNode<Panel>("BlueGemsPanelAlt");
   private Label BlueGemsAltPerTurn => _blueGemsAltPerTurn ??= GetNode<Label>("BlueGemsPanelAlt/PerTurn");
   private Label BlueGemsAltTotal => _blueGemsAltTotal ??= GetNode<Label>("BlueGemsPanelAlt/Total");
   private Panel BlueRecruitsAltPanel => _blueRecruitsAltPanel ??= GetNode<Panel>("BlueRecruitsPanelAlt");
   private Label BlueRecruitsAltPerTurn => _blueRecruitsAltPerTurn ??= GetNode<Label>("BlueRecruitsPanelAlt/PerTurn");
   private Label BlueRecruitsAltTotal => _blueRecruitsAltTotal ??= GetNode<Label>("BlueRecruitsPanelAlt/Total");

   private Control RedTower => _redTower ??= GetNode<Control>("RedTower");
   private Control RedWall => _redWall ??= GetNode<Control>("RedWall");
   private Label RedTowerHpPanel => _redTowerHpPanel ??= GetNode<Label>("RedTowerPanel/Hp");
   private Label RedWallHpPanel => _redWallHpPanel ??= GetNode<Label>("RedWallPanel/Hp");
   private Control BlueTower => _blueTower ??= GetNode<Control>("BlueTower");
   private Control BlueWall => _blueWall ??= GetNode<Control>("BlueWall");
   private Label BlueTowerHpPanel => _blueTowerHpPanel ??= GetNode<Label>("BlueTowerPanel/Hp");
   private Label BlueWallHpPanel => _blueWallHpPanel ??= GetNode<Label>("BlueWallPanel/Hp");

   private Control InGameMenu => _inGameMenu ??= GetNode<Control>("InGameMenu");
}
