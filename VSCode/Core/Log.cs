using Microsoft.Extensions.Logging;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Journal du mod.
  ///
  /// FortRise 5 fournit un ILogger par mod : ses lignes atterrissent dans le journal
  /// du launcher, prefixees du nom du mod. Pas de fichier a ouvrir soi-meme, et
  /// surtout pas de champ statique a initialiser avant le premier appel - un logger
  /// non initialise ici ne fait que perdre le message, il ne fait pas planter
  /// l'ecran qui l'appelait.
  /// </summary>
  internal static class Log
  {
    internal static ILogger Backend;

    public static void Info(string message)
    {
      Backend?.LogInformation("{message}", message);
    }

    public static void Error(string message)
    {
      Backend?.LogError("{message}", message);
    }
  }
}
