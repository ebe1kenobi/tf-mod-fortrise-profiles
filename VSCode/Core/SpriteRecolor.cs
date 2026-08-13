using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using TowerFall;

namespace TFModFortRiseArcher
{
  /// <summary>Une couleur presente dans le sprite, et le nombre de pixels qu'elle occupe.</summary>
  public sealed class PaletteColor
  {
    public Color Source;
    public int Count;
  }

  /// <summary>
  /// Recoloration du sprite d'archer d'un profil.
  ///
  /// Le principe est celui que le jeu emploie deja pour les couleurs d'equipe : il ne
  /// teinte pas le sprite, il lui substitue une autre texture via
  /// <c>Sprite.SwapSubtexture</c>. On fabrique donc, par profil, une texture
  /// autonome recoloree, et on la substitue de la meme facon. L'atlas du jeu, partage
  /// par tous les joueurs, n'est jamais modifie.
  ///
  /// SwapSubtexture decale les rectangles de frames du delta entre l'ancienne et la
  /// nouvelle origine : une texture independante calee en (0,0) et de la taille de la
  /// region d'origine convient donc telle quelle, sans recalcul d'animation.
  /// </summary>
  public static class SpriteRecolor
  {
    public const string Body = "BODY";
    public const string Head = "HEAD";
    public const string HeadNoHat = "HEAD_NOHAT";
    public const string HeadCrown = "HEAD_CROWN";
    public const string HeadBack = "HEAD_BACK";
    public const string Bow = "BOW";
    public const string Hat = "HAT";
    public const string Corpse = "CORPSE";

    /// <summary>
    /// La fleche encochee, visible des qu'on vise. Le jeu l'appelle l'aimer : c'est une
    /// image unique et non un sprite anime, elle pivote simplement dans la direction
    /// visee.
    /// </summary>
    public const string Aim = "AIM";

    private const string SpriteDirName = "sprite";

    // <id de profil> -> <partie> -> texture recoloree
    private static readonly Dictionary<string, Dictionary<string, Subtexture>> baked =
        new Dictionary<string, Dictionary<string, Subtexture>>();

    // <id de profil> -> palette, pour ne pas relire l'atlas a chaque image du menu
    private static readonly Dictionary<string, List<PaletteColor>> palettes =
        new Dictionary<string, List<PaletteColor>>();

    // ------------------------------------------------------------------
    // Sources dans l'atlas du jeu
    // ------------------------------------------------------------------

    /// <summary>
    /// Region d'atlas d'une partie du sprite, ou null si l'archer n'a pas cette partie.
    ///
    /// Corps, tetes et arc sont des sprites animes : leur texture se lit dans la
    /// definition SpriteData. Le chapeau, lui, est une sous-texture simple portee par
    /// l'ArcherData - c'est celui qui s'envole quand on se fait toucher, une entite a
    /// part entiere.
    /// </summary>
    public static Subtexture SourceOf(ArcherData archer, string part)
    {
      if (archer == null)
      {
        return null;
      }

      try
      {
        if (part == Hat)
        {
          return archer.Hat.Normal;
        }

        // La fleche encochee est une sous-texture simple, comme le chapeau : une seule
        // image, portee directement par l'ArcherData.
        if (part == Aim)
        {
          return archer.Aimer;
        }

        // Le cadavre a sa propre table de sprites, distincte de celle du jeu, et son
        // identifiant est un nom d'archer ("Green", "Pink_Alt") et non un id de sprite.
        if (part == Corpse)
        {
          if (string.IsNullOrEmpty(archer.Corpse))
          {
            return null;
          }

          XmlElement corpseXml = TFGame.CorpseSpriteData.GetXML(archer.Corpse);
          return corpseXml == null ? null : TFGame.Atlas[corpseXml.ChildText("Texture")];
        }

        string spriteId = part switch
        {
          Body => archer.Sprites.Body,
          Head => archer.Sprites.HeadNormal,
          HeadNoHat => archer.Sprites.HeadNoHat,
          HeadCrown => archer.Sprites.HeadCrown,
          HeadBack => archer.Sprites.HeadBack,
          Bow => archer.Sprites.Bow,
          _ => null
        };

        if (string.IsNullOrEmpty(spriteId))
        {
          return null;
        }

        XmlElement xml = TFGame.SpriteData.GetXML(spriteId);
        if (xml == null)
        {
          return null;
        }

        return TFGame.Atlas[xml.ChildText("Texture")];
      }
      catch (Exception e)
      {
        Log.Error($"[Recolor] source {part} introuvable : {e.Message}");
        return null;
      }
    }

    public static IEnumerable<string> AllParts()
    {
      yield return Body;
      yield return Head;
      yield return HeadNoHat;
      yield return HeadCrown;
      yield return HeadBack;
      yield return Bow;
      yield return Aim;
      yield return Hat;
      yield return Corpse;
    }

    // ------------------------------------------------------------------
    // Palette
    // ------------------------------------------------------------------

    /// <summary>
    /// Couleurs presentes dans le sprite, les plus etendues d'abord.
    ///
    /// Une seule palette pour tout l'archer et non une par partie : la meme teinte
    /// habille le corps et la tete, la changer deux fois n'aurait pas de sens.
    /// </summary>
    /// <param name="parts">
    /// Planches a examiner. Restreindre la palette aux parties qu'on modifie evite
    /// d'y faire figurer des teintes qu'on ne pourra pas atteindre.
    /// </param>
    public static List<PaletteColor> Palette(ProfileData profile, IReadOnlyList<string> parts = null)
    {
      if (profile == null)
      {
        return new List<PaletteColor>();
      }

      parts ??= new List<string>(AllParts());
      string key = profile.Id + "|" + string.Join(",", parts);

      if (palettes.TryGetValue(key, out var cached))
      {
        return cached;
      }

      // Le relevé lui-meme ne connait pas les profils : voir ColorPalette.Of, que la
      // forge emploie sur des planches qui ne viennent pas de l'atlas.
      var sources = new List<Color[]>();

      foreach (string part in parts)
      {
        sources.Add(SourcePixels(profile, part));
      }

      List<PaletteColor> palette = ColorPalette.Of(sources);
      palettes[key] = palette;
      return palette;
    }

    /// <summary>Pixels d'origine d'une planche de l'archer, avant tout remplacement.</summary>
    public static Color[] SourcePixels(ProfileData profile, string part)
    {
      return ReadPixels(SourceOf(ArcherCatalog.DataOf(profile), part));
    }

    private static Color[] ReadPixels(Subtexture subtexture)
    {
      if (subtexture == null)
      {
        return null;
      }

      try
      {
        Texture2D texture = subtexture.Texture2D;
        Rectangle rect = subtexture.Rect;
        if (texture == null || texture.IsDisposed || rect.Width <= 0 || rect.Height <= 0)
        {
          return null;
        }

        var pixels = new Color[rect.Width * rect.Height];

        // La region et la longueur du tampon doivent s'accorder : c'est exactement
        // le desaccord de ce genre qui fait ecrire FNA3D hors des clous.
        texture.GetData(0, rect, pixels, 0, pixels.Length);
        return pixels;
      }
      catch (Exception e)
      {
        Log.Error($"[Recolor] lecture de l'atlas impossible : {e.Message}");
        return null;
      }
    }

    // ------------------------------------------------------------------
    // Fabrication
    // ------------------------------------------------------------------

    /// <summary>
    /// Texture recoloree d'une partie, ou null si le profil ne change aucune couleur -
    /// auquel cas l'appelant garde celle du jeu.
    /// </summary>
    /// <param name="fromDiskFirst">
    /// Vrai en jeu : la copie PNG enregistree dans le profil fait foi, elle est
    /// modifiable hors du jeu. Faux dans l'ecran d'edition, ou la table de couleurs
    /// est la seule verite en cours - relire le PNG y reafficherait l'etat du passage
    /// precedent au lieu de la retouche qu'on vient de faire.
    /// </param>
    public static Subtexture Baked(ProfileData profile, string part, bool fromDiskFirst = true)
    {
      if (profile == null || !HasSwaps(profile))
      {
        return null;
      }

      // La cle porte l'essai actif : changer d'essai doit donner d'autres textures,
      // pas ressortir celles du precedent.
      string bakeKey = BakeKey(profile);

      if (!baked.TryGetValue(bakeKey, out var parts))
      {
        parts = new Dictionary<string, Subtexture>();
        baked[bakeKey] = parts;
      }

      if (parts.TryGetValue(part, out Subtexture cached))
      {
        return cached;
      }

      Subtexture built = fromDiskFirst
          ? BuildFromDisk(profile, part) ?? BuildFromAtlas(profile, part)
          : BuildFromAtlas(profile, part);

      parts[part] = built;
      return built;
    }

    /// <summary>
    /// Recharge la copie enregistree dans le profil. C'est elle qui sert en jeu :
    /// le PNG est la reference, modifiable a la main hors du jeu.
    /// </summary>
    private static Subtexture BuildFromDisk(ProfileData profile, string part)
    {
      try
      {
        string path = PngPath(profile, part);
        if (path == null || !File.Exists(path))
        {
          return null;
        }

        using FileStream stream = File.OpenRead(path);
        Texture2D texture = Texture2D.FromStream(Engine.Instance.GraphicsDevice, stream);
        return texture == null ? null : new Subtexture(new Monocle.Texture(texture));
      }
      catch (Exception e)
      {
        Log.Error($"[Recolor] {part}.png illisible : {e.Message}");
        return null;
      }
    }

    private static Subtexture BuildFromAtlas(ProfileData profile, string part)
    {
      Subtexture source = SourceOf(ArcherCatalog.DataOf(profile), part);
      Color[] pixels = ReadPixels(source);
      if (pixels == null)
      {
        return null;
      }

      Dictionary<uint, Color> map = SwapMap(profile, part);
      ColorTrial trial = ProfileTrials.Active(profile);
      bool adjust = trial != null && trial.HasAdjustments;
      bool changed = adjust;

      for (int i = 0; i < pixels.Length; i++)
      {
        if (pixels[i].A == 0)
        {
          continue;
        }

        if (map.TryGetValue(pixels[i].PackedValue, out Color replacement))
        {
          // L'alpha d'origine est conserve : les bords adoucis du pixel art le
          // portent, et le remplacer par 255 ferait apparaitre un lisere dur.
          replacement.A = pixels[i].A;
          pixels[i] = replacement;
          changed = true;
        }

        // Les reglages d'ensemble viennent apres le remplacement, et sur tout le
        // sprite : ils portent sur le rendu final, pas sur les teintes d'origine.
        if (adjust)
        {
          pixels[i] = ColorAdjust.Apply(pixels[i], trial);
        }
      }

      if (!changed)
      {
        return null;
      }

      try
      {
        var texture = new Texture2D(Engine.Instance.GraphicsDevice, source.Rect.Width, source.Rect.Height);
        texture.SetData(pixels);
        return new Subtexture(new Monocle.Texture(texture));
      }
      catch (Exception e)
      {
        Log.Error($"[Recolor] fabrication de {part} impossible : {e.Message}");
        return null;
      }
    }

    /// <summary>
    /// Couleur dominante du personnage recolore, ou null si le profil ne change rien -
    /// auquel cas l'appelant garde la couleur d'archer du jeu.
    ///
    /// Prise sur le corps, qui porte l'essentiel de la surface. On saute les teintes
    /// sombres et desaturees : ce sont les contours et les ombres, souvent les plus
    /// etendues, et rendre un empennage noir n'apprendrait rien sur le joueur.
    /// </summary>
    public static Color? DominantColor(ProfileData profile)
    {
      if (!HasSwaps(profile))
      {
        return null;
      }

      List<PaletteColor> palette = Palette(profile, new[] { Body });
      if (palette.Count == 0)
      {
        return null;
      }

      ColorTrial trial = ProfileTrials.Active(profile);
      Dictionary<uint, Color> map = SwapMap(profile, Body);
      Color? fallback = null;

      foreach (PaletteColor entry in palette)
      {
        Color final = map.TryGetValue(entry.Source.PackedValue, out Color replacement)
            ? replacement
            : entry.Source;

        final = ColorAdjust.Apply(final, trial);
        fallback ??= final;

        string family = ColorFamilies.NameOf(final);
        if (family != ColorFamilies.Dark && family != ColorFamilies.Neutral)
        {
          return final;
        }
      }

      // Personnage entierement gris ou noir : sa teinte la plus etendue reste le
      // meilleur choix disponible.
      return fallback;
    }

    public static bool HasSwaps(ProfileData profile)
    {
      ColorTrial trial = ProfileTrials.Active(profile);
      return trial != null && !trial.IsEmpty;
    }

    /// <summary>
    /// Remplacements qui s'appliquent a une planche donnee.
    ///
    /// Une entree sans partie vaut pour toutes : c'est la forme ancienne, conservee
    /// pour que les profils faits avant la recoloration par partie continuent de
    /// s'afficher comme avant.
    /// </summary>
    private static Dictionary<uint, Color> SwapMap(ProfileData profile, string part)
    {
      var map = new Dictionary<uint, Color>();
      if (profile == null)
      {
        return map;
      }

      ColorTrial trial = ProfileTrials.Active(profile);
      if (trial?.Palette == null)
      {
        return map;
      }

      foreach (ColorSwap swap in trial.Palette)
      {
        if (!string.IsNullOrEmpty(swap.Part)
            && !string.Equals(swap.Part, part, StringComparison.OrdinalIgnoreCase))
        {
          continue;
        }

        if (TryParse(swap.From, out Color from) && TryParse(swap.To, out Color to))
        {
          map[from.PackedValue] = to;
        }
      }

      return map;
    }

    /// <summary>
    /// Rend explicites les remplacements sans partie, en les recopiant sur chaque
    /// planche.
    ///
    /// Sans cette conversion, retoucher une teinte partagee par la tete et le corps
    /// obligerait a choisir entre ecraser l'ancien reglage global ou le laisser
    /// primer : dans les deux cas la partie qu'on ne modifie pas bougerait. Une fois
    /// les entrees explicites, chaque planche a la sienne et ne depend plus des
    /// autres.
    /// </summary>
    public static void MakePartsExplicit(ProfileData profile)
    {
      ColorTrial trial = ProfileTrials.Active(profile);
      if (trial?.Palette == null)
      {
        return;
      }

      var legacy = trial.Palette.FindAll(swap => string.IsNullOrEmpty(swap.Part));
      if (legacy.Count == 0)
      {
        return;
      }

      trial.Palette.RemoveAll(swap => string.IsNullOrEmpty(swap.Part));

      foreach (ColorSwap swap in legacy)
      {
        foreach (string part in AllParts())
        {
          trial.Palette.Add(new ColorSwap { Part = part, From = swap.From, To = swap.To });
        }
      }

      Invalidate(profile);
    }

    /// <summary>
    /// Cle de cache des textures : le profil et l'essai actif. Deux essais du meme
    /// profil ne doivent pas se partager des textures.
    /// </summary>
    private static string BakeKey(ProfileData profile)
    {
      ColorTrial trial = ProfileTrials.Active(profile);
      return profile.Id + "|" + (trial?.Name ?? "");
    }

    // ------------------------------------------------------------------
    // Hexadecimal
    // ------------------------------------------------------------------

    public static string ToHex(Color color)
    {
      return $"{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    public static bool TryParse(string hex, out Color color)
    {
      color = Color.White;
      if (string.IsNullOrEmpty(hex))
      {
        return false;
      }

      hex = hex.Trim().TrimStart('#');
      if (hex.Length != 6)
      {
        return false;
      }

      try
      {
        color = new Color(
            Convert.ToInt32(hex.Substring(0, 2), 16),
            Convert.ToInt32(hex.Substring(2, 2), 16),
            Convert.ToInt32(hex.Substring(4, 2), 16));
        return true;
      }
      catch
      {
        return false;
      }
    }

    // ------------------------------------------------------------------
    // Persistance
    // ------------------------------------------------------------------

    public static string SpriteDir(ProfileData profile)
    {
      return Path.Combine(ProfileSfx.DirOf(profile.Name), SpriteDirName);
    }

    /// <summary>
    /// Dossier des images d'un essai : un niveau par archer, un par costume, un par
    /// essai.
    ///
    /// C'est ce decoupage qui permet de changer d'archer sans rien perdre : les images
    /// de l'ancien restent a leur place, et celles du nouveau se rangent a cote au
    /// lieu de les ecraser.
    /// </summary>
    public static string TrialDir(ProfileData profile, ColorTrial trial)
    {
      if (trial == null)
      {
        return null;
      }

      return Path.Combine(
          SpriteDir(profile),
          SoundEvents.Sanitize(trial.Archer),
          SoundEvents.Sanitize(trial.Costume),
          SoundEvents.Sanitize(trial.Name));
    }

    private static string PngPath(ProfileData profile, string part)
    {
      string dir = TrialDir(profile, ProfileTrials.Active(profile));
      return dir == null ? null : Path.Combine(dir, part + ".png");
    }

    /// <summary>
    /// Ecrit dans le profil une copie PNG de chaque partie recoloree. C'est cette
    /// copie que la partie utilisera, et elle reste ouvrable dans n'importe quel
    /// editeur d'image.
    /// </summary>
    public static void Export(ProfileData profile)
    {
      if (profile == null)
      {
        return;
      }

      try
      {
        ColorTrial trial = ProfileTrials.Active(profile);
        string dir = TrialDir(profile, trial);

        // Aucun essai actif : il n'y a pas de dossier a tenir a jour, et surtout rien
        // a effacer - les essais inactifs gardent leurs images.
        if (dir == null)
        {
          Invalidate(profile);
          return;
        }

        if (!HasSwaps(profile))
        {
          // Plus aucune couleur changee : les copies deviendraient un fantome qui
          // continuerait de s'appliquer en jeu.
          if (Directory.Exists(dir))
          {
            Directory.Delete(dir, true);
          }

          Invalidate(profile);
          return;
        }

        Directory.CreateDirectory(dir);

        foreach (string part in AllParts())
        {
          // On repart de l'atlas et non du cache : le cache peut deja contenir un
          // PNG relu, le reexporter le figerait sur une palette perimee.
          Subtexture built = BuildFromAtlas(profile, part);
          string path = PngPath(profile, part);

          if (built == null)
          {
            if (File.Exists(path))
            {
              File.Delete(path);
            }

            continue;
          }

          using (FileStream stream = File.Create(path))
          {
            built.Texture2D.SaveAsPng(stream, built.Rect.Width, built.Rect.Height);
          }

          built.Texture2D.Dispose();
        }

        Invalidate(profile);
      }
      catch (Exception e)
      {
        Log.Error($"[Recolor] export impossible : {e.Message}");
      }
    }

    /// <summary>
    /// Oublie les textures fabriquees pour ce profil, en liberant celles du GPU :
    /// l'ecran d'edition refabrique a chaque couleur changee, sans cela chaque
    /// retouche laisserait une texture derriere elle.
    /// </summary>
    public static void Invalidate(ProfileData profile)
    {
      if (profile == null)
      {
        return;
      }

      // Les textures sont indexees par profil ET par essai : il faut balayer toutes
      // les cles du profil, pas seulement celle de l'essai courant.
      string prefix = profile.Id + "|";
      var stale = new List<string>();

      foreach (var pair in baked)
      {
        if (!pair.Key.StartsWith(prefix, StringComparison.Ordinal))
        {
          continue;
        }

        stale.Add(pair.Key);

        foreach (Subtexture subtexture in pair.Value.Values)
        {
          try { subtexture?.Texture2D?.Dispose(); } catch { }
        }
      }

      foreach (string key in stale)
      {
        baked.Remove(key);
      }
    }

    /// <summary>
    /// Oublie aussi la palette relevee dans l'atlas. A n'appeler que si l'archer du
    /// profil change : la palette decrit le sprite nu, elle ne bouge pas quand on
    /// remplace des couleurs, et la relire couterait une lecture GPU a chaque
    /// retouche.
    /// </summary>
    public static void Forget(ProfileData profile)
    {
      if (profile == null)
      {
        return;
      }

      // Les palettes sont indexees par profil ET par selection de planches : il y en a
      // donc plusieurs par profil, une par combinaison deja consultee.
      string prefix = profile.Id + "|";
      var stale = new List<string>();

      foreach (string key in palettes.Keys)
      {
        if (key.StartsWith(prefix, StringComparison.Ordinal))
        {
          stale.Add(key);
        }
      }

      foreach (string key in stale)
      {
        palettes.Remove(key);
      }

      Invalidate(profile);
    }
  }
}
