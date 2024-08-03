using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Arcomage.Scripts;

public class Player
{
    public long Id { get; init; }
    public string Name { get; init; }
    public bool Host { get; init; }
    public bool Ai { get; set; }
    public bool Ready { get; set; }
}
    
public partial class Table : Control
{
    private readonly RandomNumberGenerator _rng = new();

    #region Controls

    private Control Particles => GetNode<Control>("Particles");
    private TextureRect GraveyardCardBack => GetNode<TextureRect>("Graveyard/CardBack");
    private GridContainer Graveyard => GetNode<GridContainer>("Graveyard");
    private Label DrawCardLabel => GetNode<Label>("DrawCardLabel");
    private Control EndGameScreen => GetNode<Control>("EndGame");
    private Timer TimeElapsed => GetNode<Timer>("TimeElapsed");

    private HBoxContainer RedDeck => GetNode<HBoxContainer>("RedDeck");
    private HBoxContainer BlueDeck => GetNode<HBoxContainer>("BlueDeck");
    private Label RedNamePanel => GetNode<Label>("RedPanel/Name");
    private Label BlueNamePanel => GetNode<Label>("BluePanel/Name");
    private ColorRect DeckLocker => GetNode<ColorRect>("DeckLocker");

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
    private Label RedTowerHpPanel => GetNode<Label>("RedTowerPanel/Hp");
    private Label RedWallHpPanel => GetNode<Label>("RedWallPanel/Hp");
    private Label BlueTowerHpPanel => GetNode<Label>("BlueTowerPanel/Hp");
    private Label BlueWallHpPanel => GetNode<Label>("BlueWallPanel/Hp");

    private Control InGameMenu => GetNode<Control>("InGameMenu");

    #endregion

    public List<Player> Players = new();
    private int _turn;
    public bool AiReady = true;
    public bool RedPlayAgain = false;
    public bool BluePlayAgain = false;
    public bool RedDiscarding = false;
    public bool BlueDiscarding = false;
    public bool RedDrawCard = false;
    public bool BlueDrawCard = false;

    // Default towers and wall hp setters
    public int RedTowerHp = Config.Settings.TowerLevels;
    public int RedWallHp = Config.Settings.WallLevels;

    public int BlueTowerHp = Config.Settings.TowerLevels;
    public int BlueWallHp = Config.Settings.WallLevels;

    // Default resources setters
    public int RedQuarries = Config.Settings.QuarryLevels;
    public int RedBricks = Config.Settings.BrickQuantity;
    public int RedMagic = Config.Settings.MagicLevels;
    public int RedGems = Config.Settings.GemQuantity;
    public int RedDungeons = Config.Settings.DungeonLevels;
    public int RedRecruits = Config.Settings.RecruitQuantity;

    public int BlueQuarries = Config.Settings.QuarryLevels;
    public int BlueBricks = Config.Settings.BrickQuantity;
    public int BlueMagic = Config.Settings.MagicLevels;
    public int BlueGems = Config.Settings.GemQuantity;
    public int BlueDungeons = Config.Settings.DungeonLevels;
    public int BlueRecruits = Config.Settings.RecruitQuantity;

    public int Elapsed = 0;
    public string ElapsedString = "00:00";

    public bool IsOffline;

    [Signal]
    public delegate void GraveyardAnimationEndedEventHandler();

    [Signal]
    public delegate void DeckAnimationEndedEventHandler();

    public override void _Input(InputEvent @event)
    {
        base._Input(@event);
        if (!Input.IsActionJustPressed("ui_cancel")) return;
        InGameMenu.Show();
        GetTree().Paused = true;
    }

    public override void _Ready()
    {
        var args = Global.GetCommandLineArgs();
        if (args.TryGetValue("playerName", out var name))
        {
            Logger.Debug("Player name from command line: " + name);
            Config.Settings.Nickname = name;
        }
        
        Logger.Debug("Table loaded");
        Global.Table = this;

        if (Multiplayer.MultiplayerPeer is OfflineMultiplayerPeer)
        {
            IsOffline = true;
            RedNamePanel.Text = Config.Settings.Nickname;
            BlueNamePanel.Text = Tr("COMPUTER");
        }
        else if (Multiplayer.MultiplayerPeer is not null)
        {
            IsOffline = false;
            Players = Global.NetworkSetup.Players;
            RedNamePanel.Text = Players[0].Name;
            BlueNamePanel.Text = Players[1].Name;
        }
        
        LocaleStatPanels();
        
        if (!Multiplayer.IsServer()) return;
        
        SpawnLocalPlayer();
            
        Multiplayer.PeerConnected += AddPlayer;
        Multiplayer.PeerDisconnected += RemovePlayer;
        
        SpawnConnectedPlayers();
        
        _rng.Randomize();
        _turn = _rng.RandiRange(0, IsOffline ? 1 : Players.Count);
        AddResources(_turn);
        PlaceStartCardsOnDeck();
        
        if (_turn == 0)
        {
            RedDeck.Show();
            BlueDeck.Hide();
        }
        else
        {
            RedDeck.Hide();
            BlueDeck.Show();
        }
    }

    public override void _ExitTree()
    {
        if (!Multiplayer.IsServer()) return;

        Multiplayer.PeerConnected -= AddPlayer;
        Multiplayer.PeerDisconnected -= RemovePlayer;
    }

    public override void _PhysicsProcess(double delta)
    {
        UpdateStatPanelUi();
    }

    private void PlaceStartCardsOnDeck()
    {
        var card = (PackedScene)ResourceLoader.Load("res://Scenes/Card.tscn");
        for (var i = 0; i < Config.Settings.CardsInHand; i++)
        {
            var newCard = (Control)card.Instantiate();
            RedDeck.AddChild(newCard);
        }
        
        for (var i = 0; i < Config.Settings.CardsInHand; i++)
        {
            var newCard = (Control)card.Instantiate();
            BlueDeck.AddChild(newCard);
        }
    }

    private void SpawnConnectedPlayers()
    {
        foreach (var id in Multiplayer.GetPeers()) 
            AddPlayer(id);
    }

    private void SpawnLocalPlayer()
    {
        if (!OS.HasFeature("dedicated_server"))
            AddPlayer(1);
    }

    private void AddPlayer(long id)
    {
        Logger.Debug("[TABLE] Adding player with id: " + id);
        if (id == 1)
            RegisterPlayer(id, Config.Settings.Nickname);
        else
            RpcId(id, nameof(RequestNickname));
    }
    
    private void RemovePlayer(long id)
    {
        if (Players.All(x => x.Id != id)) return;
        var player = Players.First(x => x.Id == id);
        Players.Remove(player);
    }
    
    private void AddResources(int turn)
    {
        if (turn == 0)
        {
            Logger.Debug("RB: " + RedBricks + " RG: " + RedGems + " RR: " + RedRecruits);
            RedBricks += RedQuarries;
            RedGems += RedMagic;
            RedRecruits += RedDungeons;
            Logger.Debug("RB: " + RedBricks + " RG: " + RedGems + " RR: " + RedRecruits);
        }
        else
        {
            Logger.Debug("BB: " + BlueBricks + " BG: " + BlueGems + " BR: " + BlueRecruits);
            BlueBricks += BlueQuarries;
            BlueGems += BlueMagic;
            BlueRecruits += BlueDungeons;
            Logger.Debug("BB: " + BlueBricks + " BG: " + BlueGems + " BR: " + BlueRecruits);
        }
    }

    private void LocaleStatPanels() => 
        SwitchStatPanel(TranslationServer.GetLocale() == "en");

    private void SwitchStatPanel(bool toggle)
    {
        if (toggle)
        {
            RedBricksPanel.Show();
            RedGemsPanel.Show();
            RedRecruitsPanel.Show();
            BlueBricksPanel.Show();
            BlueGemsPanel.Show();
            BlueRecruitsPanel.Show();
            
            RedBricksAltPanel.Hide();
            RedGemsAltPanel.Hide();
            RedRecruitsAltPanel.Hide();
            BlueBricksAltPanel.Hide();
            BlueGemsAltPanel.Hide();
            BlueRecruitsAltPanel.Hide();
        }
        else
        {
            RedBricksPanel.Hide();
            RedGemsPanel.Hide();
            RedRecruitsPanel.Hide();
            BlueBricksPanel.Hide();
            BlueGemsPanel.Hide();
            BlueRecruitsPanel.Hide();
            
            RedBricksAltPanel.Show();
            RedGemsAltPanel.Show();
            RedRecruitsAltPanel.Show();
            BlueBricksAltPanel.Show();
            BlueGemsAltPanel.Show();
            BlueRecruitsAltPanel.Show();
        }
    }

    private void UpdateStatPanelUi()
    {
        RedBricksPerTurn.Text = RedQuarries.ToString();
        RedBricksAltPerTurn.Text = RedQuarries.ToString();
        RedBricksTotal.Text = RedBricks.ToString();
        RedBricksAltTotal.Text = RedBricks.ToString();
        
        RedGemsPerTurn.Text = RedMagic.ToString();
        RedGemsAltPerTurn.Text = RedMagic.ToString();
        RedGemsTotal.Text = RedGems.ToString();
        RedGemsAltTotal.Text = RedGems.ToString();
        
        RedRecruitsPerTurn.Text = RedDungeons.ToString();
        RedRecruitsAltPerTurn.Text = RedDungeons.ToString();
        RedRecruitsTotal.Text = RedRecruits.ToString();
        RedRecruitsAltTotal.Text = RedRecruits.ToString();
        
        BlueBricksPerTurn.Text = BlueQuarries.ToString();
        BlueBricksAltPerTurn.Text = BlueQuarries.ToString();
        BlueBricksTotal.Text = BlueBricks.ToString();
        BlueBricksAltTotal.Text = BlueBricks.ToString();
        
        BlueGemsPerTurn.Text = BlueMagic.ToString();
        BlueGemsAltPerTurn.Text = BlueMagic.ToString();
        BlueGemsTotal.Text = BlueGems.ToString();
        BlueGemsAltTotal.Text = BlueGems.ToString();
        
        BlueRecruitsPerTurn.Text = BlueDungeons.ToString();
        BlueRecruitsAltPerTurn.Text = BlueDungeons.ToString();
        BlueRecruitsTotal.Text = BlueRecruits.ToString();
        BlueRecruitsAltTotal.Text = BlueRecruits.ToString();
        
        RedTowerHpPanel.Text = RedTowerHp.ToString();
        RedWallHpPanel.Text = RedWallHp.ToString();
        
        BlueTowerHpPanel.Text = BlueTowerHp.ToString();
        BlueWallHpPanel.Text = BlueWallHp.ToString();
    }
    
    [Rpc]
    public void RequestNickname()
    {
        Logger.Debug("[TABLE] Nickname requested");
        RpcId(1, nameof(RespondNickname), Config.Settings.Nickname);
    }
    
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void RespondNickname(string name)
    {
        Logger.Debug("[TABLE] Nickname received: " + name);
        long id = Multiplayer.GetRemoteSenderId();
        RegisterPlayer(id, name);
    }
    
    private void RegisterPlayer(long id, string name)
    {
        Logger.Debug("[TABLE] Registering player with id: " + id + " and name: " + name);
        var isHost = id == 1;
        Players.Add(new Player { Id = id, Name = name, Host = isHost, Ai = false });
        Rpc(nameof(AddRemotePlayer), id, name);
    }
    
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void AddRemotePlayer(long id, string name)
    {
        Logger.Debug("[TABLE] Adding remote player with id: " + id + " and name: " + name);
        var isHost = id == 1;
        Players.Add(new Player { Id = id, Name = name, Host = isHost, Ai = false });
    }
}