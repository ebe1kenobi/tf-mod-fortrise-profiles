# Archer

Profils de joueur pour TowerFall, sous FortRise 5.

> **Le mod s'appelait `Ebe1.Profiles`.** Il s'appelle desormais `Archer`, du nom de ce
> qu'il configure. Au premier lancement, il **reprend tout seul** les donnees de
> `Saves/Ebe1.Profiles` vers `Saves/Archer` : profils, images, sons, essais de
> couleurs et archers forges. Les anciens fichiers sont copies, pas deplaces - ils
> restent intacts a leur place, au cas ou.
>
> Les mods qui l'interrogent le demandent maintenant par `"Archer"` : `Power`,
> `Poto`, `Tournament`, `WinCounters` et `LoaderAI` sont deja bascules et doivent etre
> redeployes en meme temps.

Chaque joueur se cree un profil : un nom, un archer, des sons, des portraits, des
couleurs de sprite et son propre mappage de touches. Au debut d'une partie, chacun
choisit son profil sur l'ecran de selection des archers, et tout le reste suit.

Un archer recolore emmene sa couleur partout : son nom, ses fleches, son bouclier et
toutes ses particules la reprennent.

Une lame `ARCHER` s'ajoute au menu principal, entre `MODS` et `OPTIONS`.

## Sommaire

- [Choisir son profil en partie](#choisir-son-profil-en-partie)
- [La fiche d'un profil](#la-fiche-dun-profil)
- [Sons](#sons)
- [Images](#images)
- [Couleurs du sprite](#couleurs-du-sprite)
- [Archer Forge](#archer-forge)
- [Touches](#touches)
- [Naviguer dans les ecrans](#naviguer-dans-les-ecrans)
- [Plusieurs joueurs sur le meme archer](#plusieurs-joueurs-sur-le-meme-archer)
- [Extraire les archers du jeu](#extraire-les-archers-du-jeu)
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

La liste des profils s'ouvre depuis la lame `ARCHER`. `+ NEW PROFILE` demande un
nom au clavier virtuel ; `Alt` sur une ligne supprime le profil.

| Ligne | Ce qu'elle fait |
|---|---|
| `NAME` | Renomme au clavier virtuel. Le dossier de donnees suit le renommage. |
| `ARCHER` | Archer prefere, aux fleches. |
| `COSTUME` | Tenue normale ou alternative. Grisee si l'archer n'a pas d'alternative. |
| `POWER LB` | Pouvoir de la gachette gauche, aux fleches. **N'apparait que si le mod Power est installe.** |
| `POWER RB` | Pouvoir de la gachette droite, meme regle. |
| `VICTORY MUSIC` | Musique de fin de manche quand ce profil gagne. Les fleches font defiler les pistes du jeu, Confirmer ouvre la liste des fichiers deposes. |
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

### Les deux pouvoirs

Les lignes `POWER LB` et `POWER RB` n'existent que lorsque le mod **Power** est
installe : c'est lui qui tient la liste des pouvoirs, Archer ne fait qu'enregistrer
lesquels ont ete choisis.

**Deux emplacements, un par gachette du haut.** Le mod Power y met ce qu'on veut, y
compris le **vol**, qui est un pouvoir comme un autre : le prendre, c'est renoncer a
un tir. Les deux rubriques proposent la meme liste.

`DEFAULT` laisse decider le reglage `Default power 1` ou `Default power 2` du mod
Power, et ce n'est pas la meme chose qu'aucun pouvoir - pour ca il y a l'entree
`NONE`. `DEFAULT` est la premiere entree du cycle, donc on y revient toujours sans
avoir a supprimer le profil.

Un pouvoir est enregistre par son **identifiant** et non par son rang : un profil garde
son choix meme si Power en ajoute ou en retire entre-temps. Un identifiant devenu
inconnu s'affiche tel quel et se comporte comme `DEFAULT`.

La dependance est **optionnelle des deux cotes** : sans Power, les lignes
disparaissent et rien d'autre ne change ; sans Archer, Power donne ses pouvoirs par
defaut a tout le monde. Aucun des deux mods ne reference l'autre a la compilation.

Le second pouvoir passe par une interface a part, `IProfilesPower2Api` : l'interop
construit son proxy sur la **forme** des membres, donc un mod Power plus ancien qui ne
connait que `GetPlayerPower` continue de fonctionner. Ajouter le membre a l'interface
existante aurait fait echouer tout le proxy chez lui, et il aurait perdu le premier
pouvoir en meme temps que le second.

## Sons

Les WAV vivent dans un **vivier commun** ; les affecter a un profil en copie le
fichier dans son dossier, qui devient ainsi autonome.

```
Saves/Archer/wav/*.wav        vos fichiers
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
Saves/Archer/images/*.png     vos fichiers
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

L'export ecrit un JSON dans `Saves/Archer/trials/`, lisible et modifiable a la
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

## Archer Forge

`ARCHER FORGE`, en tete de la liste des profils, fabrique de nouveaux archers a
partir d'images decoupees. Un archer forge s'essaie **sans redemarrer le jeu**, ou
s'exporte comme un mod autonome.

### Le vivier

**Un repertoire par planche, un PNG par pose, et rien d'autre.** Pas d'index, pas de
grille, aucun format propre a la forge : ce qui se regarde dans un explorateur de
fichiers se debogue sans lancer le jeu, et une image ajoutee ou remplacee a la main
est vue telle quelle.

La forge lit `Saves/Archer/sprites`, ou l'endroit qu'indique un fichier
`sprites.path` — trente mille fichiers ne se recopient pas a chaque essai.
`script/slice_sheets.py` remplit ce dossier, mais n'importe quel PNG depose a la main
fait l'affaire. `script/slice_sheets.ps1` fait exactement la meme chose **sans
Python** : memes reglages, memes noms de fichiers, et environ deux fois et demie plus
rapide. Le pixel y est confie a une classe C# compilee a la volee - quatre millions
de pixels relus a chaque test de grille, PowerShell pur y mettrait des minutes. Rien
a installer : System.Drawing est livre avec Windows.

Les fichiers dont le nom commence par un **souligne** sont ignores : c'est ce qui
ecarte la planche de contact (`_contact.png`) sans l'empecher d'exister.

Toute taille d'image est acceptee. Celles qui depassent la fenetre de decoupe sont
**reduites** a l'entree, en conservant leur proportion et au plus proche voisin ; les
plus petites sont laissees telles quelles, car les agrandir doublerait leurs pixels.

Le vivier est relu **a chaque entree dans la forge** : un decoupage relance pendant
que le jeu tourne est vu sans redemarrer.

### Composer un archer

`SOURCE` pose les seize poses d'un coup depuis un personnage. `FRAMES` les ouvre une
par une. Aucun des deux n'est un mode : qui veut tout choisir a la main ne touche
jamais a `SOURCE`.

`WINDOW X` et `WINDOW Y` reglent **ou decouper** dans la case source. La meme fenetre
sert a toutes les poses — une fenetre ajustee pose par pose ferait sautiller le
personnage au lieu de le faire marcher. Un pixel d'ecart se voit : le personnage
flotte ou s'enfonce dans le sol, d'ou l'apercu anime a cote.

### Chapeau

Trois emplacements en fin de liste, tous facultatifs : `CHAPEAU`, `CHAPEAU BLEU`,
`CHAPEAU ROUGE`. Sans image, l'archer part tete nue - c'est ce que faisaient tous les
archers forges jusqu'ici. Avec, il retrouve le chapeau qui s'envole quand on le
touche, le seul effet de jeu que les neuf archers du jeu ont tous.

Rien n'est emprunte automatiquement, contrairement a l'arc ou au viseur : un chapeau
se voit, et celui d'un autre archer se reconnait. Pour reprendre celui du vert, il
suffit de deposer ses images dans le vivier et de les choisir ici.

Les deux variantes d'equipe sont facultatives elles aussi : sans elles, le chapeau
normal est reteinte comme le reste du personnage.

L'image est **recadree sur son dessin** et non laissee a la taille de la fenetre de
decoupe. Le jeu centre le chapeau sur sa propre texture : une image de 24x24 presque
vide le ferait tourner autour du vide et voler de travers. Les chapeaux du jeu font
huit pixels sur quatre.

### Costume ALT

Facultatif. `ALT COSTUME OF` rattache un archer forge a un autre : il occupe alors
**son** emplacement au rollcall et se choisit avec la bascule ALT, au lieu d'ajouter
une case a la selection.

La chaine ne va pas plus loin que deux : un archer qui a deja un ALT ne peut pas en
devenir un.

A l'essai, le parent doit etre enregistre en premier — il prete son emplacement.

### Voix

`VOICE` donne une voix de repli parmi celles du jeu, puis vingt-et-une actions —
`FIRE ARROW`, `JUMP`, `DIE`, `WALLSLIDE LOOP`... — que l'on peut remplacer une par
une avec un WAV de la banque des sons. La ligne survolee joue son fichier.

Les libelles sont **derives des cles d'evenement**, donc identiques a ceux de l'ecran
`SOUNDS` d'un profil pour tout ce que les deux partagent. Deux libelles a tenir en
accord finissent toujours par diverger, et le fichier exporte porte de toute facon le
nom de la cle.

Le repli agit **action par action** : poser un son sur `MORT` ne rend pas l'archer
muet pour les vingt autres. Un archer sans aucun fichier a deja une voix complete.

### Musique de victoire

`VICTORY MUSIC` propose `AUTO` et les treize pistes du jeu **aux fleches** ;
**Confirmer** ouvre la liste des fichiers deposes. Deux gestes et non un seul cycle :
les treize pistes se connaissent par coeur et se prennent au vol, alors qu'un fichier
se cherche dans une liste - il y en a autant qu'on en depose.

Les fichiers sont lus dans `Saves/Archer/music` **et** dans `Saves/Archer/wav` (WAV ou
OGG). Un jingle de victoire est un WAV comme un autre, et obliger a le recopier dans
un second dossier ne servirait qu'a expliquer pourquoi il y a deux dossiers.

`AUTO` fait suivre la voix de repli. Ce n'est pas de la coquetterie : le jeu lit ce
champ comme une cle de dictionnaire **sans la verifier**, et un archer sans musique
fait tomber la fin du round. Il y a donc toujours une valeur, meme quand on n'a rien
choisi.

**Une musique apportee ne s'entend qu'apres export**, et la ligne l'annonce en
affichant `(EXPORT)`. Une piste doit etre declaree comme ressource de mod pour etre
jouable, et une ressource se construit a partir d'un contenu de mod que l'essai a
chaud n'a pas. L'archer essaye gagne donc sur la piste du jeu que `AUTO` aurait
choisie ; le mod exporte, lui, emporte le fichier dans `Content/Music` et joue la
bonne.

### Essayer et exporter

`TEST IN GAME` pose l'archer dans la partie en cours. Trois familles de tableaux du
jeu sont indexees par archer et dimensionnees au chargement — particules, compteurs
de statistiques, voix : la forge les rallonge, sans quoi le jeu tomberait a la
premiere esquive, a la premiere mort ou en fin de match. Un archer essaye ne se
retire pas : il faut redemarrer.

`EXPORT AS MOD` ecrit un vrai mod dans `Mods/Ebe1.Forge.<nom>`, qui passe par le
chemin de chargement normal et survit a la suppression du dessin.

L'export ne gere pas encore le **costume ALT** : il ecrirait un `<AltArcher>` citant
des planches absentes du mod, ce qui ferait tomber le chargement. Les deux ou aucun.

## Touches

`GAMEPAD` et `KEYBOARD` donnent a chaque profil son mappage, ou `GLOBAL` pour suivre
celui du jeu. Le mappage s'installe quand le profil est choisi sur l'ecran de
selection, et se retire quand on le quitte.

Le profil recoit toujours une **copie** de la configuration : l'ecran Options du jeu
ne peut donc pas ecrire dedans par megarde, et la configuration globale n'est jamais
alteree.

Les deux ecrans montrent `DASH LEFT`, `DASH RIGHT`, `POWER LEFT` et `POWER RIGHT` -
quatre touches pour deux roles. Le jeu n'a QU'UNE action d'esquive : les deux dash y
sont reversees ensemble, et les deux touches de pouvoir sont lues par le mod Power,
qui ne connait rien d'elles autrement.

**`ALT DODGE` n'y figure pas**, et c'est voulu : `XGamepadInput` verse cette touche
dans `DodgeCheck` a cote de `Dodge`, c'est donc une troisieme touche d'esquive, en
doublon des deux ci-dessus. Elle sert encore a naviguer dans les menus et garde pour
cela la valeur du jeu - on l'a retiree du reglage, pas de la configuration.

## Naviguer dans les ecrans

Le bouton retour ramene **d'ou l'on vient**, et sur **la ligne qu'on avait quittee**.
Les deux tiennent a une pile de navigation (`Core/MenuNav.cs`) plutot qu'a un ecran
parent ecrit en dur dans chaque `Create()`.

La difference se voit des qu'un ecran a deux portes d'entree : les reglages
d'ensemble s'ouvrent depuis les deux editeurs de couleur, le choix d'image depuis les
poses comme depuis les calques. Une valeur en dur y est juste une fois sur deux.

La pile depile TOUT ce qui se trouve au-dessus de l'ecran d'arrivee, ce qui rattrape
aussi les sorties de secours - un ecran dont la fiche a disparu se renvoie a la liste,
trois crans plus haut, sans avoir a le declarer.

## Plusieurs joueurs sur le meme archer

Le jeu l'interdit, pour que deux personnages identiques ne soient pas confondus. La
recoloration rendant cette raison caduque, le mod leve la restriction.

Deux joueurs sur le meme archer **sans** profil recolore resteront indiscernables :
la restriction avait sa raison d'etre.

## Extraire les archers du jeu

`script/extract-vanilla-archers.ps1` ecrit un mod par personnage du jeu, images,
SpriteData et `archerData.xml` compris : une base de travail, pas une copie a jouer.

Les **sons sont copies**, pas references. Les archers du jeu ne declarent pas leur
voix : `<SFX>` y porte un ENTIER, l'index d'un jeu de sons que le jeu tient en dur, et
un mod qui reprend cet entier ne recopie rien - il pointe vers le jeu. Le script
resout l'index (l'ordre des prefixes `CHAR_A`..`CHAR_I` n'est PAS celui des archers),
copie les vingt-trois WAV dans `Content/SFX/<archer>/` et remplace l'entier par
`Content/SFX/<archer>/{action}.wav`.

Les costumes ALT ont leur propre jeu, **complete par celui de base** la ou il manque -
exactement ce que fait le jeu. Sans ce report, un costume ALT extrait n'aurait que
cinq sons sur vingt-trois.

La **musique de victoire reste une reference**, et c'est la seule limite : les pistes
du jeu sont enfermees dans `MusicWaveBank.xwb`, une banque XACT compressee. Il n'y a
pas de fichier a copier. Les sons, eux, sont des WAV poses dans `Content/SFX`.

## Ou vivent les donnees

```
Saves/Archer/
  Archer.profiles.json                  les profils
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
context.Interop.GetApi<IProfilesModApi>("Archer");
```

| Membre | |
|---|---|
| `HandlesRollcall` | Vrai quand Profiles pilote l'ecran de selection. Un mod qui lit le meme bouton doit s'abstenir. |
| `GetPlayerName(int)` | Nom affiche : le profil, ou `P1`..`P8`. Ne rend jamais vide. |
| `SetPlayerName(int, string)` | Impose un nom. Le choix d'un profil reprend la main dessus. |
| `GetProfileName(int)` | Nom du profil rattache, ou vide. |

Les pouvoirs vivent dans **deux interfaces separees**, a demander chacune de son cote :

| Interface | Membre | |
|---|---|---|
| `IProfilesPowerApi` | `GetPlayerPower(int)` | Pouvoir de la gachette gauche, ou vide. |
| `IProfilesPower2Api` | `GetPlayerPower2(int)` | Pouvoir de la gachette droite, ou vide. |

Une interface par ajout, et jamais un membre de plus sur une interface deja publiee :
le proxy de l'interop est construit sur la **forme** des membres, donc un appelant qui
declare un membre absent de la version installee n'obtient plus rien du tout - pas
meme les membres qui existent.

`GetPlayerName` et `SetPlayerName` reprennent les signatures de l'ancien
`ICustomNameModApi` : un mod qui lisait les noms via CustomName n'a que le nom du mod
a changer.

`LoaderAI`, `Poto`, `Power`, `Tournament` et `WinCounters` sont deja bascules sur le
nom `Archer`. CustomName se retire de l'ecran de selection quand ce mod est actif.

## Construire et deployer

```
dotnet build VSCode/TFModFortRiseArcher.csproj
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
