using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Le profil vu par les ecrans de couleur : l'essai actif, et les planches de
  /// l'archer du jeu qu'il a choisi.
  ///
  /// Une facade et non un deplacement de code : <see cref="SpriteRecolor"/> continue
  /// de faire le travail, avec ses caches et son export sur disque. Ce qui change est
  /// que les ecrans ne s'adressent plus a lui directement, et acceptent donc un autre
  /// sujet.
  /// </summary>
  public sealed class ProfileColorSubject : IColorSubject
  {
    private readonly ProfileData profile;
    private readonly ColorTrial trial;

    public ProfileColorSubject(ProfileData profile, ColorTrial trial)
    {
      this.profile = profile;
      this.trial = trial;
    }

    public ProfileData Profile => profile;

    public ColorTrial Trial => trial;

    public IReadOnlyList<string> Groups => SpritePartGroups.All;

    public IEnumerable<string> PartsOf(string group)
    {
      return SpritePartGroups.PartsOf(group);
    }

    public Color[] SourcePixels(string part)
    {
      return SpriteRecolor.SourcePixels(profile, part);
    }

    public void Invalidate()
    {
      SpriteRecolor.Invalidate(profile);
    }
  }
}
