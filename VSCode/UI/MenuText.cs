using System.Collections.Generic;
using System.Text;
using TowerFall;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Retire d'un texte les caracteres que la police du jeu ne sait pas dessiner.
  ///
  /// SpriteFont.MeasureString ne les ignore pas : il leve une exception, et comme la
  /// mesure a lieu pendant le rendu, c'est tout le jeu qui tombe. Un simple signe
  /// degre ou un nom de fichier accentue suffit.
  ///
  /// Le filtre est donc pose au seul endroit par lequel passent tous les libelles de
  /// ce mod, plutot que de traquer les caracteres fautifs un par un - une liste qu'on
  /// n'a aucun moyen de tenir a jour, puisqu'elle depend aussi des noms de fichiers
  /// que le joueur depose dans ses viviers.
  /// </summary>
  internal static class MenuText
  {
    private static HashSet<char> known;

    public static string Safe(string text)
    {
      if (string.IsNullOrEmpty(text))
      {
        return text;
      }

      HashSet<char> allowed = Known();
      if (allowed == null)
      {
        return text;
      }

      // Chemin rapide : l'immense majorite des libelles sont deja dessinables.
      bool clean = true;
      foreach (char c in text)
      {
        if (!allowed.Contains(c))
        {
          clean = false;
          break;
        }
      }

      if (clean)
      {
        return text;
      }

      var builder = new StringBuilder(text.Length);
      foreach (char c in text)
      {
        if (allowed.Contains(c))
        {
          builder.Append(c);
        }
      }

      return builder.ToString();
    }

    private static HashSet<char> Known()
    {
      if (known != null)
      {
        return known;
      }

      var font = TFGame.Font;
      if (font?.Characters == null)
      {
        return null;
      }

      known = new HashSet<char>(font.Characters);
      return known;
    }
  }
}
