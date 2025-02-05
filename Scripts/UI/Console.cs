using System;
using System.Collections.Generic;
using System.Linq;
using Arcomage.Scripts.Core;
using Arcomage.Scripts.Logging;
using Godot;
using ImGuiNET;
using Vector2 = System.Numerics.Vector2;

namespace Arcomage.Scripts.UI;

public partial class Console : Node
{
   private static Logger _Logger = Logger.GetOrCreateLogger("Console");

   private readonly List<string> _items = [];
   private readonly List<string> _history = [];
   private string _inputText = "";
   private readonly Dictionary<string, Action<string[]>> _commands = new();
   private bool _isVisible;

   public override void _Ready()
   {
      RegisterCommand("clear", _ => _items.Clear());
      RegisterCommand("help", _ =>
      {
         _items.Add("Available commands:");
         foreach (var cmd in _commands.Keys)
            _items.Add($" - {cmd}");
      });
      RegisterCommand("echo", ExecuteEchoCommand);
      RegisterCommand("change_scene", ExecuteChangeSceneCommand);

      Logger.NewLogAdded += OnNewLogAdded;
   }

   private void OnNewLogAdded(string message) => _items.Add(message);

   private void ExecuteEchoCommand(string[] args)
   {
      if (args.Length == 0)
         _items.Add("echo: missing argument");
      else
         _items.Add(string.Join(" ", args));
   }

   private void ExecuteChangeSceneCommand(string[] args)
   {
      if (args.Length < 1 || args.Length > 1 || string.IsNullOrWhiteSpace(args[0]))
      {
         _items.Add("change_scene: missing or too many arguments");
      }
      else
      {
         var scenes = GetAllScenePaths("res://Scenes/");
         if (!scenes.TryGetValue(args[0], out var value))
         {
            _items.Add("change_scene: scene not found");
            return;
         }

         _Logger.Debug("Changing scene to {Scene}", value);
         GetTree().ChangeSceneDeferred(value);
      }
   }

   public override void _Input(InputEvent @event)
   {
      if (Input.IsActionJustPressed("ui_console"))
         _isVisible = !_isVisible;
   }

   private void RegisterCommand(string name, Action<string[]> action) => _commands[name] = action;

   public override void _Process(double delta)
   {
      if (!_isVisible)
         return;

      ImGui.Begin("Console");
      ImGui.BeginChild("ScrollingRegion", new Vector2(0, -30), ImGuiChildFlags.Border, ImGuiWindowFlags.HorizontalScrollbar);
      foreach (var item in _items)
         ImGui.TextUnformatted(item);
      if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
         ImGui.SetScrollHereY(1.0f);
      ImGui.EndChild();

      ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
      if (ImGui.InputText("##Input", ref _inputText, 256, ImGuiInputTextFlags.EnterReturnsTrue))
      {
         if (!string.IsNullOrWhiteSpace(_inputText))
         {
            _history.Add(_inputText);
            ExecuteCommand(_inputText);
            _inputText = "";
         }
         ImGui.SetKeyboardFocusHere(-1);
      }
      ImGui.End();
   }

   private void ExecuteCommand(string commandLine)
   {
      _items.Add($"] {commandLine}");
      var parts = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
      if (parts.Length == 0)
         return;
      var cmdName = parts[0];
      var args = parts.Skip(1).ToArray();
      if (_commands.TryGetValue(cmdName, out var command))
      {
         try { command(args); }
         catch (Exception ex) { _items.Add("Error: " + ex.Message); }
      }
      else
      {
         _items.Add("Unknown command: " + commandLine);
         _items.Add("Type 'help' for list of available commands.");
      }
   }

   private Dictionary<string, string> GetAllScenePaths(string startDirectory = "res://")
   {
      var scenePaths = new Dictionary<string, string>();
      using var dir = DirAccess.Open(startDirectory);
      if (dir == null)
         return scenePaths;

      dir.ListDirBegin();
      string fileName;
      while ((fileName = dir.GetNext()) != "")
      {
         if (fileName is "." or "..")
            continue;

         var filePath = $"{startDirectory}/{fileName}";
         if (dir.CurrentIsDir())
         {
            var subDirScenes = GetAllScenePaths(filePath);
            foreach (var scene in subDirScenes)
               scenePaths[scene.Key] = scene.Value;
         }
         else if (fileName.EndsWith(".tscn"))
         {
            var sceneName = fileName.GetFile().GetBaseName();
            scenePaths[sceneName] = filePath;
         }
      }
      dir.ListDirEnd();
      return scenePaths;
   }

   private string GetScenePathByName(string sceneName)
   {
      var scenes = GetAllScenePaths();
      return scenes.GetValueOrDefault(sceneName);
   }
}