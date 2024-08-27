using Godot;
using static Arcomage.Scripts.Global;

namespace Arcomage.Scripts;

public partial class Boot : Node
{
    public override void _Ready()
    {
        var version = ProjectSettings.GetSetting("application/config/version").ToString();
        Logger.Debug("Bootstrap loaded");
        Logger.Debug("Arcomage {Version} loaded", version);
        Logger.Debug("Build number: {BuildNumber}", BuildNumber);
        Logger.Debug("IP address: {IPv4}", Network.IpAddress);

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