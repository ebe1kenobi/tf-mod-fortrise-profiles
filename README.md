# Ebe1.Profiles

Profils de joueur pour TowerFall, sous FortRise 5.

Chaque joueur se cree un profil : un nom, un archer, des sons, des portraits, des
couleurs de sprite et son propre mappage de touches. Au debut d'une partie, chacun
choisit son profil sur l'ecran de selection des archers, et tout le reste suit.

Un archer recolore emmene sa couleur partout : son nom, ses fleches, son bouclier et
toutes ses particules la reprennent.

Une lame `PROFILES` s'ajoute au menu principal, entre `MODS` et `OPTIONS`.

## Sommaire

- [Choisir son profil en partie](#choisir-son-profil-en-partie)
- [La fiche d'un profil](#la-fiche-dun-profil)
- [Sons](#sons)
- [Images](#images)
- [Couleurs du sprite](#couleurs-du-sprite)
- [Touches](#touches)
- [Plusieurs joueurs sur le meme archer](#plusieurs-joueurs-sur-le-meme-archer)
- [Ou vivent les donnees](#ou-vivent-les-donnees)
- [API pour les autres mods](#api-pour-les-autres-mods)
- [Construire et deployer](#construire-et-deployer)
- [Points a savoir](#points-a-savoir)

## Choisir son profil en partie

Sur l'ecran de selection des archers, le bouton **Y** fait defiler les profils.

Le nom s'affiche au-dessus du portrait : blanc pour `P1`/`P2`... quand aucun profil
n'est choisi, jaune sinon. Le cycle compte un cran de plus que la liste — apres le
dernier profil vient « aucun profil », puis on repart du premier — ce qui permet de
se detacher sans faire le tour.

Choisir un profil applique son archer et sa tenue, et **verrouille les fleches** :
tant qu'un profil est actif, l'archer est le sien. Le verrou est leve si son archer
n'a pas pu etre applique, pour ne jamais enfermer un joueur sur un personnage qu'il
n'a pas choisi.

Le rattachement survit d'une partie a l'autre : revenir sur l'ecran de selection
retrouve les profils du tour precedent.

## La fiche d'un profil

La liste des profils s'ouvre depuis la lame `PROFILES`. `+ NEW PROFILE` demande un
nom au clavier virtuel ; `Alt` sur une ligne supprime le profil.

| Ligne | Ce qu'elle fait |
|---|---|
| `NAME` | Renomme au clavier virtuel. Le dossier de donnees suit le renommage. |
| `ARCHER` | Archer prefere, aux fleches. |
| `COSTUME` | Tenue normale ou alternative. Grisee si l'archer n'a pas d'alternative. |
| `SOUNDS` | Sons par evenement. |
| `IMAGES` | Portraits personnels. |
| `COLORS` | Essais de recoloration du sprite. |
| `GAMEPAD` / `KEYBOARD` | Mappage de touches propre au profil. |
| `SAVE` | Enregistre et revient a la liste. |
| `DELETE PROFILE` | Supprime, avec confirmation. |

Le bouton **Retour enregistre aussi** : `SAVE` ne rend pas la sauvegarde possible,
il la rend visible.

### Clavier virtuel

Frappe directe au clavier physique, disposition et Shift compris. A la manette :
Confirmer pose la lettre surlignee, Alt efface, Start valide, Retour annule.

## Sons

Les WAV vivent dans un **vivier commun** ; les affecter a un profil en copie le
fichier dans son dossier, qui devient ainsi autonome.

```
Saves/Ebe1.Profiles/wav/*.wav        vos fichiers
ModFile/Content/wav/*.wav            ceux livres avec le mod, marques MOD
```

A nom de fichier identique, le votre l'emporte. Le vivier est relu a chaque ouverture
de l'ecran : vous pouvez deposer des fichiers pendant que le jeu tourne.

### Evenements

`DIE`, `DIE_BOMB`, `DIE_ENV`, `DIE_LASER`, `DIE_STOMP`, `FIRE_ARROW`, `WIN`, plus un
`KILLED_BY_<AUTRE PROFIL>` par autre profil.

Un evenement accepte **plusieurs sons** : le mod en tire un au sort. `KILLED_BY_X`
l'emporte sur `DIE` quand il existe, etant le plus specifique des deux.

### A chaque fois ou de temps en temps

Chaque son affecte porte un mode, aux fleches : `ALWAYS` par defaut, ou `SOMETIMES`.
Un son occasionnel passe un tirage a 25 % avant d'entrer dans le tirage final ; si
aucun son ne reste eligible, **c'est le son du jeu qui se fait entendre** — une
replique qui ne sort pas a tous les coups plutot qu'un silence.

`Alt` fait ecouter le fichier survole.

## Images

Six emplacements, un fichier chacun, choisis dans un vivier de PNG.

| Emplacement | Ou | Taille du jeu |
|---|---|---|
| `ARCHER` / `ARCHER_ALT` | portrait de l'ecran de selection | 60x120 |
| `WIN` / `WIN_ALT` | portrait de victoire | 50x50 |
| `LOSE` / `LOSE_ALT` | portrait de defaite | 50x50 |

Le costume du profil decide de la variante appliquee. L'apercu suit la ligne
survolee et affiche les dimensions du fichier. `Alt` retire l'image.

```
Saves/Ebe1.Profiles/images/*.png     vos fichiers
ModFile/Content/images/*.png         ceux livres avec le mod
```

Le script `script/split-portraits.ps1` decoupe les planches empaquetees du mod
Customize en fichiers individuels, prets pour ce vivier.

## Couleurs du sprite

Le mod ne teinte pas le sprite : il **fabrique une texture recoloree** et la
substitue, comme le jeu le fait pour les couleurs d'equipe. L'atlas partage n'est
jamais modifie, et deux joueurs sur le meme archer gardent chacun ses couleurs.

### Essais

`COLORS` ouvre la liste des **essais** de l'archer et du costume courants. Un essai
est une tentative nommee : on en garde plusieurs, on compare, on choisit.

- `+ NEW TRIAL` en cree un, `+ IMPORT` en lit un du vivier.
- Valider ouvre l'essai dans l'editeur, `Alt` ouvre `EXPORT / DELETE`.
- L'apercu montre l'essai **survole** sans l'activer : on juge avant de choisir.

Les essais sont ranges par archer et par costume. Changer d'archer ne perd donc
rien : ceux de l'ancien restent en place et ressortent quand on y revient.

L'export ecrit un JSON dans `Saves/Ebe1.Profiles/trials/`, lisible et modifiable a la
main. Un essai fait pour un autre archer s'importe quand meme, il n'apparaitra que
lorsque le profil sera sur cet archer-la.

### Les deux editeurs

**Par familles de teinte** — les nuances sont groupees en `RED`, `ORANGE`, `YELLOW`,
`GREEN`, `CYAN`, `BLUE`, `PURPLE`, `PINK`, plus `GREY` et `BLACK`. Vous choisissez
une couleur pour la famille : la nuance la plus etendue devient exactement celle-la,
et toutes les autres subissent le meme ecart de teinte, de saturation et de
luminosite. Les ombres restent des ombres.

**Couleur par couleur** — les teintes dominantes une a une, pour la retouche fine.

Une ligne permet de passer de l'un a l'autre. Dans les deux, Confirmer ouvre la roue
chromatique, `Alt` la saisie hexadecimale.

### Choisir les parties

Cinq cases en haut : `BODY`, `HEAD`, `BOW`, `HAT`, `CORPSE`. La palette ne montre que
les teintes des parties cochees, et le remplacement n'est ecrit que pour elles — une
couleur reglee sur la tete ne bouge plus quand on retouche le corps, meme si les deux
partagent la teinte.

Deux cases couvrent plusieurs planches. `HEAD` prend les quatre tetes : normale, sans
chapeau, couronne, arriere. `BOW` prend l'arc **et la fleche encochee**, qui n'apparait
qu'avec lui — les separer aurait permis de recolorer l'arc en laissant dessus une
fleche aux couleurs d'origine.

### Reglages d'ensemble

`ADJUST` donne quatre curseurs, appliques apres le remplacement et sur tout le
sprite :

| Reglage | Effet |
|---|---|
| `SATURATION` | Le plus payant : delave ou avive le personnage entier. |
| `HUE` | Fait glisser toutes les teintes d'un coup. |
| `BRIGHTNESS` | Assombrit ou eclaircit. |
| `CONTRAST` | A manier avec retenue : une silhouette de douze pixels n'a que quelques teintes pour rendre son volume. |

La palette affiche le **rendu final**, reglages compris, et la couleur que vous
choisissez est bien celle que vous obtenez.

### L'apercu

Le sprite anime, a sa taille de jeu, avec le cadavre a cote. Il enchaine en
parallele : quatre animations du corps, les trois coiffes, six poses de cadavre et
trois poses d'arc — dont l'arc bande, qui n'emploie pas les memes images que le
repos.

La fleche encochee se montre sur cette pose bandee, la seule ou le jeu l'affiche
aussi.

### En jeu

Les textures recolorees s'appliquent au corps, aux trois tetes, a l'arc, a la fleche
encochee, au chapeau qui s'envole et au cadavre.

### La couleur dominante

Tout ce que le jeu teinte avec la couleur de l'archer prend la **couleur dominante du
profil** : la teinte la plus etendue du corps, contours et ombres exclus — les
proposer donnerait un noir qui n'apprend rien sur le joueur.

Elle sert a trois familles de choses.

**Le nom du joueur**, aux trois endroits ou il s'affiche : sur l'ecran de selection,
au debut du round et sur les resultats de manche. C'est ce qui relie le libelle au
personnage, surtout depuis que deux joueurs peuvent partager un archer.

**Les objets teintes** : l'empennage de la fleche ordinaire, l'image de la fleche
plume, et le bouclier.

**Les particules**, toutes les familles que le jeu indexe par archer :

| Famille | Quand |
|---|---|
| `PlayerDust` | course, atterrissage |
| `Dash` | esquive, bouclier, fantome |
| `HyperJump` | saut hyper |
| `PurpleAmbience` | brume permanente de l'archer violet |
| `PlayerFeathers` | fleche plume |
| `LaserArrowGlow` / `LaserArrowTrail` | fleche laser |

Chaque profil recoit ses **propres types de particules**, jamais une teinte posee sur
le type partage : une particule relit les couleurs de son type pendant toute sa vie,
une couleur rendue apres coup ressortirait sur la moitie des particules en vol.

### Les modes par equipes

Ils gardent integralement les couleurs du jeu — sprites, noms, objets et particules.
Ces couleurs designent le camp, les remplacer supprimerait l'information.

## Touches

`GAMEPAD` et `KEYBOARD` donnent a chaque profil son mappage, ou `GLOBAL` pour suivre
celui du jeu. Le mappage s'installe quand le profil est choisi sur l'ecran de
selection, et se retire quand on le quitte.

Le profil recoit toujours une **copie** de la configuration : l'ecran Options du jeu
ne peut donc pas ecrire dedans par megarde, et la configuration globale n'est jamais
alteree.

## Plusieurs joueurs sur le meme archer

Le jeu l'interdit, pour que deux personnages identiques ne soient pas confondus. La
recoloration rendant cette raison caduque, le mod leve la restriction.

Deux joueurs sur le meme archer **sans** profil recolore resteront indiscernables :
la restriction avait sa raison d'etre.

## Ou vivent les donnees

```
Saves/Ebe1.Profiles/
  Ebe1.Profiles.profiles.json          les profils
  wav/                                  vivier de sons
  images/                               vivier d'images
  trials/                               essais exportes
  profiles/<PROFIL>/
    <EVENEMENT>/*.wav                   sons affectes
    images/<EMPLACEMENT>.png            portraits affectes
    sprite/<ARCHER>/<COSTUME>/<ESSAI>/<PARTIE>.png
```

Le JSON fait foi ; les PNG de sprite en sont le resultat, refabricables. Renommer un
profil deplace son dossier ; le supprimer l'efface.

L'archer est enregistre par son **nom**, l'index ne servant que de secours : un mod
qui ajoute ou retire des archers decale les index, les profils y survivent.

## API pour les autres mods

```csharp
context.Interop.GetApi<IProfilesModApi>("Ebe1.Profiles");
```

| Membre | |
|---|---|
| `HandlesRollcall` | Vrai quand Profiles pilote l'ecran de selection. Un mod qui lit le meme bouton doit s'abstenir. |
| `GetPlayerName(int)` | Nom affiche : le profil, ou `P1`..`P8`. Ne rend jamais vide. |
| `SetPlayerName(int, string)` | Impose un nom. Le choix d'un profil reprend la main dessus. |
| `GetProfileName(int)` | Nom du profil rattache, ou vide. |

`GetPlayerName` et `SetPlayerName` reprennent les signatures de l'ancien
`ICustomNameModApi` : un mod qui lisait les noms via CustomName n'a que le nom du mod
a changer.

`LoaderAI`, `Poto`, `Tournament` et `WinCounters` sont deja bascules. CustomName se
retire de l'ecran de selection quand Profiles est actif.

## Construire et deployer

```
dotnet build VSCode/TFModFortRiseProfiles.csproj
```

La tache MSBuild de `FortRise.Configuration` publie sous le nom du projet ; les
scripts de `script/` produisent en plus un `release/` sous le nom
`tf-mod-fortrise-profiles`, aligne sur les autres mods du depot. **`deploy.bat`
efface les deux copies avant de reinstaller** : sans quoi le mod serait charge deux
fois.

## Points a savoir

- **Version majeure** : FortRise exige une majeure identique entre la version
  requise par un mod dependant et celle installee. Passer de `1.x` a `2.0` obligerait
  a mettre a jour les quatre mods qui dependent de Profiles.
- **Police du jeu** : elle ne dessine pas tout. Les libelles passent par un filtre
  qui retire les caracteres inconnus, sans quoi `MeasureString` leve en plein rendu
  et le jeu tombe — un nom de fichier accentue suffisait.
- **Pastille "NEW!"** : FortRise dessine son indicateur de mises a jour a une
  ordonnee fixe, calee sur la lame la plus haute. Inserer une lame entre `MODS` et
  `OPTIONS` impose de remonter `MODS`, et la pastille designe alors `PROFILES`. Elle
  n'apparait que lorsque des mises a jour attendent. Voir le commentaire dans
  `VSCode/Patches/MyMainMenu.cs`.
- **Mod homonyme** : `Teuria.Profiles` (GameBanana 693658) couvre un terrain voisin
  et patche lui aussi l'ecran de selection. Les deux ne sont pas prevus pour
  cohabiter.
