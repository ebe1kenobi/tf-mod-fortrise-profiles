# La forge d'archers

Un joueur assemble son propre archer depuis le menu du jeu : il choisit un
personnage source, corrige les poses qui ne lui plaisent pas, essaie le resultat
immediatement, et l'exporte en mod quand il en est content.

**Etat.** Ecrit en entier, pas encore compile ni essaye en jeu.

## Par ou l'on entre

`PROFILES` > `ARCHER FORGE`. Pas de lame au menu principal : les lames se suivent
sans interstice et `QUIT` touche deja le bas de l'ecran, en inserer une seconde
apres `PROFILES` obligerait a en deplacer trois.

La fiche d'un archer tient en onze lignes :

| Ligne | Ce qu'elle fait |
|---|---|
| `NAME` | Nom court, sans espace. Il sert d'identifiant de sprite et de nom de dossier a l'export. |
| `TOP NAME` / `BOTTOM NAME` | Les deux lignes du nom affiche en jeu. |
| `SOURCE` | Choisit un personnage source et pose les seize poses d'un coup. |
| `FRAMES` | Ouvre les seize emplacements un par un. `Alt` en vide un. |
| `WINDOW X` / `WINDOW Y` | Deplace la fenetre de decoupe, d'un pixel a la fois, l'apercu sous les yeux. |
| `BORROWED HUE` | Teinte de l'arc, du viseur, des ailes et des gemmes. |
| `TEST IN GAME` | Enregistre l'archer a chaud. Il apparait a la selection sans redemarrage. |
| `EXPORT AS MOD` | Ecrit un mod FortRise complet dans `Mods/`. Actif au prochain lancement. |

`SOURCE` et `FRAMES` sont les deux facons d'assembler, et elles sont voisines
exprès. Qui veut tout choisir a la main ne touche jamais a `SOURCE` ; qui veut
aller vite ne descend jamais jusqu'a `FRAMES`. Aucun mode a declarer : les deux
chemins mènent au meme dessin, et changer de source ne detruit pas les poses deja
corrigees.

---

## Ce qu'il faut pour faire un archer

Releve sur `FR5tf-mod-fortrise-archer-brones`, qui est le precedent complet et qui
tourne. Un archer TowerFall n'est pas une image, c'est une vingtaine de planches.

| Piece | Format | D'ou elle vient |
|---|---|---|
| Corps | 10 images de 24x24, accolees | **choisie** |
| Corps rouge / bleu | idem | derivee par deplacement de teinte |
| Cadavre | 6 images de 24x24 | **choisi** |
| Cadavre rouge / bleu / flash | idem | derives |
| Tete sans chapeau / couronne | 5 images de 10x10 | vides par defaut |
| Arc + variantes d'equipe | 6 images de 12x10 | repris du jeu, teinte |
| Viseur, ailes, gemmes | 30x10, 120x20, 40x20 et 160x40 | repris du jeu, teintes |
| Portraits joined / notJoined | 60x120 | agrandissement de la pose debout |
| Portraits win / lose | 50x50 | idem |
| Statue | 20x20 | idem |

Seize images a choisir, tout le reste se deduit. C'est ce rapport qui rend la
forge utilisable : on ne demande pas au joueur de dessiner un buste de 60x120.

## Ce qui rend le choix supportable

Les planches Broforce ont **toutes la meme mise en page**. Verifie sur huit
personnages : la case (0,0) donne la pose debout, (21,4) le saut, (16,7) le
cadavre au sol, et ainsi de suite pour les seize. Les coordonnees codees en dur
dans `build-sprites.ps1` ne valaient donc pas que pour Indianna Brones - elles
valent pour la planche entiere du jeu.

La forge s'en sert : choisir une planche source pre-remplit les seize poses d'un
coup. Le joueur n'ouvre le detail que pour ce qui cloche - le `dodge` de BROBOCOP
est vide, par exemple, et il faut lui en trouver un autre.

Les deux facons de faire coexistent, et c'est la meme liste dans les deux cas :

- **Par personnage** : une planche source, seize poses posees, on corrige.
- **A la main** : aucune source, chaque pose se choisit dans le vivier.

## Le vivier

`script/slice_sheets.py` decoupe les planches en images individuelles, un
repertoire par planche, avec un `index.json` decrivant la grille. 333 planches,
29 714 images.

La forge lit ces repertoires tels quels. Elle n'a besoin de rien d'autre :
`index.json` porte la taille des cases, la grille et le cadre du dessin dans sa
case, ce qui suffit a afficher, a caler et a recadrer.

Le vivier vit dans l'espace de sauvegarde du mod, a cote des profils :

```
FortRise/Saves/Profiles/sprites/<planche>/rXXcYY.png
FortRise/Saves/Profiles/sprites/<planche>/index.json
```

Un `slice_sheets.py --out` pointant la suffit a l'alimenter. Le chemin reste
reglable pour qui veut travailler depuis son depot sans recopier 30 000 fichiers.

Sur les 333 planches, **140 peuvent servir de source** : cases de 32x32 et grille
assez grande pour que les seize poses existent. Les autres restent choisissables
case par case, mais ne pre-remplissent pas.

### Le vivier doit etre decoupe avec `--keep-duplicates`

`slice_sheets.py` n'ecrit qu'une fois deux cases rigoureusement identiques. C'est
raisonnable pour ranger des images et faux pour la forge : la case ecartee n'a pas
de fichier, la forge la croit vide, et l'emplacement se retrouve comble par la pose
debout. **28 poses sur 16 planches** sont dans ce cas - `mookWarlock_anim` et
`TankBroTank_Gun_Anim` y perdent leurs deuxieme et troisieme images de course, et
se mettent donc a boiter sans que rien n'ait signale d'erreur.

`ForgeBank.Duplicates` compte ce que le decoupage a ecarte, pour que l'ecran puisse
le dire au lieu de laisser chercher.

### Ce qui reste mal decoupe

Une trentaine de planches sont mal decoupees - celles dont les dessins se touchent,
ou la deduction remonte a une case absurde. `mookbig_anim` sort en cases de 128 la
ou la vraie case fait 32. Elles ne passent pas le filtre des sources et ne genent
donc personne ; les corriger reste a faire, sans urgence.

---

## Ce que la forge n'exploite pas encore

Un archer du jeu porte bien plus d'images que les seize d'ici. Releve sur
`Vigilante Thief`, qui est le Green d'origine : sa planche de corps fait 144x80 en
images de 12x20, soit **12 colonnes sur 4 rangees - 48 images**. La planche n'est
donc pas une bande mais une grille, et rien n'empeche la forge d'en faire autant.

| Animation | Le jeu | La forge |
|---|---|---|
| `stand` | 1 | 1 |
| `run` | 2 images | **3** |
| `jump` | 3 images | 1 |
| `fall` / `glide` | 2 images en boucle | 1 |
| `slide` | 8 images, **fois trois etats de coiffe** | 1, repetee |
| tete | 24 images : idle, lookUp, lookDown, lookBack, chacun en version sol, chute et saut | vide |

La matiere existe dans les planches Broforce : la rangee 1 porte une course de dix
images, la rangee 4 une chute et une roulade, la rangee 3 une escalade complete. Ce
qui manque n'est pas le dessin, c'est la table de coordonnees, qui ne connait
aujourd'hui qu'une image par animation.

Autres proprietes du sprite de corps, toutes facultatives sauf mention :

- `HeadYOrigins` - **obligatoire**, et le piege le plus vicieux du format. Player le
  lit sans verifier qu'il existe, puis l'indexe par l'image courante du corps **sans
  borne** - son voisin `HeadXOrigins` est garde, pas lui. Un tableau plus court que
  la planche fait tomber le jeu pendant le rendu, en plein match, au moment ou une
  animation atteint l'image en trop. `ForgeSlots.HeadYOrigins()` le calcule donc
  depuis la liste des poses, pour qu'il ne puisse pas prendre du retard sur elle.
- `RedTexture` / `BlueTexture` - lues **sans garde** en mode par equipes. Un corps
  qui ne les declare pas y tombe.
- `SlideHead` - vaut `True` par defaut. La forge le met a `False` : la tete est
  dessinee dans le corps, en poser une seconde pendant la glissade d'esquive serait
  absurde. C'est ce que font `Vigilante Thief` et `Prancing Puppet`.
- `HideBow` - retire l'arc. Pas employe, mais c'est sans doute ce qu'on veut pour un
  personnage qui porte un fusil.
- `BowXOffsets` / `BowYOffsets` - deplacent l'arc image par image.
- `Hat` dans `archerData.xml` - le chapeau qui s'envole quand on est touche. La
  forge ne le declare pas, d'ou `StartNoHat`.

## Architecture

Le mod a deja tout ce qu'il faut : `SpriteRecolor` fabrique des textures
autonomes en memoire, `UIProfileImagePicker` parcourt un vivier avec apercu,
`UISpritePreview` anime un sprite dans le menu. La forge reprend ces trois
choses.

### Core

| Fichier | Role |
|---|---|
| `ForgeSlots.cs` | Les seize emplacements : nom, planche de destination, index dans la planche, taille d'image. Source unique de verite, calquee sur le `spriteData.xml` de Brones. |
| `ForgeLayout.cs` | La table des coordonnees canoniques Broforce, pose par pose. C'est elle qui pre-remplit. |
| `ForgeBank.cs` | Le vivier : enumere les repertoires, lit les `index.json`, charge les apercus a la demande et les libere. |
| `ForgeDesign.cs` | L'archer en cours : noms, couleurs, fenetre de decoupe, et une reference d'image par emplacement. |
| `ForgeStorage.cs` | Lecture et ecriture des archers forges, dans un fichier a part de celui des profils. |
| `ForgeBuild.cs` | L'assemblage : construit les `Texture2D` accolees a partir des images choisies, plus les variantes rouge, bleue et flash. |
| `ForgeRegister.cs` | L'enregistrement a chaud dans FortRise. |
| `ForgeExport.cs` | L'ecriture d'un mod autonome sur le disque. |

### UI

| Ecran | Ce qu'il fait |
|---|---|
| `UIForgeList` | La liste des archers forges. `+ NEW ARCHER`, `Alt` pour supprimer. |
| `UIForgeEdit` | La fiche : `NAME0`, `NAME1`, `COLORS`, `SOURCE`, `FRAMES`, `TEST`, `EXPORT`. |
| `UIForgeSource` | Le choix de la planche source, avec apercu de la pose debout. |
| `UIForgeFrames` | Les seize emplacements, vignette a cote de chacun. Un emplacement vide se voit. |
| `UIForgeFramePicker` | Le choix d'une image pour un emplacement : planche, puis case, avec apercu. |
| `UIForgePreview` | Le rendu anime du corps assemble - marche, saut, esquive - pendant qu'on choisit. |

Une lame `FORGE` s'ajoute au menu principal a cote de `PROFILES`, par le meme
point d'accroche que `MyMainMenu` emploie deja.

---

## L'enregistrement a chaud

FortRise l'autorise. `RegistryQueue.AddOrInvoke` appelle l'invoker sans attendre
des lors que `loadState == Ready`, et `IModSubtextures.RegisterTexture` accepte
une `Func<Subtexture>` - donc une texture construite en memoire, exactement ce que
`SpriteRecolor` fabrique deja.

L'ordre est impose par les dependances :

1. `RegisterTexture` pour chaque planche assemblee.
2. `RegisterSprite<string>` pour le corps, les tetes, l'arc.
3. `RegisterCorpseSprite<string>` pour le cadavre.
4. `RegisterMenuSprite<string>` pour la gemme du rollcall.
5. `RegisterArcher` avec l'`ArcherConfiguration` qui les rassemble.

### Le piege des particules

`Particles.Load()` construit six tableaux dimensionnes sur le nombre d'archers du
moment : `Dash`, `HyperJump`, `LaserArrowGlow`, `LaserArrowTrail`, `PlayerDust`,
`PlayerFeathers`. Un archer ajoute apres coup porte un index qui sort de ces
tableaux, et le jeu tombe **a la premiere esquive**, pas a la selection - ce qui
en fait une panne facile a manquer en test.

La forge les rallonge donc elle-meme apres chaque enregistrement, en copiant le
type existant et en lui posant les couleurs du nouvel archer. `ProfileParticles`
sait deja copier un `ParticleType` et decaler ses teintes ; c'est le meme geste.

### Ce qui restera imparfait a chaud

Le nombre d'archers change sous une interface qui l'a peut-etre deja lu. La liste
noire de FortRise et le verrou du rollcall comparent a `ArcherData.Archers.Length`.
Le risque est faible et se corrige en ressortant du menu, mais l'export existe
pour ceux qui veulent un archer definitif : un mod ecrit sur le disque passe par
le chemin de chargement normal, celui que Brones valide deja.

## L'export

`EXPORT` ecrit un mod complet dans `FortRise/Mods/`, dans la forme exacte du mod
Brones :

```
Ebe1.Forge.<nom>/
  meta.json
  Content/Atlas/GameData/archerData.xml
  Content/Atlas/SpriteData/spriteData.xml
  Content/Atlas/SpriteData/corpseSpriteData.xml
  Content/Atlas/SpriteData/menuSpriteData.xml
  Content/Atlas/sprites/...
```

Deux regles apprises a la dure sur Brones, et qui valent pour le generateur :

- Le cadavre va dans `corpseSpriteData.xml` et nulle part ailleurs. Le chargeur
  range chaque entree selon le fichier qui la contient ; declare dans
  `spriteData.xml`, il est introuvable et le jeu tombe a la premiere mort.
- La gemme du rollcall est un `sprite_string` de `menuSpriteData.xml`, lue par
  `ArcherPortrait.InitGem`. Declaree en `sprite_int`, elle est introuvable de la.

---

## Comment l'assemblage est verifie

L'archer Brones a ete fabrique a la main, puis eprouve en jeu. Ses planches sont
donc une reference : la forge, partie de la meme planche source et des memes
coordonnees, doit les retrouver au pixel pres.

`script/forge_verify.py` transcrit le calcul de `ForgeBuild.cs` et le fait partir
du vivier decoupe, comme le C# en jeu. Il compare les vingt-deux planches des deux
costumes de Brones - corps, teintes d'equipe, cadavre, flash, portraits.

```
python script/forge_verify.py
22 planches identiques a la reference.
```

Ce n'est pas une formalite : la verification a trouve deux ecarts reels. Les
canaux etaient tronques au lieu d'etre arrondis, ce qui deplacait des centaines de
pixels d'un point sur chaque planche teintee ; et les pixels transparents
gardaient leur couleur, invisible en jeu mais suffisant a faire echouer toute
comparaison. Les deux sont corriges, dans le C# comme dans le prototype.

Quand `ForgeBuild.cs` change, `forge_verify.py` change avec lui, ou la
verification ne veut plus rien dire.

## Ce qui reste a eprouver

Rien de tout ceci n'a encore tourne dans le jeu. Les points ou l'on s'attend a
trouver quelque chose, dans l'ordre ou il vaut mieux les essayer :

1. **L'esquive apres un essai a chaud.** C'est le test de `ForgeParticles`. S'il
   manque quelque chose, le jeu tombe la et nulle part ailleurs.
2. **La mort.** C'est le test du cadavre enregistre comme sprite de cadavre et non
   comme sprite ordinaire.
3. **L'arrivee sur l'archer au rollcall.** C'est le test de la gemme de menu.
4. **Les modes par equipes.** Les textures rouge et bleue remplacent la planche
   entiere ; une taille qui ne correspond pas se voit tout de suite.
5. **L'export, puis un redemarrage.** Le mod genere doit se charger seul, sans que
   la forge soit ouverte.

L'assemblage, lui, est deja verifie autrement qu'a l'oeil - voir plus haut.
