using System;
using System.Collections.Generic;
using System.IO;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// La musique de victoire d'un archer forge.
  ///
  /// Le champ parait facultatif - rien ne le reclame dans la configuration d'archer -
  /// mais le jeu l'emploie comme cle de dictionnaire sans la verifier :
  /// ArcherData.PlayVictoryMusic fait tomber la fin du round sur une cle nulle. C'est
  /// un champ obligatoire deguise, et c'est pourquoi il y a toujours une valeur meme
  /// quand l'utilisateur n'a rien choisi.
  ///
  /// D'ou le cran AUTO, qui est le defaut : la musique suit alors la voix de repli.
  /// Un archer qui parle avec celle du vert gagne avec sa musique, sans avoir eu a
  /// le decider.
  ///
  /// Seules les musiques du jeu sont proposees. En apporter une a soi demanderait de
  /// la declarer comme ressource de mod, ce que l'essai a chaud ne sait pas faire -
  /// la piste se joue depuis un flux que seul le chargeur de contenu sait ouvrir.
  /// </summary>
  public static class ForgeMusic
  {
    /// <summary>Valeur du dessin quand la musique suit la voix.</summary>
    public const string Auto = "";

    /// <summary>
    /// Les treize pistes de victoire du jeu, sous le nom qu'il leur donne. Le jeu
    /// les prefixe lui-meme de "Victory" pour trouver le fichier.
    /// </summary>
    public static readonly (string Key, string Label)[] Tracks =
    {
      ("Green", "GREEN"),
      ("Blue", "BLUE"),
      ("Pink", "PINK"),
      ("Orange", "ORANGE"),
      ("OrangeAlt", "ORANGE ALT"),
      ("White", "WHITE"),
      ("WhiteAlt", "WHITE ALT"),
      ("Yellow", "YELLOW"),
      ("Cyan", "CYAN"),
      ("Purple", "PURPLE"),
      ("Red", "RED"),
      ("Kyle", "KYLE"),
      ("Team", "TEAM")
    };

    // ------------------------------------------------------------------
    // Musiques apportees
    // ------------------------------------------------------------------

    private const string BankDirName = "music";

    /// <summary>
    /// Marque un choix qui designe un fichier de la banque et non une piste du jeu.
    /// Un prefixe plutot qu'un second champ : le reglage reste une seule liste a
    /// faire defiler, ce qui evite d'avoir a choisir d'abord entre deux origines.
    /// </summary>
    public const string FilePrefix = "file:";

    /// <summary>Ou deposer ses musiques, a cote de la banque de sons.</summary>
    public static string BankDir
    {
      get
      {
        string root = TFModFortRiseArcherModule.Instance.Context.Storage.StoragePath;
        return Path.Combine(root, BankDirName);
      }
    }

    /// <summary>
    /// Les fichiers deposes, par nom. WAV et OGG : ce sont les deux formats que le
    /// jeu sait lire pour une piste.
    ///
    /// Les DEUX banques sont lues, celle des musiques et celle des sons. Un jingle de
    /// victoire est un WAV comme un autre, et obliger a le recopier dans un second
    /// dossier ne servirait qu'a expliquer pourquoi il y a deux dossiers. A nom egal,
    /// celui de la banque de musiques l'emporte - c'est la qu'on l'a range expres.
    /// </summary>
    public static List<string> BankFiles()
    {
      var files = new List<string>();
      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      Collect(BankDir, files, seen, create: true);
      Collect(ProfileSfx.PoolDir, files, seen, create: false);

      files.Sort(StringComparer.OrdinalIgnoreCase);
      return files;
    }

    private static void Collect(string directory, List<string> into, HashSet<string> seen, bool create)
    {
      try
      {
        if (!Directory.Exists(directory))
        {
          if (create)
          {
            // Cree au premier passage : personne ne doit deviner le chemin.
            Directory.CreateDirectory(directory);
          }

          return;
        }

        foreach (string path in Directory.GetFiles(directory))
        {
          string extension = Path.GetExtension(path).ToLowerInvariant();

          if (extension != ".wav" && extension != ".ogg")
          {
            continue;
          }

          string name = Path.GetFileName(path);

          if (seen.Add(name))
          {
            into.Add(name);
          }
        }
      }
      catch (Exception e)
      {
        Log.Error($"[Forge] banque de musiques illisible : {e.Message}");
      }
    }

    /// <summary>
    /// Chemin complet d'un fichier de musique, dans l'une ou l'autre banque, ou null.
    /// </summary>
    public static string FindFile(string file)
    {
      if (string.IsNullOrEmpty(file))
      {
        return null;
      }

      foreach (string directory in new[] { BankDir, ProfileSfx.PoolDir })
      {
        string path = Path.Combine(directory, file);

        if (File.Exists(path))
        {
          return path;
        }
      }

      return null;
    }

    public static bool IsFile(string value)
    {
      return !string.IsNullOrEmpty(value) && value.StartsWith(FilePrefix, StringComparison.Ordinal);
    }

    public static string FileNameOf(string value)
    {
      return IsFile(value) ? value.Substring(FilePrefix.Length) : null;
    }

    /// <summary>Chemin complet du fichier choisi, ou null s'il a disparu.</summary>
    public static string FilePathOf(ForgeDesign design)
    {
      return FindFile(design == null ? null : FileNameOf(design.VictoryMusic));
    }

    /// <summary>
    /// Chemin que le mod exporte doit citer. Le chargeur d'archer enregistre
    /// lui-meme la piste quand le chemin existe dans le mod : il n'y a pas de
    /// content.json a ecrire.
    /// </summary>
    public static string ExportPath(ForgeDesign design)
    {
      string file = FilePathOf(design) == null ? null : FileNameOf(design.VictoryMusic);
      return file == null ? null : "Content/Music/" + file;
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// La piste a inscrire dans l'archer enregistre a chaud. Jamais vide : c'est ce
    /// qui evite la cle nulle.
    ///
    /// Une musique apportee n'est PAS jouable ici. Elle doit etre declaree comme
    /// ressource de mod, et une ressource se construit a partir d'un contenu de mod
    /// que l'essai a chaud n'a pas. L'archer essaye gagne donc sur la piste du jeu
    /// que AUTO aurait choisie ; c'est l'export qui rend la vraie.
    /// </summary>
    public static string Resolve(ForgeDesign design)
    {
      if (design == null)
      {
        return "Green";
      }

      // Une piste enregistree qui n'existe plus dans la liste - un fichier edite a
      // la main - vaut mieux etre traitee comme AUTO que passee telle quelle.
      if (!string.IsNullOrEmpty(design.VictoryMusic) && IsKnown(design.VictoryMusic))
      {
        return design.VictoryMusic;
      }

      return ForgeVoice.MusicOf(design.VoiceFallback);
    }

    public static bool IsKnown(string key)
    {
      foreach (var track in Tracks)
      {
        if (string.Equals(track.Key, key, StringComparison.OrdinalIgnoreCase))
        {
          return true;
        }
      }

      return false;
    }

    /// <summary>Ce que la fiche affiche : le choix, ou la piste que AUTO designe.</summary>
    public static string Label(ForgeDesign design)
    {
      if (design == null)
      {
        return "AUTO";
      }

      if (IsFile(design.VictoryMusic))
      {
        string file = FileNameOf(design.VictoryMusic);

        // Un fichier disparu de la banque doit se voir : il ne sera pas exporte, et
        // laisser son nom laisserait croire le contraire.
        if (FilePathOf(design) == null)
        {
          return "FILE MISSING";
        }

        // Le mot dit ce que l'essai a chaud ne pourra pas faire entendre, la ou on
        // le decide - plutot que de le laisser decouvrir en testant.
        return Path.GetFileNameWithoutExtension(file).ToUpperInvariant() + " (EXPORT)";
      }

      if (string.IsNullOrEmpty(design.VictoryMusic) || !IsKnown(design.VictoryMusic))
      {
        return "AUTO (" + LabelOf(ForgeVoice.MusicOf(design.VoiceFallback)) + ")";
      }

      return LabelOf(design.VictoryMusic);
    }

    private static string LabelOf(string key)
    {
      foreach (var track in Tracks)
      {
        if (string.Equals(track.Key, key, StringComparison.OrdinalIgnoreCase))
        {
          return track.Label;
        }
      }

      return key;
    }

    /// <summary>
    /// Choix suivant : AUTO, puis les pistes du jeu, puis les fichiers deposes. Une
    /// seule liste plutot que deux, pour n'avoir pas a choisir d'abord une origine.
    /// </summary>
    public static string Next(string current, int direction)
    {
      var cycle = new List<string> { Auto };

      foreach (var track in Tracks)
      {
        cycle.Add(track.Key);
      }

      foreach (string file in BankFiles())
      {
        cycle.Add(FilePrefix + file);
      }

      int position = cycle.FindIndex(
          value => string.Equals(value, current ?? Auto, StringComparison.OrdinalIgnoreCase));

      if (position < 0)
      {
        position = 0;
      }

      int next = ((position + direction) % cycle.Count + cycle.Count) % cycle.Count;
      return cycle[next];
    }
  }
}
