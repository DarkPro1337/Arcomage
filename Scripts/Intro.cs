using Godot;

namespace Arcomage.Scripts;

public partial class Intro : Control
{
   private static readonly Logger _Logger = Logger.GetOrCreateLogger("Intro");

   public override void _EnterTree()
   {
      base._EnterTree();
      var anim = GetNode<AnimationPlayer>("Animator");
      anim.Connect("animation_finished", new Callable(this, nameof(OnAnimPlayerAnimationFinished)));
   }

   private void OnAnimPlayerAnimationFinished(string animName)
   {
      if (animName != "StartUp") return;
      _Logger.Debug("Loading to the Main menu...");
      GetTree().CallDeferred("change_scene_to_file", "res://scenes/MainMenu.tscn");
   }

   public override void _Input(InputEvent @event)
   {
      base._Input(@event);
      if (!Input.IsActionJustPressed("ui_cancel") && !Input.IsActionJustPressed("ui_select")) return;
      _Logger.Debug("Skipping Intro to the Main menu...");
      GetTree().CallDeferred("change_scene_to_file", "res://scenes/MainMenu.tscn");
   }
}