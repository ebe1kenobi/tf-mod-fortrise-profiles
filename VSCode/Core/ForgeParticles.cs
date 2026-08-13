using System;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Rallonge les tableaux de particules indexes par archer.
  ///
  /// Voici le piege, et il merite d'etre ecrit en entier parce qu'il ne se voit pas.
  ///
  /// Particles.Load() construit six familles dimensionnees sur le nombre d'archers
  /// du moment : poussiere, esquive, saut hyper, plumes, lueur et trainee de fleche
  /// laser. Un archer enregistre APRES ce chargement - c'est exactement ce que fait
  /// la forge quand elle essaie a chaud - porte un index qui sort de ces tableaux.
  ///
  /// Le jeu ne tombe alors ni a la selection ni a l'apparition, mais a la premiere
  /// esquive. C'est une panne facile a manquer en essai : on choisit son archer, on
  /// le voit courir, on croit que tout va bien, et il disparait des qu'on appuie sur
  /// le bouton d'esquive.
  ///
  /// Les nouvelles entrees sont des copies de celles d'un archer existant, dont on
  /// deplace les teintes vers celles du nouveau. On ne peut pas refaire l'appel
  /// d'origine : les prototypes que Particles.Load lui passait sont des variables
  /// locales, perdues depuis longtemps. Copier puis reteinter conserve tout le reste
  /// - taille, duree, gravite, fondu - ce qui est precisement ce qu'on veut garder.
  /// </summary>
  public static class ForgeParticles
  {
    /// <summary>
    /// Met les six familles a la taille du nombre d'archers, en teintant les entrees
    /// ajoutees aux couleurs de l'archer correspondant.
    ///
    /// Sans effet si rien n'a change : la fonction peut donc etre appelee apres
    /// chaque enregistrement sans avoir a tenir le compte de ce qui a deja ete fait.
    /// </summary>
    public static void Extend()
    {
      try
      {
        int count = ArcherData.Archers?.Length ?? 0;

        if (count == 0)
        {
          return;
        }

        // Ces six lignes recopient, argument pour argument, les appels que
        // Particles.Load fait a GetArcherParticleTypes. Elles n'ont donc l'air
        // arbitraires que tant qu'on ne les met pas cote a cote : un multiplicateur
        // change ici donnerait a un archer forge des particules plus pales ou plus
        // vives que celles de tous les autres.
        Grow(ref Particles.Dash, count, true, 1f, false, 1f);
        Grow(ref Particles.HyperJump, count, true, 0.8f, false, 0.8f);
        Grow(ref Particles.LaserArrowGlow, count, true, 1f, false, 1f);
        Grow(ref Particles.LaserArrowTrail, count, true, 1f, false, 1f);
        Grow(ref Particles.PlayerDust, count, true, 1f, true, 0.5f);
        Grow(ref Particles.PlayerFeathers, count, true, 1f, true, 0.5f);
      }
      catch (Exception e)
      {
        Log.Error($"[Forge] particules non rallongees : {e}");
      }
    }

    private static void Grow(
        ref ParticleType[] family,
        int count,
        bool firstLight,
        float firstMult,
        bool secondLight,
        float secondMult)
    {
      // Un tableau absent veut dire que Particles.Load n'a pas encore tourne. Le
      // fabriquer ici le ferait ecraser juste apres, avec des prototypes inventes :
      // mieux vaut ne rien faire et laisser le jeu construire les siens.
      if (family == null || family.Length == 0 || family.Length >= count)
      {
        return;
      }

      ParticleType prototype = family[0];
      int from = family.Length;
      Array.Resize(ref family, count);

      for (int i = from; i < count; i++)
      {
        ArcherData archer = ArcherData.Archers[i];

        // Un emplacement encore vide - un archer enregistre dont la donnee n'est pas
        // posee - prend le prototype tel quel. Il vaut mieux une particule de la
        // mauvaise couleur qu'une reference nulle a l'esquive suivante.
        if (archer == null)
        {
          family[i] = new ParticleType(prototype);
          continue;
        }

        family[i] = new ParticleType(prototype)
        {
          Color = (firstLight ? archer.ColorB : archer.ColorA) * firstMult,
          Color2 = (secondLight ? archer.ColorA : archer.ColorB) * secondMult
        };
      }
    }
  }
}
