using System;
using System.IO;
using FortRise;
using Monocle;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// La musique de victoire d'un profil.
  ///
  /// Elle se choisit comme celle d'un archer forge - les memes pistes du jeu, la meme
  /// banque de fichiers, le meme encodage dans le fichier de sauvegarde - mais elle ne
  /// se range pas au meme endroit et ne repond pas a la meme question. Celle de
  /// l'archer dit "voila ce que joue ce personnage" ; celle du profil dit "voila ce que
  /// joue Eric, quel que soit le personnage qu'il a pris". C'est la seconde qui gagne
  /// quand les deux existent.
  ///
  /// A ne pas confondre avec le son WIN de l'ecran des sons : celui-la est une voix,
  /// une reaction courte jouee par-dessus ce qui tourne. Celle-ci REMPLACE la piste de
  /// fin de manche.
  /// </summary>
  internal static class ProfileMusic
  {
    /// <summary>
    /// Prefixe des pistes que ce mod inscrit dans la table de FortRise. Un nom qui ne
    /// peut appartenir a personne d'autre : la table est commune a tous les mods.
    /// </summary>
    private const string TrackPrefix = "Archer/Victory/";

    /// <summary>Choix suivant : AUTO, les pistes du jeu, puis les fichiers deposes.</summary>
    public static string Next(string current, int direction)
    {
      return ForgeMusic.Next(current, direction);
    }

    /// <summary>
    /// Ce que la fiche affiche. "ARCHER" et non "AUTO" : le profil ne devine rien, il
    /// laisse simplement la main a la musique de l'archer choisi.
    /// </summary>
    public static string Label(ProfileData profile)
    {
      string value = profile?.VictoryMusic;

      if (string.IsNullOrEmpty(value))
      {
        return "ARCHER";
      }

      if (ForgeMusic.IsFile(value))
      {
        string file = ForgeMusic.FileNameOf(value);

        // Un fichier disparu de la banque doit se voir : la musique de l'archer
        // reprendra sans qu'on l'ait demande.
        return ForgeMusic.FindFile(file) != null
            ? Path.GetFileNameWithoutExtension(file).ToUpperInvariant()
            : "FILE MISSING";
      }

      foreach (var track in ForgeMusic.Tracks)
      {
        if (string.Equals(track.Key, value, StringComparison.OrdinalIgnoreCase))
        {
          return track.Label;
        }
      }

      return "ARCHER";
    }

    /// <summary>
    /// Joue la musique du profil. Rend faux quand il n'y en a pas : l'appelant laisse
    /// alors le jeu jouer celle de l'archer.
    /// </summary>
    public static bool Play(ProfileData profile)
    {
      string value = profile?.VictoryMusic;

      if (string.IsNullOrEmpty(value))
      {
        return false;
      }

      try
      {
        if (!ForgeMusic.IsFile(value))
        {
          if (!ForgeMusic.IsKnown(value))
          {
            return false;
          }

          Music.PlayImmediate("Victory" + value);
          return true;
        }

        string track = Register(ForgeMusic.FileNameOf(value));

        if (track == null)
        {
          return false;
        }

        // Comme le fait FortRise pour une piste de mod : le jingle ne boucle pas, et
        // la musique d'attente prend la suite quand il se termine. Sans elle, l'ecran
        // de resultats resterait dans le silence.
        Music.PlayImmediate(track, false);
        Music.PlayNext("TheArchives", true);
        return true;
      }
      catch (Exception e)
      {
        Log.Error($"[Music] musique de victoire non jouee : {e.Message}");
        return false;
      }
    }

    /// <summary>
    /// Inscrit un fichier de la banque dans la table des pistes de FortRise, et rend
    /// le nom sous lequel Music le connait desormais. Null si le fichier a disparu.
    ///
    /// Passer par la table plutot que d'ouvrir le flux nous-memes : c'est elle qui
    /// tient l'enchainement des pistes, la coupure de la precedente et le volume. Une
    /// piste jouee a cote continuerait par-dessus la musique du menu suivant.
    /// </summary>
    private static string Register(string file)
    {
      if (string.IsNullOrEmpty(file))
      {
        return null;
      }

      string name = TrackPrefix + file;

      if (Music.TrackMap.ContainsKey(name))
      {
        return name;
      }

      string path = ForgeMusic.FindFile(file);

      if (path == null)
      {
        return null;
      }

      // Une ressource sans mod proprietaire : rien dans la lecture d'une piste ne
      // remonte au mod, seuls le flux et le type de fichier sont lus. Ce type est
      // pose a la main, la methode qui le deduit de l'extension n'etant pas publique.
      var resource = new FileResourceInfo(null, file, path)
      {
        ResourceType = path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)
            ? typeof(RiseCore.ResourceTypeOggFile)
            : typeof(RiseCore.ResourceTypeWavFile)
      };

      Music.TrackMap[name] = new TrackInfo(name, resource);
      return name;
    }
  }
}
