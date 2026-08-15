using Godot;

namespace Arcomage.Gameplay;

public partial class Table
{
   private Control Particles => GetNode<Control>("Particles");
   private TextureRect GraveyardCardBack => GetNode<TextureRect>("Graveyard/CardBack");
   private GridContainer Graveyard => GetNode<GridContainer>("Graveyard");
   private Label DrawCardLabel => GetNode<Label>("DrawCardLabel");
   private Control MatchResult => GetNode<Control>("MatchResult");
   private Timer TimeElapsed => GetNode<Timer>("TimeElapsed");

   private HBoxContainer RedDeck => GetNode<HBoxContainer>("RedDeck");
   private HBoxContainer BlueDeck => GetNode<HBoxContainer>("BlueDeck");
   private Label RedNamePanel => GetNode<Label>("RedPanel/Name");
   private Label BlueNamePanel => GetNode<Label>("BluePanel/Name");
   private ColorRect DeckLocker => GetNode<ColorRect>("DeckLocker");
   private Control CardAnimLayer => GetNode<Control>("CardAnimLayer");

   private Panel RedBricksPanel => GetNode<Panel>("RedBricksPanel");
   private Label RedBricksPerTurn => GetNode<Label>("RedBricksPanel/PerTurn");
   private Label RedBricksTotal => GetNode<Label>("RedBricksPanel/Total");
   private Panel RedGemsPanel => GetNode<Panel>("RedGemsPanel");
   private Label RedGemsPerTurn => GetNode<Label>("RedGemsPanel/PerTurn");
   private Label RedGemsTotal => GetNode<Label>("RedGemsPanel/Total");
   private Panel RedRecruitsPanel => GetNode<Panel>("RedRecruitsPanel");
   private Label RedRecruitsPerTurn => GetNode<Label>("RedRecruitsPanel/PerTurn");
   private Label RedRecruitsTotal => GetNode<Label>("RedRecruitsPanel/Total");

   private Panel BlueBricksPanel => GetNode<Panel>("BlueBricksPanel");
   private Label BlueBricksPerTurn => GetNode<Label>("BlueBricksPanel/PerTurn");
   private Label BlueBricksTotal => GetNode<Label>("BlueBricksPanel/Total");
   private Panel BlueGemsPanel => GetNode<Panel>("BlueGemsPanel");
   private Label BlueGemsPerTurn => GetNode<Label>("BlueGemsPanel/PerTurn");
   private Label BlueGemsTotal => GetNode<Label>("BlueGemsPanel/Total");
   private Panel BlueRecruitsPanel => GetNode<Panel>("BlueRecruitsPanel");
   private Label BlueRecruitsPerTurn => GetNode<Label>("BlueRecruitsPanel/PerTurn");
   private Label BlueRecruitsTotal => GetNode<Label>("BlueRecruitsPanel/Total");

   private Panel RedBricksAltPanel => GetNode<Panel>("RedBricksPanelAlt");
   private Label RedBricksAltPerTurn => GetNode<Label>("RedBricksPanelAlt/PerTurn");
   private Label RedBricksAltTotal => GetNode<Label>("RedBricksPanelAlt/Total");
   private Panel RedGemsAltPanel => GetNode<Panel>("RedGemsPanelAlt");
   private Label RedGemsAltPerTurn => GetNode<Label>("RedGemsPanelAlt/PerTurn");
   private Label RedGemsAltTotal => GetNode<Label>("RedGemsPanelAlt/Total");
   private Panel RedRecruitsAltPanel => GetNode<Panel>("RedRecruitsPanelAlt");
   private Label RedRecruitsAltPerTurn => GetNode<Label>("RedRecruitsPanelAlt/PerTurn");
   private Label RedRecruitsAltTotal => GetNode<Label>("RedRecruitsPanelAlt/Total");

   private Panel BlueBricksAltPanel => GetNode<Panel>("BlueBricksPanelAlt");
   private Label BlueBricksAltPerTurn => GetNode<Label>("BlueBricksPanelAlt/PerTurn");
   private Label BlueBricksAltTotal => GetNode<Label>("BlueBricksPanelAlt/Total");
   private Panel BlueGemsAltPanel => GetNode<Panel>("BlueGemsPanelAlt");
   private Label BlueGemsAltPerTurn => GetNode<Label>("BlueGemsPanelAlt/PerTurn");
   private Label BlueGemsAltTotal => GetNode<Label>("BlueGemsPanelAlt/Total");
   private Panel BlueRecruitsAltPanel => GetNode<Panel>("BlueRecruitsPanelAlt");
   private Label BlueRecruitsAltPerTurn => GetNode<Label>("BlueRecruitsPanelAlt/PerTurn");
   private Label BlueRecruitsAltTotal => GetNode<Label>("BlueRecruitsPanelAlt/Total");
   private Control RedTower => GetNode<Control>("RedTower");
   private Control RedWall => GetNode<Control>("RedWall");
   private Label RedTowerHpPanel => GetNode<Label>("RedTowerPanel/Hp");
   private Label RedWallHpPanel => GetNode<Label>("RedWallPanel/Hp");
   private Control BlueTower => GetNode<Control>("BlueTower");
   private Control BlueWall => GetNode<Control>("BlueWall");
   private Label BlueTowerHpPanel => GetNode<Label>("BlueTowerPanel/Hp");
   private Label BlueWallHpPanel => GetNode<Label>("BlueWallPanel/Hp");

   private Control InGameMenu => GetNode<Control>("InGameMenu");
}
