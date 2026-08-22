namespace Arcomage.Gameplay;

/// <summary>
/// One-shot damage/heal particles and SFX for stat changes on <see cref="Table"/>.
/// Sound names follow the original 3DO Arcomage / GDScript mapping.
/// </summary>
public partial class Table
{
   private const string DamageSoundPath = "res://Sounds/damage.ogg";
   private const string TowerHealSoundPath = "res://Sounds/up.ogg";
   private const string WallHealSoundPath = "res://Sounds/heal.ogg";
   private const string ResourceUpSoundPath = "res://Sounds/launch.ogg";
   private const string ResourceDownSoundPath = "res://Sounds/quarry_down.ogg";
   private const string DealSoundPath = "res://Sounds/deal.ogg";
   private const string SoundsBus = "Sounds";

   private static readonly Dictionary<string, AudioStream> _sfxCache = new();

   private PackedScene _damageParticlesScene;
   private PackedScene _healParticlesScene;

   private PackedScene DamageParticlesScene => _damageParticlesScene ??= ResourceLoader.Load<PackedScene>("res://Scenes/Gameplay/Particles/Damage.tscn");
   private PackedScene HealParticlesScene => _healParticlesScene ??= ResourceLoader.Load<PackedScene>("res://Scenes/Gameplay/Particles/Heal.tscn");

   private sealed record StatSnapshot(int TowerHp, int WallHp, int Quarries, int Bricks, int Magic, int Gems, int Dungeons, int Recruits)
   {
      public static StatSnapshot From(Player player)
      {
         return new StatSnapshot(
            player.TowerHp,
            player.WallHp,
            player.Quarries,
            player.Bricks,
            player.Magic,
            player.Gems,
            player.Dungeons,
            player.Recruits);
      }
   }

   /// <summary>
   /// Captures tower, wall, generator, and resource totals for every seated player.
   /// </summary>
   private Dictionary<long, StatSnapshot> CaptureStatSnapshots()
   {
      var snapshots = new Dictionary<long, StatSnapshot>(Players.Count);
      foreach (var (id, player) in Players)
         snapshots[id] = StatSnapshot.From(player);

      return snapshots;
   }

   /// <summary>
   /// Pretends the card cost was already paid in <paramref name="snapshots"/>, so the cost
   /// itself does not count as a stat change for particles and SFX.
   /// Looks up cost from deck data: a freshly instantiated <see cref="CardControl"/> has
   /// <c>CardCost</c> / <c>CardLayout</c> only after <c>_Ready</c>.
   /// </summary>
   private static void ApplyPayCostToSnapshot(Dictionary<long, StatSnapshot> snapshots, long playerId, string cardId)
   {
      if (string.IsNullOrEmpty(cardId) || !snapshots.TryGetValue(playerId, out var snapshot))
         return;

      var def = Global.DeckManager.GetAllCards().FirstOrDefault(card => card.Id == cardId);
      if (def == null)
         return;

      snapshots[playerId] = def.Type switch
      {
         CardType.Brick => snapshot with { Bricks = snapshot.Bricks - def.Cost },
         CardType.Gem => snapshot with { Gems = snapshot.Gems - def.Cost },
         CardType.Recruit => snapshot with { Recruits = snapshot.Recruits - def.Cost },
         _ => snapshot
      };
   }

   /// <summary>
   /// Spawns particles over each control whose value changed and plays the matching SFX.
   /// Card cost is excluded: compare against a snapshot taken after <see cref="PayCost"/>.
   /// </summary>
   private void PlayStatChangeFeedback(Dictionary<long, StatSnapshot> before)
   {
      if (before == null)
         return;

      foreach (var player in Players.Values)
      {
         if (!before.TryGetValue(player.Id, out var snapshot))
            continue;

         NotifyIfChanged(player, ResourceTypes.Tower, snapshot.TowerHp, player.TowerHp);
         NotifyIfChanged(player, ResourceTypes.Wall, snapshot.WallHp, player.WallHp);
         NotifyIfChanged(player, ResourceTypes.Quarry, snapshot.Quarries, player.Quarries);
         NotifyIfChanged(player, ResourceTypes.Bricks, snapshot.Bricks, player.Bricks);
         NotifyIfChanged(player, ResourceTypes.Magic, snapshot.Magic, player.Magic);
         NotifyIfChanged(player, ResourceTypes.Gems, snapshot.Gems, player.Gems);
         NotifyIfChanged(player, ResourceTypes.Dungeon, snapshot.Dungeons, player.Dungeons);
         NotifyIfChanged(player, ResourceTypes.Recruits, snapshot.Recruits, player.Recruits);
      }
   }

   private void NotifyIfChanged(Player player, ResourceTypes resource, int previous, int current)
   {
      if (previous == current)
         return;

      var increased = current > previous;
      SpawnStatParticles(GetStatFeedbackControl(player, resource), increased);
      PlaySfx(GetStatSoundPath(resource, increased));
   }

   private Control GetStatFeedbackControl(Player player, ResourceTypes resource)
   {
      if (!_hudByPlayer.TryGetValue(player.Id, out var hud))
         return null;

      return hud.GetFeedbackControl(resource);
   }

   private static string GetStatSoundPath(ResourceTypes resource, bool increased)
   {
      return resource switch
      {
         ResourceTypes.Tower => increased ? TowerHealSoundPath : DamageSoundPath,
         ResourceTypes.Wall => increased ? WallHealSoundPath : DamageSoundPath,
         _ => increased ? ResourceUpSoundPath : ResourceDownSoundPath
      };
   }

   private void SpawnStatParticles(Control target, bool heal)
   {
      if (target == null || !IsInstanceValid(target))
         return;

      var scene = heal ? HealParticlesScene : DamageParticlesScene;
      if (scene == null)
         return;

      var particles = scene.Instantiate<GpuParticles2D>();
      Particles.AddChild(particles);
      particles.GlobalPosition = GetControlVisualCenter(target);
      particles.Restart();
      particles.Finished += OnParticlesFinished;

      var lifetime = particles.Lifetime / Mathf.Max(particles.SpeedScale, 0.01f) + 0.25f;
      GetTree().CreateTimer(lifetime).Timeout += OnParticlesFinished;
      return;

      void OnParticlesFinished()
      {
         if (IsInstanceValid(particles))
            particles.QueueFree();
      }
   }

   /// <summary>
   /// Visual center of <paramref name="control"/>, including rotation.
   /// Towers and walls are drawn upside-down (rotation π), so
   /// <see cref="Control.GetGlobalRect"/> sits under the base and is hidden by the deck.
   /// </summary>
   private static Vector2 GetControlVisualCenter(Control control)
   {
      return control.GetGlobalTransformWithCanvas() * (control.Size / 2f);
   }

   private void PlaySfx(string path)
   {
      if (!_sfxCache.TryGetValue(path, out var stream))
      {
         stream = ResourceLoader.Load<AudioStream>(path);
         if (stream != null)
            _sfxCache[path] = stream;
      }

      if (stream == null)
         return;

      var player = new AudioStreamPlayer
      {
         Stream = stream,
         Bus = SoundsBus
      };

      AddChild(player);
      player.Finished += player.QueueFree;
      player.Play();
   }
}
