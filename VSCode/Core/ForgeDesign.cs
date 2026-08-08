using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TFModFortRiseProfiles
{
  /// <summary>Une image du vivier : la planche dont elle vient, et sa case.</summary>
  public class ForgePick
  {
    /// <summary>Nom du repertoire du vivier, donc de la planche source.</summary>
    public string Source { get; set; } = "";

    public int Col { get; set; }
    public int Row { get; set; }

    [JsonIgnore]
    public ForgeCell Cell => new ForgeCell(Col, Row);

    public static ForgePick Of(string source, ForgeCell cell)
    {
      return new ForgePick { Source = source, Col = cell.Col, Row = cell.Row };
    }

    public override string ToString()
    {
      return Source + "/" + Cell;
    }
  }

  /// <summary>
  /// Un archer en cours de forge, tel qu'il est serialise dans Profiles.forge.json.
  ///
  /// Le dessin ne contient aucune image : seulement d'ou chaque pose vient. Les
  /// planches se refabriquent a chaque chargement, ce qui parait couteux et ne l'est
  /// pas - seize decoupes de vingt-quatre pixels - et ce qui evite surtout d'avoir
  /// deux verites, le dessin et les PNG, qui finiraient par diverger. C'est le meme
  /// choix que la table de couleurs des profils, pour la meme raison.
  ///
  /// Meme format additif que ProfileData : System.Text.Json ignore ce qu'il ne
  /// connait pas, un fichier ecrit par une version plus recente reste donc lisible.
  /// </summary>
  public class ForgeDesign
  {
    /// <summary>Identifiant stable, independant du nom.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Nom court, sans espace : il sert d'identifiant de sprite et de nom de fichier
    /// a l'export. Le nom affiche dans le jeu, lui, tient sur les deux lignes.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>Premiere ligne du nom affiche - "INTREPID" chez Brones.</summary>
    public string Name0 { get; set; } = "";

    /// <summary>Seconde ligne du nom affiche - "ARCHAEOLOGIST" chez Brones.</summary>
    public string Name1 { get; set; } = "";

    /// <summary>Couleur principale, en hexadecimal RRGGBB. Elle porte le nom et les fleches.</summary>
    public string ColorA { get; set; } = "8B5A2B";

    /// <summary>Couleur secondaire, en hexadecimal RRGGBB. Elle porte la plupart des effets.</summary>
    public string ColorB { get; set; } = "C08040";

    /// <summary>
    /// Planche qui a pre-rempli les emplacements, gardee pour pouvoir y revenir.
    ///
    /// Elle ne fait pas foi : ce sont les choix de <see cref="Picks"/> qui comptent.
    /// Une pose retouchee ne doit pas revenir a celle de la source parce qu'on a
    /// rouvert l'ecran.
    /// </summary>
    public string Source { get; set; } = "";

    /// <summary>
    /// Fenetre de decoupe dans la case source, calee sur le personnage debout.
    ///
    /// Reglable parce que toutes les planches ne calent pas leur personnage au meme
    /// endroit, et parce qu'un pixel d'ecart se voit : le personnage flotte ou
    /// s'enfonce dans le sol.
    /// </summary>
    public int WindowX { get; set; } = ForgeSlots.WindowX;

    public int WindowY { get; set; } = ForgeSlots.WindowY;

    /// <summary>
    /// Teinte appliquee aux pieces reprises du jeu - arc, viseur, ailes, gemmes -
    /// en degres. Broforce n'a pas d'arc, et un arc vert sur un archer brun se
    /// verrait immediatement.
    /// </summary>
    public float Hue { get; set; } = 30f;

    /// <summary>
    /// D'ou vient chaque pose, par cle d'emplacement.
    ///
    /// Forme ancienne, conservee pour relire les dessins faits avant les calques :
    /// une pose n'y a qu'une image. <see cref="Layers"/> la remplace, et
    /// <see cref="Migrate"/> transporte l'une dans l'autre au chargement.
    /// </summary>
    public Dictionary<string, ForgePick> Picks { get; set; } = new Dictionary<string, ForgePick>();

    /// <summary>
    /// Les images qui composent chaque pose, dans l'ordre ou elles se superposent.
    ///
    /// Une pose est rarement une seule image : Broforce dessine les bras sur une
    /// planche a part, parce qu'ils s'animent independamment du corps. Un archer
    /// fabrique a partir du seul corps sort donc manchot, et aucun reglage de
    /// fenetre n'y change quoi que ce soit - l'image n'existe pas dans cette case.
    ///
    /// D'ou l'empilement : la premiere image choisie est le fond, chaque suivante se
    /// pose par-dessus. L'ordre est celui de la selection, et il compte - un bras
    /// derriere le corps n'est pas un bras devant.
    /// </summary>
    public Dictionary<string, List<ForgePick>> Layers { get; set; }
        = new Dictionary<string, List<ForgePick>>();

    /// <summary>
    /// Identifiant du dessin dont celui-ci est le costume ALT, ou vide.
    ///
    /// Un costume ALT n'ajoute pas une case au rollcall : il occupe l'emplacement de
    /// son parent et se choisit avec la bascule ALT. C'est ainsi que sont faits les
    /// neuf archers du jeu, et c'est ce qui manquait le plus a la forge.
    ///
    /// Volontairement facultatif : un archer seul reste un archer normal, et rien ne
    /// force a en faire une paire.
    /// </summary>
    public string AltOf { get; set; } = "";

    /// <summary>
    /// Voix de repli : l'archer du jeu dont les sons comblent ce qui n'est pas
    /// fourni. Zero est le vert - c'est la valeur que tous les archers forges
    /// avaient sans l'avoir choisie.
    /// </summary>
    public int VoiceFallback { get; set; }

    /// <summary>
    /// Fichier de la banque WAV assigne a chaque action, par cle d'action. Ce qui
    /// n'y figure pas est joue avec la voix de repli.
    /// </summary>
    public Dictionary<string, string> Voice { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Musique de victoire, ou vide pour la faire suivre la voix de repli.
    ///
    /// Facultative comme le reste, mais jamais absente a l'arrivee : le jeu lit ce
    /// champ sans le verifier, et vide il fait tomber la fin du round. Voir
    /// <see cref="ForgeMusic"/>.
    /// </summary>
    public string VictoryMusic { get; set; } = ForgeMusic.Auto;

    /// <summary>Vrai si ce dessin est le costume ALT d'un autre.</summary>
    [JsonIgnore]
    public bool IsAlt => !string.IsNullOrEmpty(AltOf);

    /// <summary>
    /// Compte des modifications depuis le chargement.
    ///
    /// Sert uniquement a l'apercu, qui garde les planches assemblees en memoire : il
    /// faut bien qu'il sache quand les refaire. Non serialise - un numero de version
    /// enregistre ne voudrait rien dire d'un lancement a l'autre.
    /// </summary>
    [JsonIgnore]
    public int Revision { get; private set; }

    /// <summary>
    /// A appeler apres toute modification qui change l'image de l'archer : les noms
    /// et les couleurs n'en font pas partie, la fenetre de decoupe et les poses si.
    /// </summary>
    public void Touch()
    {
      Revision++;
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// Reprend les dessins ecrits avant les calques : chaque pose y devient un
    /// empilement d'une seule image. A appeler une fois apres la lecture du fichier.
    /// </summary>
    public void Migrate()
    {
      Layers ??= new Dictionary<string, List<ForgePick>>();

      if (Picks == null)
      {
        return;
      }

      foreach (var pair in Picks)
      {
        if (pair.Value != null && !Layers.ContainsKey(pair.Key))
        {
          Layers[pair.Key] = new List<ForgePick> { pair.Value };
        }
      }

      Picks.Clear();
    }

    /// <summary>Les images d'une pose, du fond vers le dessus. Jamais null.</summary>
    public List<ForgePick> LayersOf(string slotKey)
    {
      if (slotKey == null || Layers == null)
      {
        return new List<ForgePick>();
      }

      return Layers.TryGetValue(slotKey, out List<ForgePick> stack) && stack != null
          ? stack
          : new List<ForgePick>();
    }

    /// <summary>
    /// La premiere image d'une pose, ou null.
    ///
    /// Ce que voient les vignettes et l'apercu de survol, qui n'ont pas besoin de
    /// l'empilement entier pour dire de quelle planche vient la pose.
    /// </summary>
    public ForgePick PickOf(string slotKey)
    {
      List<ForgePick> stack = LayersOf(slotKey);
      return stack.Count == 0 ? null : stack[0];
    }

    /// <summary>Remplace la pose par une seule image, ou la vide si null.</summary>
    public void Set(string slotKey, ForgePick pick)
    {
      Layers ??= new Dictionary<string, List<ForgePick>>();

      if (pick == null)
      {
        Layers.Remove(slotKey);
      }
      else
      {
        Layers[slotKey] = new List<ForgePick> { pick };
      }

      Touch();
    }

    /// <summary>Ajoute une image par-dessus les precedentes.</summary>
    public void AddLayer(string slotKey, ForgePick pick)
    {
      if (pick == null)
      {
        return;
      }

      Layers ??= new Dictionary<string, List<ForgePick>>();

      if (!Layers.TryGetValue(slotKey, out List<ForgePick> stack) || stack == null)
      {
        stack = new List<ForgePick>();
        Layers[slotKey] = stack;
      }

      stack.Add(pick);
      Touch();
    }

    /// <summary>Retire la derniere image posee. La pose se vide avec la premiere.</summary>
    public void RemoveTopLayer(string slotKey)
    {
      if (Layers == null || !Layers.TryGetValue(slotKey, out List<ForgePick> stack) || stack == null)
      {
        return;
      }

      if (stack.Count > 0)
      {
        stack.RemoveAt(stack.Count - 1);
      }

      if (stack.Count == 0)
      {
        Layers.Remove(slotKey);
      }

      Touch();
    }

    /// <summary>
    /// Pose les seize emplacements aux coordonnees canoniques d'une planche.
    /// </summary>
    /// <param name="keepEdits">
    /// Vrai pour ne remplir que ce qui est vide. C'est ce qu'on veut quand on change
    /// de source apres avoir deja retouche : une correction faite a la main ne doit
    /// pas se perdre parce qu'on a essaye une autre planche.
    /// </param>
    public void Prefill(ForgeSource source, bool keepEdits = false)
    {
      if (source == null)
      {
        return;
      }

      Layers ??= new Dictionary<string, List<ForgePick>>();
      Source = source.Name;

      foreach (ForgeSlot slot in ForgeSlots.All)
      {
        if (keepEdits && Layers.ContainsKey(slot.Key))
        {
          continue;
        }

        ForgeCell? cell = ForgeLayout.Of(slot.Key);
        if (cell == null)
        {
          continue;
        }

        // Une case vide n'a pas de fichier. On laisse l'emplacement vide plutot que
        // d'y inscrire une reference qui ne mene nulle part : c'est ce qui permet a
        // l'ecran de dire ce qui reste a faire.
        if (!ForgeBank.Has(source, cell.Value))
        {
          Layers.Remove(slot.Key);
          continue;
        }

        // Le pre-remplissage repose une pose entiere : il remplace l'empilement au
        // lieu de s'y ajouter, sans quoi changer de source empilerait deux
        // personnages l'un sur l'autre.
        Layers[slot.Key] = new List<ForgePick> { ForgePick.Of(source.Name, cell.Value) };
      }

      Touch();
    }

    /// <summary>Les emplacements encore vides, dans l'ordre de la liste.</summary>
    public List<ForgeSlot> Missing()
    {
      var missing = new List<ForgeSlot>();

      foreach (ForgeSlot slot in ForgeSlots.All)
      {
        if (PickOf(slot.Key) == null)
        {
          missing.Add(slot);
        }
      }

      return missing;
    }

    /// <summary>
    /// Vrai si l'archer peut etre fabrique.
    ///
    /// La pose debout est la seule reellement indispensable : elle sert de portrait,
    /// de statue et de repli pour toute pose manquante. Un archer sans esquive est
    /// laid, un archer sans pose debout n'existe pas.
    /// </summary>
    [JsonIgnore]
    public bool Buildable => !string.IsNullOrWhiteSpace(Name) && PickOf("stand") != null;
  }
}
