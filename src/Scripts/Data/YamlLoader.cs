using YamlDotNet.Serialization.NamingConventions;
using FileAccess = Godot.FileAccess;

namespace Arcomage.Data;

public static class YamlLoader
{
   public static IDeserializer Create(bool withActionConverter = true)
   {
      var builder = new DeserializerBuilder()
         .WithNamingConvention(CamelCaseNamingConvention.Instance);

      if (withActionConverter)
         builder = builder.WithTypeConverter(new ActionTypeConverter());

      return builder.Build();
   }

   public static string ReadFile(string filePath)
   {
      using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
      var yaml = file.GetAsText();
      file.Close();
      return yaml;
   }
}
