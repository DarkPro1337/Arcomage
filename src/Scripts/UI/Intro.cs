using Godot;
using Logger = Arcomage.Logging.Logger;

namespace Arcomage.UI;

public partial class Intro : Control
{
   private static readonly Logger _logger = Logger.GetOrCreateLogger("Intro");

   public override void _EnterTree()
   {
      base._EnterTree();
      var anim = GetNode<AnimationPlayer>("Animator");
      anim.Connect("animation_finished", new Callable(this, nameof(OnAnimPlayerAnimationFinished)));
   }

   private void OnAnimPlayerAnimationFinished(string animName)
   {
      if (animName != "StartUp") return;
      _logger.Debug("Loading to the Main menu...");
      GetTree().CallDeferred("change_scene_to_file", "res://Scenes/Main/MainMenu.tscn");
   }

   public override void _Input(InputEvent @event)
   {
      base._Input(@event);
      if (!Input.IsActionJustPressed("ui_cancel") && !Input.IsActionJustPressed("ui_select")) return;
      _logger.Debug("Skipping Intro to the Main menu...");
      GetTree().CallDeferred("change_scene_to_file", "res://Scenes/Main/MainMenu.tscn");
   }
}