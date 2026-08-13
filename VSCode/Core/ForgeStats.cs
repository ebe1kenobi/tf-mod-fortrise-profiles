using System;
using TowerFall;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Rallonge les compteurs indexes par archer.
  ///
  /// C'est le meme piege que celui des particules et celui des voix, et c'est le
  /// troisieme endroit ou il se paie. Le motif merite d'etre nomme une fois pour
  /// toutes : dans ce jeu, tout ce qui est indexe par archer est un tableau
  /// dimensionne au chargement, et personne n'y verifie ses bornes. Ajouter un
  /// archer a chaud - ce que fait la forge quand elle essaie - donne un index qui
  /// sort de chacun d'eux.
  ///
  /// Ces tableaux-ci ne font tomber le jeu ni a la selection ni a l'apparition, mais
  /// a la PREMIERE MORT et en fin de match : les endroits ou l'on croit le moins
  /// avoir affaire a un probleme de sprite.
  ///
  /// On rallonge plutot que de reconstruire : Initialize() les refabriquerait vides
  /// et effacerait les statistiques de la partie en cours.
  /// </summary>
  public static class ForgeStats
  {
    /// <summary>
    /// Met les compteurs a la taille du nombre d'archers.
    ///
    /// Sans effet si rien n'a change : appelable apres chaque enregistrement sans
    /// avoir a tenir le compte de ce qui a deja ete fait.
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

        // Statistiques de session : lues a chaque mort par SessionStats.
        Grow(ref SessionStats.ArcherPlays, count);
        Grow(ref SessionStats.ArcherKills, count);
        Grow(ref SessionStats.ArcherDeaths, count);
        Grow(ref SessionStats.ArcherSelfKills, count);
        Grow(ref SessionStats.ArcherWins, count);

        GameStats stats = SaveData.Instance?.Stats;

        if (stats == null)
        {
          return;
        }

        // Victoires par archer, ecrites a la construction de l'ecran de resultats.
        // Celui-ci survit a la partie : il est enregistre dans la sauvegarde, et le
        // jeu le remet a la bonne taille au chargement suivant.
        Grow(ref stats.Wins, count);

        // Recompenses de fin de match : un tableau par recompense, indexe par
        // archer. VersusAwards y ecrit sans borne au depouillement.
        if (stats.Awards != null)
        {
          for (int i = 0; i < stats.Awards.Length; i++)
          {
            Grow(ref stats.Awards[i], count);
          }
        }
      }
      catch (Exception e)
      {
        Log.Error($"[Forge] compteurs non rallonges : {e}");
      }
    }

    /// <summary>
    /// Un tableau absent veut dire que le jeu ne l'a pas encore construit. Le
    /// fabriquer ici le ferait ecraser juste apres : on laisse faire.
    /// </summary>
    private static void Grow<T>(ref T[] array, int count)
    {
      if (array == null || array.Length >= count)
      {
        return;
      }

      Array.Resize(ref array, count);
    }
  }
}
