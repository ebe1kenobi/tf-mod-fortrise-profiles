using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace TFModFortRiseProfiles
{
  /// <summary>
  /// Types de particules recolores aux couleurs d'un profil.
  ///
  /// Le jeu construit six familles indexees par archer - poussiere, esquive, saut
  /// hyper, plumes, lueur et trainee de fleche laser - toutes teintees a partir des
  /// couleurs de l'ArcherData. Deux joueurs sur le meme archer partagent donc le meme
  /// objet : on ne peut pas le modifier en place sans toucher l'autre.
  ///
  /// Chaque profil recoit donc sa propre copie, durable. C'est indispensable et pas
  /// seulement propre : une particule relit les couleurs de son type pendant toute sa
  /// vie, une teinte posee puis retiree ressortirait sur la moitie des particules en
  /// vol.
  ///
  /// La recoloration reprend le decalage des familles de teinte : la couleur de
  /// reference de l'archer devient la couleur dominante du profil, et les teintes du
  /// type suivent le meme ecart. Les rapports d'origine sont ainsi conserves, sans
  /// avoir a connaitre les multiplicateurs avec lesquels le jeu les a fabriquees.
  /// </summary>
  public static class ProfileParticles
  {
    private sealed class Entry
    {
      public Color Dominant;
      public readonly Dictionary<ParticleType, ParticleType> Types = new Dictionary<ParticleType, ParticleType>();
    }

    private static readonly Dictionary<string, Entry> cache = new Dictionary<string, Entry>();

    /// <summary>
    /// Version recoloree d'un type partage, ou le type lui-meme si le profil ne change
    /// rien.
    /// </summary>
    public static ParticleType For(ProfileData profile, ParticleType shared, int characterIndex)
    {
      if (shared == null || profile == null)
      {
        return shared;
      }

      try
      {
        Color? dominant = SpriteRecolor.DominantColor(profile);
        if (dominant == null)
        {
          return shared;
        }

        if (!cache.TryGetValue(profile.Id, out Entry entry) || entry.Dominant != dominant.Value)
        {
          // La couleur dominante a change : les copies precedentes sont perimees.
          entry = new Entry { Dominant = dominant.Value };
          cache[profile.Id] = entry;
        }

        if (entry.Types.TryGetValue(shared, out ParticleType built))
        {
          return built;
        }

        Color reference = ReferenceColor(characterIndex);

        built = new ParticleType(shared)
        {
          Color = Shift(shared.Color, reference, dominant.Value),
          Color2 = Shift(shared.Color2, reference, dominant.Value)
        };

        entry.Types[shared] = built;
        return built;
      }
      catch (Exception e)
      {
        Log.Error($"[Particles] recoloration impossible : {e.Message}");
        return shared;
      }
    }

    /// <summary>
    /// Teinte dont ces particules sont tirees. ColorB est celle que le jeu emploie le
    /// plus souvent pour les effets ; a defaut d'archer valide, on rend du blanc, ce
    /// qui laisse le decalage sans effet plutot que de le fausser.
    /// </summary>
    private static Color ReferenceColor(int characterIndex)
    {
      if (!ArcherCatalog.Ready || characterIndex < 0 || characterIndex >= ArcherData.Archers.Length)
      {
        return Color.White;
      }

      return ArcherData.Archers[characterIndex]?.ColorB ?? Color.White;
    }

    /// <summary>
    /// Decale une teinte, en preservant sa transparence : les particules s'appuient
    /// dessus pour leur fondu, et la perdre les rendrait opaques.
    /// </summary>
    private static Color Shift(Color source, Color reference, Color target)
    {
      Color shifted = ColorFamilies.Shift(source, reference, target);
      shifted.A = source.A;
      return shifted;
    }
  }
}
