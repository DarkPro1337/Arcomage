using Godot;
using Logger = Arcomage.Logging.Logger;

namespace Arcomage.UI;

public partial class Intro : Control
{
   private static readonly Logger _logger = Logger.GetOrCreateLogger("Intro");

   private AnimationPlayer _anim;

   public override void _EnterTree()
   {
      base._EnterTree();
      _anim = GetNode<AnimationPlayer>("Animator");
      _anim.AnimationFinished += OnAnimPlayerAnimationFinished;
   }

   public override void _ExitTree()
   {
      if (_anim != null)
         _anim.AnimationFinished -= OnAnimPlayerAnimationFinished;

      base._ExitTree();
   }

   private void OnAnimPlayerAnimationFinished(StringName animName)
   {
      if (animName != "StartUp")
         return;

      _logger.Debug("Loading to the Main menu...");
      GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, "res://Scenes/Main/MainMenu.tscn");
   }

   public override void _Input(InputEvent @event)
   {
      base._Input(@event);
      if (!Input.IsActionJustPressed("ui_cancel") && !Input.IsActionJustPressed("ui_select"))
         return;

      _logger.Debug("Skipping Intro to the Main menu...");
      GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, "res://Scenes/Main/MainMenu.tscn");
   }
}
