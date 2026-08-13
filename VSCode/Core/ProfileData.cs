using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using TowerFall;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Un profil de joueur, tel qu'il est serialise dans Profiles.profiles.json.
  ///
  /// Le format est volontairement additif : les rubriques a venir (sons par
  /// evenement, portrait personnalise, palette) s'ajouteront en proprietes
  /// optionnelles. System.Text.Json ignore les proprietes qu'il ne connait pas,
  /// donc un fichier ecrit par une version plus recente reste lisible par une
  /// version plus ancienne, et inversement les nouvelles proprietes prennent
  /// simplement leur valeur par defaut.
  /// </summary>
  public class ProfileData
  {
    /// <summary>
    /// Identifiant stable, independant du nom.
    ///
    /// C'est lui qui nommera le dossier d'assets du profil quand les sons et les
    /// images arriveront : renommer un profil ne doit pas orpheliner ses fichiers.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Nom affiche, en majuscules, borne par <see cref="MaxNameLength"/>.</summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Nom de l'archer prefere, sous la forme "NAME0 NAME1" (ex : "GREEN ARCHER").
    ///
    /// C'est cette reference qui fait foi et non l'index : ArcherData.Archers est
    /// reconstruit a chaque lancement, et n'importe quel mod d'archers en decale le
    /// contenu. Un profil doit survivre a l'ajout ou au retrait d'un tel mod.
    /// </summary>
    public string Archer { get; set; } = "";

    /// <summary>
    /// Index dans ArcherData.Archers au moment de la sauvegarde, garde en secours
    /// pour le cas ou le nom ne se retrouve plus.
    /// </summary>
    public int ArcherIndex { get; set; }

    /// <summary>
    /// <see cref="ProfileCostumes.Normal"/> ou <see cref="ProfileCostumes.Alt"/>.
    ///
    /// Stocke en chaine plutot qu'en ArcherData.ArcherTypes : l'enum du jeu comprend
    /// aussi Secret, qui n'est pas un choix offert ici, et une chaine reste lisible
    /// dans le fichier de sauvegarde.
    /// </summary>
    public string Costume { get; set; } = ProfileCostumes.Normal;

    /// <summary>
    /// Pouvoir du mod Aura, par son identifiant ("Kamehameha", "Kienzan"...), ou
    /// vide pour suivre le pouvoir par defaut regle dans Aura.
    ///
    /// Une chaine et non un enum : la liste des pouvoirs appartient a Aura, qui
    /// peut en ajouter sans que Profiles le sache. Un profil enregistre avec un
    /// pouvoir devenu inconnu - Aura desinstalle, pouvoir retire - reste lisible et
    /// retombe simplement sur le defaut.
    /// </summary>
    public string Power { get; set; } = "";

    /// <summary>
    /// Second pouvoir, sur l'autre gachette du haut. Memes regles que le premier :
    /// vide pour suivre le defaut regle dans le mod Power.
    ///
    /// Une propriete a part plutot qu'une liste : les emplacements sont deux, fixes
    /// par les deux gachettes, et une liste laisserait croire qu'on peut en ajouter.
    /// </summary>
    public string Power2 { get; set; } = "";

    /// <summary>
    /// Musique de fin de manche quand ce profil gagne, ou vide pour laisser celle de
    /// l'archer.
    ///
    /// Meme encodage que celle d'un archer forge - le nom d'une piste du jeu, ou
    /// "file:" suivi d'un fichier de la banque music - parce que c'est la meme liste
    /// qu'on fait defiler, et qu'un jour on voudra copier l'une dans l'autre.
    ///
    /// Distincte du son WIN de l'ecran des sons : ce dernier est une voix, jouee
    /// par-dessus. Celle-ci remplace la piste.
    /// </summary>
    public string VictoryMusic { get; set; } = "";

    /// <summary>
    /// Mappage manette propre au profil, ou null pour suivre celui du jeu.
    ///
    /// GamepadConfig porte [JsonInclude] sur chacun de ses champs : il se serialise
    /// tel quel dans le fichier des profils, sans conversion a ecrire. Null plutot
    /// qu'une copie du mappage global par defaut, pour que "je n'ai rien personnalise"
    /// reste distinct de "j'ai personnalise a l'identique" - un profil sans reglage
    /// suit ainsi les changements faits plus tard dans les options du jeu.
    /// </summary>
    public GamepadConfig Gamepad { get; set; }

    /// <summary>Mappage clavier propre au profil, ou null pour suivre celui du jeu.</summary>
    public KeyboardConfig Keyboard { get; set; }

    /// <summary>
    /// Les quatre touches que le jeu ne connait pas.
    ///
    /// TowerFall n'a QU'UNE action d'esquive, a laquelle on assigne une liste de
    /// boutons d'un coup. Ici elle est coupee en deux - une touche a gauche, une a
    /// droite - et deux touches de plus servent aux deux pouvoirs du mod Power, qui
    /// les lit par interop au lieu de LB/RB en dur.
    ///
    /// L'esquive du jeu reste alimentee : DashLeft et DashRight y sont reversees
    /// ensemble (voir ProfileControls.SyncDodge). Ce sont donc bien quatre touches
    /// pour deux roles, et non quatre actions nouvelles.
    ///
    /// Stockees en entiers et non en Buttons/Keys : le fichier de profil reste
    /// lisible et ne depend pas des valeurs d'une enumeration du jeu ou de XNA.
    /// </summary>
    public int[] PadDashLeft { get; set; }
    public int[] PadDashRight { get; set; }
    public int[] PadPowerLeft { get; set; }
    public int[] PadPowerRight { get; set; }

    public int[] KeyDashLeft { get; set; }
    public int[] KeyDashRight { get; set; }
    public int[] KeyPowerLeft { get; set; }
    public int[] KeyPowerRight { get; set; }

    /// <summary>
    /// Couleurs du sprite remplacees, en hexadecimal RRGGBB.
    ///
    /// La table fait foi et les PNG du dossier du profil n'en sont que le resultat :
    /// on peut ainsi refabriquer les images apres une mise a jour du jeu, ou lire ce
    /// qui a ete change sans ouvrir un editeur d'image.
    /// </summary>
    /// <summary>
    /// Ancienne palette unique du profil.
    ///
    /// Conservee uniquement pour la relecture des profils d'avant les essais :
    /// ProfileTrials.Migrate la convertit en un essai nomme DEFAULT, puis la vide.
    /// Aucun code ne doit s'en servir en dehors de cette conversion.
    /// </summary>
    public List<ColorSwap> Palette { get; set; }

    /// <summary>Les essais de couleurs, tous archers et costumes confondus.</summary>
    public List<ColorTrial> Trials { get; set; }

    /// <summary>L'essai retenu pour chaque couple archer / costume.</summary>
    public List<ActiveTrial> ActiveTrials { get; set; }

    /// <summary>
    /// Sons a ne jouer que de temps en temps ; tous les autres se jouent a chaque
    /// fois.
    ///
    /// Seuls les ecarts au defaut sont enregistres : la liste des sons affectes reste
    /// donnee par les fichiers presents dans le dossier du profil, et une entree qui
    /// ne correspond plus a aucun fichier est simplement ignoree. Sans cela, supprimer
    /// un WAV a la main laisserait une regle orpheline qui s'appliquerait au prochain
    /// fichier de meme nom.
    /// </summary>
    public List<OccasionalSound> OccasionalSounds { get; set; }

    /// <summary>Longueur maximale d'un nom, alignee sur celle du clavier virtuel.</summary>
    public const int MaxNameLength = 12;

    // Deduit de Costume : l'ecrire dans le fichier laisserait croire qu'on peut le
    // modifier a la main, alors que la relecture l'ignore.
    [JsonIgnore]
    public bool IsAlt => string.Equals(Costume, ProfileCostumes.Alt, StringComparison.OrdinalIgnoreCase);
  }

  /// <summary>Designe un fichier son a ne jouer que de temps en temps.</summary>
  public class OccasionalSound
  {
    public string Event { get; set; }
    public string File { get; set; }
  }

  /// <summary>Une couleur d'origine et celle qui la remplace, en hexadecimal RRGGBB.</summary>
  public class ColorSwap
  {
    /// <summary>
    /// Planche a laquelle le remplacement s'applique - BODY, HEAD, BOW...
    ///
    /// Vide ou absent, il vaut pour toutes : c'est la forme qu'avaient les profils
    /// avant que la recoloration devienne partie par partie. Ces entrees anciennes
    /// sont converties en entrees explicites a la premiere ouverture d'un ecran de
    /// couleur, pour qu'une teinte reglee sur la tete ne bouge plus quand on retouche
    /// le corps.
    /// </summary>
    public string Part { get; set; }

    public string From { get; set; }
    public string To { get; set; }
  }

  public static class ProfileCostumes
  {
    public const string Normal = "NORMAL";
    public const string Alt = "ALT";
  }
}
