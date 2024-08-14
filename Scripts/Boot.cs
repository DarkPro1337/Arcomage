using Godot;
using static Arcomage.Scripts.Global;

namespace Arcomage.Scripts;

public partial class Boot : Node
{
    public override void _Ready()
    {
        var version = ProjectSettings.GetSetting("application/config/version");
        Logger.Debug("Bootstrap loaded");
        Logger.Debug($"Arcomage v.{version} loaded");
        Logger.Debug($"Build number: {BuildNumber}");
        Logger.Debug($"IP address: {Network.IpAddress}");

        if (!Config.Settings.IntroSkip)
        {
            Logger.Debug("Loading from Boot to Intro...");
            GetTree().ChangeSceneToFile("res://Scenes/Intro.tscn");
        }
        else
        {
            Logger.Debug("Loading from Boot to Main menu...");
            GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
        }
    }
}