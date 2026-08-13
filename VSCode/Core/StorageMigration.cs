using System;
using System.IO;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Reprend les donnees du temps ou le mod s'appelait "Ebe1.Profiles".
  ///
  /// FortRise range les sauvegardes d'un mod sous <c>Saves/&lt;nom du mod&gt;</c> et
  /// prefixe ses fichiers du meme nom. Renommer le mod change donc le dossier ET les
  /// noms de fichiers : sans reprise, un joueur retrouverait Archer vide, avec tous
  /// ses profils, ses images et ses sons toujours sur le disque mais introuvables.
  ///
  /// Le dossier est DEPLACE, pas copie. Ce n'est pas un detail : un dossier de
  /// profils bien rempli - sprites de la forge, sons, images - pese des dizaines de
  /// milliers de fichiers, et les copier un par un bloquerait le demarrage du jeu
  /// pendant des minutes, au beau milieu du constructeur du module. Un Directory.Move
  /// sur le meme volume ne fait que renommer une entree : c'est instantane, et
  /// atomique.
  ///
  /// La reprise n'a lieu qu'une fois : elle s'abstient des que le nouveau dossier
  /// contient quoi que ce soit, sinon elle ecraserait le travail fait depuis.
  /// </summary>
  internal static class StorageMigration
  {
    /// <summary>Nom du mod avant le renommage. Ne changera plus jamais.</summary>
    private const string OldName = "Ebe1.Profiles";

    /// <summary>
    /// A appeler AVANT toute lecture de reglages ou de profils - donc au tout debut
    /// du constructeur du module.
    /// </summary>
    /// <param name="newStoragePath">Le dossier de sauvegarde d'aujourd'hui.</param>
    /// <param name="newName">Le nom du mod d'aujourd'hui, prefixe de ses fichiers.</param>
    public static void Run(string newStoragePath, string newName)
    {
      try
      {
        if (string.IsNullOrEmpty(newStoragePath) || OldName == newName)
        {
          return;
        }

        string target = newStoragePath.Replace('/', Path.DirectorySeparatorChar);
        string saves = Path.GetDirectoryName(target);
        if (string.IsNullOrEmpty(saves))
        {
          return;
        }

        string old = Path.Combine(saves, OldName);
        if (!Directory.Exists(old))
        {
          return;
        }

        // FortRise cree le dossier du mod avant nous. Ce qui compte est qu'il soit
        // VIDE : sinon le joueur a deja des donnees sous le nouveau nom, et ce sont
        // elles qui font foi.
        if (Directory.Exists(target))
        {
          if (Directory.GetFiles(target).Length > 0 || Directory.GetDirectories(target).Length > 0)
          {
            return;
          }

          Directory.Delete(target);
        }

        Directory.Move(old, target);
        RenamePrefixedFiles(target, OldName, newName);

        Log.Info($"[Migration] donnees reprises de '{OldName}' vers '{newName}'");
      }
      catch (Exception e)
      {
        // Une reprise ratee laisse le mod demarrer vide, ce qui est desagreable ;
        // une exception ici l'empecherait de demarrer du tout. Les anciennes donnees
        // restent la ou elles sont, et un renommage a la main les retrouve.
        Log.Error($"[Migration] reprise impossible : {e.Message}");
      }
    }

    /// <summary>
    /// Renomme les fichiers dont le nom commence par l'ancien nom du mod.
    ///
    /// C'est ainsi que le jeu les nomme - <c>&lt;mod&gt;.profiles.json</c>,
    /// <c>&lt;mod&gt;.settings.json</c>, <c>&lt;mod&gt;.forge.json</c> - et le code
    /// d'aujourd'hui ne les cherchera que sous le nouveau nom. Seule la RACINE est
    /// parcourue : les sous-dossiers ne contiennent que des images, des sons et des
    /// sprites, dont les noms n'ont jamais dependu de celui du mod.
    /// </summary>
    private static void RenamePrefixedFiles(string directory, string oldName, string newName)
    {
      foreach (string file in Directory.GetFiles(directory))
      {
        string name = Path.GetFileName(file);
        if (!name.StartsWith(oldName, StringComparison.Ordinal))
        {
          continue;
        }

        string renamed = Path.Combine(directory, newName + name.Substring(oldName.Length));
        if (!File.Exists(renamed))
        {
          File.Move(file, renamed);
        }
      }
    }
  }
}
