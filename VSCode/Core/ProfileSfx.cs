using System;
using System.Collections.Generic;
using System.IO;
using FortRise;
using Microsoft.Xna.Framework.Audio;
using Monocle;
using TowerFall;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Un fichier du vivier, quelle que soit sa provenance.
  /// </summary>
  public sealed class SoundFile
  {
    public string Name;
    /// <summary>"MOD" pour un son livre avec le mod, "USER" pour un son depose.</summary>
    public string Source;
    private readonly Func<Stream> open;

    internal SoundFile(string name, string source, Func<Stream> open)
    {
      Name = name;
      Source = source;
      this.open = open;
    }

    public Stream Open()
    {
      return open();
    }
  }

  /// <summary>
  /// Les sons des profils : le vivier ou l'on pioche, les copies attachees a chaque
  /// profil, et la lecture en jeu.
  ///
  /// Deux niveaux, volontairement :
  ///
  /// - le <b>vivier</b> reunit les WAV livres avec le mod (Content/wav, en lecture
  ///   seule et potentiellement dans un zip) et ceux que le joueur depose lui-meme
  ///   dans Saves/Profiles/wav ;
  ///
  /// - affecter un son a un profil en <b>copie</b> le fichier sous
  ///   Saves/Profiles/profiles/&lt;PROFIL&gt;/&lt;EVENEMENT&gt;/. Le profil devient ainsi
  ///   autonome : vider le vivier ou desinstaller le mod ne lui retire pas ses sons,
  ///   et le dossier se lit et se modifie a la main.
  ///
  /// Un meme evenement accepte plusieurs fichiers ; la lecture en tire un au hasard.
  /// </summary>
  public static class ProfileSfx
  {
    private const string PoolDirName = "wav";
    private const string ProfilesDirName = "profiles";
    private const string ModPoolPath = "Content/wav";
    private const string WavExtension = ".wav";

    /// <summary>Chance qu'un son marque "de temps en temps" soit retenu pour un evenement.</summary>
    private const float OccasionalChance = 0.25f;

    /// <summary>Un son decode, et le fichier dont il vient - c'est lui qui porte le reglage.</summary>
    private sealed class LoadedSound
    {
      public string File;
      public SoundEffect Effect;
    }

    // <PROFIL> -> <EVENEMENT> -> sons deja decodes
    private static readonly Dictionary<string, Dictionary<string, List<LoadedSound>>> bank =
        new Dictionary<string, Dictionary<string, List<LoadedSound>>>(StringComparer.OrdinalIgnoreCase);

    private static List<SoundFile> pool;

    private static string StorageRoot => TFModFortRiseArcherModule.Instance.Context.Storage.StoragePath;

    public static string PoolDir => Path.Combine(StorageRoot, PoolDirName);

    public static string DirOf(string profileName)
    {
      return Path.Combine(StorageRoot, ProfilesDirName, SoundEvents.Sanitize(profileName));
    }

    public static string DirOf(string profileName, string soundEvent)
    {
      return Path.Combine(DirOf(profileName), soundEvent);
    }

    // ------------------------------------------------------------------
    // Vivier
    // ------------------------------------------------------------------

    /// <summary>
    /// Les WAV disponibles a l'affectation. Relu a chaque appel de
    /// <see cref="RefreshPool"/> : le joueur depose ses fichiers pendant que le jeu
    /// tourne, un cache definitif l'obligerait a relancer.
    /// </summary>
    public static IReadOnlyList<SoundFile> Pool
    {
      get
      {
        if (pool == null)
        {
          RefreshPool();
        }

        return pool;
      }
    }

    public static void RefreshPool()
    {
      var found = new List<SoundFile>();
      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      // Les fichiers deposes par le joueur passent en premier : a nom egal, le sien
      // l'emporte sur celui livre avec le mod.
      try
      {
        if (Directory.Exists(PoolDir))
        {
          foreach (string path in Directory.GetFiles(PoolDir, "*" + WavExtension))
          {
            string name = Path.GetFileName(path);
            if (seen.Add(name))
            {
              string captured = path;
              found.Add(new SoundFile(name, "USER", () => File.OpenRead(captured)));
            }
          }
        }
        else
        {
          // Cree le dossier des le premier passage : le joueur doit pouvoir y deposer
          // ses fichiers sans avoir a deviner le chemin ni a le creer lui-meme.
          Directory.CreateDirectory(PoolDir);
        }
      }
      catch (Exception e)
      {
        Log.Error($"[Sfx] vivier utilisateur illisible : {e.Message}");
      }

      try
      {
        IModContent content = TFModFortRiseArcherModule.Instance.ModContent;
        if (content != null && content.TryGetResource(ModPoolPath, out IResourceInfo root) && root?.Childrens != null)
        {
          foreach (IResourceInfo child in root.Childrens)
          {
            if (child.Name.EndsWith(WavExtension, StringComparison.OrdinalIgnoreCase) && seen.Add(child.Name))
            {
              IResourceInfo captured = child;
              found.Add(new SoundFile(child.Name, "MOD", () => captured.Stream));
            }
          }
        }
      }
      catch (Exception e)
      {
        Log.Error($"[Sfx] vivier du mod illisible : {e.Message}");
      }

      found.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
      pool = found;
    }

    // ------------------------------------------------------------------
    // Affectation
    // ------------------------------------------------------------------

    public static bool IsAssigned(string profileName, string soundEvent, string fileName)
    {
      try
      {
        return File.Exists(Path.Combine(DirOf(profileName, soundEvent), fileName));
      }
      catch
      {
        return false;
      }
    }

    public static int CountAssigned(string profileName, string soundEvent)
    {
      try
      {
        string dir = DirOf(profileName, soundEvent);
        return Directory.Exists(dir) ? Directory.GetFiles(dir, "*" + WavExtension).Length : 0;
      }
      catch
      {
        return 0;
      }
    }

    /// <summary>Copie le fichier du vivier dans le dossier du profil.</summary>
    public static bool Assign(string profileName, string soundEvent, SoundFile file)
    {
      try
      {
        string dir = DirOf(profileName, soundEvent);
        Directory.CreateDirectory(dir);

        using (Stream source = file.Open())
        using (FileStream target = File.Create(Path.Combine(dir, file.Name)))
        {
          source.CopyTo(target);
        }

        Invalidate(profileName);
        return true;
      }
      catch (Exception e)
      {
        Log.Error($"[Sfx] copie de {file.Name} impossible : {e.Message}");
        return false;
      }
    }

    public static bool Unassign(string profileName, string soundEvent, string fileName)
    {
      try
      {
        string path = Path.Combine(DirOf(profileName, soundEvent), fileName);
        if (File.Exists(path))
        {
          File.Delete(path);
        }

        Invalidate(profileName);
        return true;
      }
      catch (Exception e)
      {
        Log.Error($"[Sfx] suppression de {fileName} impossible : {e.Message}");
        return false;
      }
    }

    // ------------------------------------------------------------------
    // Frequence
    // ------------------------------------------------------------------

    public static bool IsOccasional(ProfileData profile, string soundEvent, string fileName)
    {
      if (profile?.OccasionalSounds == null)
      {
        return false;
      }

      foreach (OccasionalSound entry in profile.OccasionalSounds)
      {
        if (string.Equals(entry.Event, soundEvent, StringComparison.OrdinalIgnoreCase)
            && string.Equals(entry.File, fileName, StringComparison.OrdinalIgnoreCase))
        {
          return true;
        }
      }

      return false;
    }

    public static void SetOccasional(ProfileData profile, string soundEvent, string fileName, bool occasional)
    {
      if (profile == null)
      {
        return;
      }

      profile.OccasionalSounds ??= new List<OccasionalSound>();

      profile.OccasionalSounds.RemoveAll(entry =>
          string.Equals(entry.Event, soundEvent, StringComparison.OrdinalIgnoreCase)
          && string.Equals(entry.File, fileName, StringComparison.OrdinalIgnoreCase));

      if (occasional)
      {
        profile.OccasionalSounds.Add(new OccasionalSound { Event = soundEvent, File = fileName });
      }

      if (profile.OccasionalSounds.Count == 0)
      {
        profile.OccasionalSounds = null;
      }
    }

    /// <summary>
    /// Suit un renommage de profil. Le dossier porte le nom du profil pour rester
    /// lisible ; sans ce deplacement, renommer laisserait les sons derriere.
    /// </summary>
    public static void MoveProfileFolder(string oldName, string newName)
    {
      try
      {
        string from = DirOf(oldName);
        string to = DirOf(newName);

        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(from))
        {
          return;
        }

        if (Directory.Exists(to))
        {
          // Le nom est deja pris sur le disque alors que les profils sont uniques :
          // il s'agit d'un reliquat. On ne l'ecrase pas.
          Log.Error($"[Sfx] {to} existe deja, les sons de {oldName} restent en place");
          return;
        }

        Directory.Move(from, to);
        Invalidate(oldName);
        Invalidate(newName);
      }
      catch (Exception e)
      {
        Log.Error($"[Sfx] renommage du dossier de sons impossible : {e.Message}");
      }
    }

    public static void DeleteProfileFolder(string profileName)
    {
      try
      {
        string dir = DirOf(profileName);
        if (Directory.Exists(dir))
        {
          Directory.Delete(dir, true);
        }

        Invalidate(profileName);
      }
      catch (Exception e)
      {
        Log.Error($"[Sfx] suppression du dossier de sons impossible : {e.Message}");
      }
    }

    // ------------------------------------------------------------------
    // Lecture
    // ------------------------------------------------------------------

    public static void Invalidate(string profileName)
    {
      bank.Remove(SoundEvents.Sanitize(profileName));
    }

    public static void InvalidateAll()
    {
      bank.Clear();
    }

    /// <summary>
    /// Joue un son de l'evenement, tire au hasard s'il y en a plusieurs.
    /// </summary>
    /// <returns>
    /// Vrai si un son a ete joue, ce qui indique a l'appelant de ne pas jouer celui
    /// du jeu.
    /// </returns>
    public static bool TryPlay(ProfileData profile, string soundEvent, float volume)
    {
      if (profile == null || string.IsNullOrEmpty(profile.Name) || string.IsNullOrEmpty(soundEvent))
      {
        return false;
      }

      List<LoadedSound> sounds = Sounds(profile.Name, soundEvent);
      if (sounds == null || sounds.Count == 0)
      {
        return false;
      }

      // Les sons "de temps en temps" ne concourent que s'ils passent leur tirage. Si
      // aucun ne reste, on rend faux et le son du jeu se fait entendre a la place -
      // c'est precisement l'effet recherche : une réplique qui ne sort pas a tous
      // les coups.
      var eligible = new List<LoadedSound>(sounds.Count);
      foreach (LoadedSound sound in sounds)
      {
        if (sound.Effect == null)
        {
          continue;
        }

        if (IsOccasional(profile, soundEvent, sound.File) && Calc.Random.NextFloat() >= OccasionalChance)
        {
          continue;
        }

        eligible.Add(sound);
      }

      if (eligible.Count == 0)
      {
        return false;
      }

      SoundEffect effect = eligible[Calc.Random.Next(eligible.Count)].Effect;
      if (effect == null)
      {
        return false;
      }

      // Volume a zero : il y a bien un son attache, on rend vrai pour que le son du
      // jeu ne le remplace pas au moment ou le joueur a coupe le son.
      if (Audio.MasterVolume <= 0f)
      {
        return true;
      }

      // Pan neutre : les WAV des profils sont des voix, pas des bruits situes dans le
      // niveau. Les latereliser sur la position du joueur les rend inaudibles d'un
      // cote de l'ecran.
      effect.Play(volume * Audio.MasterVolume, Audio.MasterPitch, 0f);
      return true;
    }

    /// <summary>
    /// Vrai si l'evenement a au moins un son attache. Ne tient pas compte du tirage
    /// des sons occasionnels : c'est une question de contenu, pas de hasard.
    /// </summary>
    public static bool Has(ProfileData profile, string soundEvent)
    {
      if (profile == null || string.IsNullOrEmpty(profile.Name))
      {
        return false;
      }

      List<LoadedSound> sounds = Sounds(profile.Name, soundEvent);
      return sounds != null && sounds.Count > 0;
    }

    /// <summary>
    /// Les sons d'un evenement, decodes a la demande et gardes en memoire ensuite.
    /// Charger tout au demarrage obligerait a relancer le jeu apres chaque
    /// affectation ; charger a chaque lecture ferait un acces disque en plein match.
    /// </summary>
    private static List<LoadedSound> Sounds(string profileName, string soundEvent)
    {
      string key = SoundEvents.Sanitize(profileName);

      if (!bank.TryGetValue(key, out var events))
      {
        events = new Dictionary<string, List<LoadedSound>>(StringComparer.OrdinalIgnoreCase);
        bank[key] = events;
      }

      if (events.TryGetValue(soundEvent, out var cached))
      {
        return cached;
      }

      var loaded = new List<LoadedSound>();

      try
      {
        string dir = DirOf(profileName, soundEvent);
        if (Directory.Exists(dir))
        {
          foreach (string path in Directory.GetFiles(dir, "*" + WavExtension))
          {
            try
            {
              using FileStream stream = File.OpenRead(path);
              loaded.Add(new LoadedSound
              {
                File = Path.GetFileName(path),
                Effect = SoundEffect.FromStream(stream)
              });
            }
            catch (Exception e)
            {
              // Un WAV illisible ne doit pas priver l'evenement de ses autres sons.
              Log.Error($"[Sfx] {Path.GetFileName(path)} illisible : {e.Message}");
            }
          }
        }
      }
      catch (Exception e)
      {
        Log.Error($"[Sfx] lecture de {profileName}/{soundEvent} impossible : {e.Message}");
      }

      events[soundEvent] = loaded;
      return loaded;
    }
  }
}
