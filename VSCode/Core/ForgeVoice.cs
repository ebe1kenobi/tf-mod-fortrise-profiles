using System;
using System.Collections.Generic;
using System.IO;

namespace TFModFortRiseArcher
{
  /// <summary>Une action sonore d'archer, et son libelle a l'ecran.</summary>
  public sealed class ForgeVoiceAction
  {
    public readonly string Key;

    /// <summary>
    /// Ce que l'ecran affiche : la cle, ses souligne changes en espaces.
    ///
    /// Derive et non ecrit a cote, pour que l'ecran VOICE de la forge et l'ecran
    /// SOUNDS d'un profil nomment un meme son de la MEME facon - ils lisent tous
    /// deux SoundEvents.Label. Deux libelles a tenir en accord finissent toujours par
    /// diverger, et le fichier exporte porte de toute facon le nom de la cle.
    /// </summary>
    public string Label => SoundEvents.Label(Key);

    /// <summary>
    /// Vrai pour les sons a variantes : le jeu en tire un au hasard. Le chargeur les
    /// cherche numerotes - NOM_01.wav, NOM_02.wav - et non sous leur nom nu.
    /// </summary>
    public readonly bool Varied;

    /// <summary>Vrai pour les sons tenus en boucle tant que l'etat dure.</summary>
    public readonly bool Looped;

    internal ForgeVoiceAction(string key, bool varied = false, bool looped = false)
    {
      Key = key;
      Varied = varied;
      Looped = looped;
    }
  }

  /// <summary>
  /// La voix d'un archer forge.
  ///
  /// Le principe est celui que FortRise applique deja aux mods de contenu, et il
  /// vaut d'etre repris tel quel plutot que reinvente : un archer designe une VOIX
  /// DE REPLI - celle d'un archer du jeu - puis remplace action par action ce qu'il
  /// veut. Chaque son laisse vide retombe sur celui du repli, individuellement. Un
  /// archer sans aucun fichier a donc deja une voix complete, et poser un seul WAV
  /// sur DIE ne coute pas les vingt autres.
  ///
  /// Consequence a connaitre : la valeur de repli par defaut est 0, l'archer vert.
  /// Tous les archers forges parlent donc aujourd'hui avec sa voix, sans que
  /// personne l'ait choisi.
  ///
  /// Les fichiers viennent de la meme banque WAV que les sons de profil - le dossier
  /// `wav` de l'espace de sauvegarde. Une banque unique plutot que deux : ce sont les
  /// memes fichiers, deposes par la meme personne, au meme endroit.
  /// </summary>
  public static class ForgeVoice
  {
    /// <summary>
    /// Les vingt-et-une actions, dans l'ordre ou l'ecran les presente : d'abord ce
    /// qu'on entend le plus souvent, les morts ensuite, l'accessoire a la fin.
    /// </summary>
    public static readonly ForgeVoiceAction[] Actions =
    {
      new ForgeVoiceAction("READY", varied: true),
      new ForgeVoiceAction("JUMP"),
      new ForgeVoiceAction("LAND"),
      new ForgeVoiceAction("DUCK"),
      new ForgeVoiceAction("GRAB"),
      new ForgeVoiceAction("FIRE_ARROW"),
      new ForgeVoiceAction("NOFIRE"),
      new ForgeVoiceAction("AIM"),
      new ForgeVoiceAction("AIM_DIR"),
      new ForgeVoiceAction("AIM_CANCEL"),
      new ForgeVoiceAction("ARROW_GRAB"),
      new ForgeVoiceAction("ARROW_RECOVER"),
      new ForgeVoiceAction("ARROW_STEAL"),
      new ForgeVoiceAction("DIE"),
      new ForgeVoiceAction("DIE_BOMB"),
      new ForgeVoiceAction("DIE_LASER"),
      new ForgeVoiceAction("DIE_STOMP"),
      new ForgeVoiceAction("DIE_ENV"),
      new ForgeVoiceAction("REVIVE"),
      new ForgeVoiceAction("DESELECT"),
      new ForgeVoiceAction("WALLSLIDE_LOOP", looped: true)
    };

    /// <summary>
    /// Les archers du jeu, dans l'ordre de leur identifiant de son. Ce sont les voix
    /// de repli proposees.
    ///
    /// L'index est celui que le jeu emploie dans Sounds.Characters, pas celui de
    /// l'archer : les costumes ALT ont leur propre voix, d'ou les entrees a partir
    /// de dix.
    /// </summary>
    public static readonly (int Id, string Label)[] Fallbacks =
    {
      (0, "GREEN"),
      (1, "BLUE"),
      (2, "PINK"),
      (3, "ORANGE"),
      (4, "WHITE"),
      (5, "YELLOW"),
      (6, "CYAN"),
      (7, "PURPLE"),
      (8, "RED"),
      (10, "GREEN ALT"),
      (11, "BLUE ALT"),
      (12, "PINK ALT"),
      (13, "ORANGE ALT"),
      (14, "WHITE ALT"),
      (15, "YELLOW ALT"),
      (16, "CYAN ALT"),
      (17, "PURPLE ALT"),
      (18, "RED ALT")
    };

    public static ForgeVoiceAction Get(string key)
    {
      foreach (ForgeVoiceAction action in Actions)
      {
        if (string.Equals(action.Key, key, StringComparison.OrdinalIgnoreCase))
        {
          return action;
        }
      }

      return null;
    }

    /// <summary>
    /// Musique de victoire associee a une voix de repli.
    ///
    /// Le jeu ne la designe pas par un nombre mais par un nom - "Green", "Blue" -
    /// qu'il prefixe de "Victory". Et il ne verifie rien : ArcherData.PlayVictoryMusic
    /// interroge un dictionnaire avec ce nom, donc un archer qui n'en declare pas
    /// fait tomber le jeu a la fin du round, sur une cle nulle. Ce n'est pas un
    /// reglage facultatif, c'est un champ obligatoire deguise.
    ///
    /// On la fait suivre la voix : un archer qui parle avec celle du vert gagne avec
    /// sa musique. Une decision de moins a prendre, et un accord plutot qu'un
    /// hasard.
    /// </summary>
    public static string MusicOf(int fallbackId)
    {
      string[] names =
      {
        "Green", "Blue", "Pink", "Orange", "White", "Yellow", "Cyan", "Purple", "Red"
      };

      // Les voix ALT portent le meme numero plus dix. Elles n'ont pas toutes leur
      // musique - seuls l'orange et le blanc en ont une - alors toutes reprennent
      // celle de leur archer de base.
      int index = fallbackId >= 10 ? fallbackId - 10 : fallbackId;

      return index >= 0 && index < names.Length ? names[index] : names[0];
    }

    public static string FallbackLabel(int id)
    {
      foreach (var entry in Fallbacks)
      {
        if (entry.Id == id)
        {
          return entry.Label;
        }
      }

      return id.ToString();
    }

    /// <summary>Voix de repli suivante dans la liste, en bouclant.</summary>
    public static int NextFallback(int current, int direction)
    {
      int index = 0;

      for (int i = 0; i < Fallbacks.Length; i++)
      {
        if (Fallbacks[i].Id == current)
        {
          index = i;
          break;
        }
      }

      index = ((index + direction) % Fallbacks.Length + Fallbacks.Length) % Fallbacks.Length;
      return Fallbacks[index].Id;
    }

    // ------------------------------------------------------------------
    // Fichiers
    // ------------------------------------------------------------------

    /// <summary>Fichier assigne a une action, ou null.</summary>
    public static string FileOf(ForgeDesign design, string action)
    {
      if (design?.Voice == null || action == null)
      {
        return null;
      }

      return design.Voice.TryGetValue(action, out string file) && !string.IsNullOrEmpty(file)
          ? file
          : null;
    }

    /// <summary>Chemin complet du WAV assigne a une action, ou null s'il a disparu.</summary>
    public static string PathOf(ForgeDesign design, string action)
    {
      string file = FileOf(design, action);
      if (file == null)
      {
        return null;
      }

      string path = Path.Combine(ProfileSfx.PoolDir, file);
      return File.Exists(path) ? path : null;
    }

    public static void Assign(ForgeDesign design, string action, string file)
    {
      if (design == null || action == null)
      {
        return;
      }

      design.Voice ??= new Dictionary<string, string>();

      if (string.IsNullOrEmpty(file))
      {
        design.Voice.Remove(action);
      }
      else
      {
        design.Voice[action] = file;
      }
    }

    /// <summary>Combien d'actions ont un fichier a elles.</summary>
    public static int AssignedCount(ForgeDesign design)
    {
      int count = 0;

      foreach (ForgeVoiceAction action in Actions)
      {
        if (PathOf(design, action.Key) != null)
        {
          count++;
        }
      }

      return count;
    }

    /// <summary>
    /// Nom de fichier que l'export doit ecrire pour une action.
    ///
    /// Le chargeur de FortRise ne cherche pas les fichiers sous leur nom d'origine
    /// mais sous le motif declare dans SFX, ou {action} est remplace par la cle. Les
    /// sons a variantes se cherchent en plus numerotes : READY_01.wav et non
    /// READY.wav, sans quoi ils sont ignores sans un mot.
    /// </summary>
    public static string ExportName(ForgeVoiceAction action)
    {
      return action.Varied ? action.Key + "_01.wav" : action.Key + ".wav";
    }
  }
}
