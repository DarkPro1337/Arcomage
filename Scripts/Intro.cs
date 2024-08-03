using Godot;

namespace Arcomage.Scripts
{
	public partial class Intro : Control
	{
		public override void _EnterTree()
		{
			base._EnterTree();
			var anim = GetNode<AnimationPlayer>("Animator");
			anim.Connect("animation_finished", new Callable(this,nameof(OnAnimPlayerAnimationFinished)));
		}

		public override void _Ready()
		{
			Logger.Debug("Intro loaded...");
		}

		private void OnAnimPlayerAnimationFinished(string animName)
		{
			if (animName != "StartUp") return;
			Logger.Debug("Loading to the Main menu...");
			GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
		}
			
		public override void _Input(InputEvent @event)
		{
			base._Input(@event);
			if (!Input.IsActionJustPressed("ui_cancel") && !Input.IsActionJustPressed("ui_select")) return;
			Logger.Debug("Skipping Intro to the Main menu...");
			GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
		}
	}
}
