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

### Les trois etats de couvre-chef

Le jeu montre un archer de trois facons : tete nue, coiffe, couronne. C'est la tete
qui change, jamais le corps - **sauf pendant la glissade**, ou la tete est cachee et
ou le corps doit donc porter le couvre-chef lui-meme. D'ou trois images de glissade,
et trois planches de tete.

La forge les DEDUIT plutot que de les faire redessiner :

| A fournir | Ce qu'on en tire |
|---|---|
| Les cinq images de tete nue | `HeadNoHat` |
| `CHAPEAU` (celui qui s'envole deja) | `HeadNormal` = tete nue + chapeau |
| `COURONNE`, une image | `HeadCrown` = tete nue + couronne |
| `GLISSADE` | `GLISSADE CHAPEAU` et `GLISSADE COURONNE`, ornement pose seulement si la tete est cachee |

Les emplacements `TETE COIFFEE` et `TETE COURONNEE` existent quand meme, pour
reprendre la main : une casquette qui change la silhouette, un archer repris d'un mod
qui fournit ses trois planches. Une image choisie l'emporte toujours sur le deduit.

Le cas qui vaut le detour : **un dessin sans tete du tout, mais avec un chapeau.**
L'etat coiffe vaut alors le chapeau SEUL, et l'etat nu reste vide. Un personnage qui
porte sa tete dans son corps - tout ce qui sort d'une planche Broforce - ne peut pas
perdre un chapeau dessine avec lui ; sorti en sprite de tete, il s'envole. C'est le
seul effet de jeu des archers d'origine qui manquait encore, et il ne coute qu'une
image.

L'ornement se cale par son propre decalage de calque, comme n'importe quelle image.

Les trois planches sont **teintees par equipe** comme le corps, par le meme
deplacement de teinte. Sans cela un archer a tete separee gardait sa tete verte sur
un corps rouge - et le chapeau avec, puisqu'il y est pose. Les emplacements
`CHAPEAU BLEU` et `CHAPEAU ROUGE` restent utiles au chapeau qui S'ENVOLE, qui est une
image a part et non un morceau du personnage.

### La tete qui suit le corps

Le jeu accroche la tete a une hauteur **par image du corps** : chez l'archer vert 19
debout, 20 sur la premiere image de course, 15 accroupi. C'est ce qui fait qu'une
tete vit au lieu de flotter.

`TETE Y`, dans l'ecran des calques d'une pose du corps, regle cet ecart. L'import le
releve tout seul sur un archer repris ; cette ligne est le seul moyen de le donner a
un archer dessine ici.

Le sens est celui de la forge - a droite, le personnage descend - alors que la valeur
enregistree est un ecart d'ANCRE, dont le sens est l'inverse. Le retournement a lieu
une fois, dans `ForgeSlots.HeadYOrigins`, et nulle part ailleurs.

### Ce que la forge ne represente toujours pas

Releve sur l'archer vert, qui est le modele du genre :

| Planche | Images | Citees | Ce que la forge en prend |
|---|---|---|---|
| Corps | 48 | 35 | 12 |
| Tete x3 etats | 24 chacune | 17 | 5 chacune |

- **La glissade est une animation de huit images** chez les archers du jeu ; la forge
  n'en pose qu'une, tenue le temps de la glissade.
- **La tete a dix-sept images** : les cinq de base, plus des variantes de chute - qui
  alternent sur deux images, la tete ballotte - et de saut. Les treize animations de
  la forge pointent toutes sur ses cinq images.

Rien de tout cela n'empeche un archer de fonctionner : on perd du mouvement, pas des
poses.

### Retoucher les images : taille et rognage

`TAILLE / ROGNAGE` sur la fiche regle **toutes les images d'un coup** ; la meme ligne
dans l'ecran des calques ne regle que **l'image courante**. C'est le meme ecran :
ce sont les memes reglages, sur un nombre different d'images.

| Ligne | Ce qu'elle fait |
|---|---|
| `TAILLE` | Pourcentage de la taille du fichier, par pas de 5. |
| `ROGNER GAUCHE/DROITE/HAUT/BAS` | Pixels retires de chaque bord, comptes sur le fichier. |
| `MIROIR H` / `MIROIR V` | Retourne l'image dans son cadre, sans le deplacer. |
| `ROTATION` | Autour du centre de l'image, par crans de 15 degres. |
| `DETOURER` | Rogne chaque image sur ses pixels opaques - image par image, chacune ayant son propre vide autour d'elle. |
| `REINITIALISER` | Rend les images telles qu'elles sont dans le vivier. |

Quatre choses a savoir, et aucune ne se devine :

- **Rien n'est ecrit dans le vivier.** Une retouche est un reglage du dessin : elle
  se defait, et la meme image servie a deux archers peut y etre reglee autrement.
- **La taille est absolue**, en pourcentage du fichier. Reposer 40% deux fois donne
  la meme image ; un reglage relatif reduirait a chaque passage.
- **On reduit autour de l'ANCRE**, pas du coin de l'image. L'ancre est le point que
  le jeu pose sur la position du personnage - les pieds au sol. Reduire depuis le
  coin ferait flotter en l'air tout ce qu'on rapetisse.
- **Le rognage ne deplace pas ce qui reste** : il compense de lui-meme le decalage
  qu'il provoque, sinon chaque bord retire demanderait un recalage.

Le miroir horizontal merite un mot : ce n'est pas un effet, c'est une reprise. Les
archers du jeu sont dessines tournes vers la DROITE, arc devant eux, et c'est le jeu
qui retourne l'image pour l'autre sens. Une planche prise ailleurs et dessinee vers
la gauche donne donc un personnage qui court a reculons - sur toutes les images a la
fois, cette ligne la remet d'aplomb.

La rotation tourne autour du centre de l'image, le centre restant en place : le cadre
s'agrandit de ce qu'il faut pour que rien ne sorte, et le decalage suit tout seul.
**Les quarts de tour sont exacts** - une transposition, pas un echantillonnage :
quatre fois 90 degres rendent l'image d'origine au pixel pres, et le compte de pixels
opaques ne bouge pas. Les autres angles perdent quelques pixels au passage - 3 sur 156
a 45 degres sur la pose debout de Brones - ce qui reste utilisable pour incliner un
bras ou coucher un cadavre, mais ne doit pas servir a redresser une planche.

Tout se fait au plus proche voisin, sans interpolation - un sprite a bords francs
doit le rester. Une reduction mange donc des colonnes entieres, et une reduction
forte peut poser les pieds un pixel trop haut ; les facteurs ronds - une moitie, un
quart - sont les plus surs, et `WINDOW Y` rattrape le reste.

### Le cadre orange, et les apercus a taille reelle

Les apercus sont a la **taille reelle** du sprite tel qu'il sera en jeu, du premier
ecran au dernier. Un apercu agrandi flatte toujours, et l'on ne decouvre qu'apres
l'export que le personnage fait deux fois la taille d'un archer. La gachette gauche
fait defiler 1x, 2x, 4x, 8x pour examiner un detail ; le reglage est commun a tous
les ecrans.

Le rectangle orange ne decoupe plus rien - le cadre reel se mesure sur les images
choisies, voir `ForgeCompose.FrameOf`. Il montre desormais **la place que tiendrait
un archer du jeu** : 12x20, ancre au bas et deux pixels a droite du centre, comme le
vert et le jaune. Cale sur la meme ancre que la pose, pieds au sol - deux rectangles
de tailles differentes ne se comparent que s'ils reposent au meme endroit.

La question qu'il repond est la seule qui compte devant une image reprise ailleurs :
de combien ce personnage depasse-t-il un archer d'origine ?

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
| `ForgeImport.cs` | Le chemin inverse : reprend un archer installe, decoupe ses planches dans le vivier et rend un dessin qui les designe. |
| `ForgePixels.cs` | Les retouches d'une image : rogner ses bords, changer sa taille, relever ses marges transparentes. |

### UI

| Ecran | Ce qu'il fait |
|---|---|
| `UIForgeList` | La liste des archers forges. `+ NEW ARCHER`, `IMPORT ARCHER`, `Alt` pour supprimer. |
| `UIForgeImport` | Les archers installes qu'on peut reprendre, un par ligne, avec le mod d'ou ils viennent. |
| `UIForgeAdjust` | La taille et le rognage des images : toutes a la fois depuis la fiche, une seule depuis les calques. |
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

### Reessayer le meme archer

`TEST IN GAME` se rappelle autant de fois qu'on veut. On ne reenregistre pas
l'archer - FortRise ne sait pas en retirer un, et un second sous le meme nom
laisserait deux entrees dont une morte : on **remplace ce que la premiere montre**.

C'est possible parce que rien n'est fige dans un `ArcherData` : ses sprites y sont
designes par un identifiant, resolu a l'apparition du joueur, et ses images par
l'objet lui-meme. `ForgeRegister.Apply` refait donc les memes affectations que
`ModArchers.Invoke` au premier essai, sur la structure deja en place. L'index de
l'archer ne bouge pas, donc rien ne se decale.

Deux consequences a connaitre :

- Un joueur deja en piste garde les planches avec lesquelles il est ne. Il faut
  relancer la partie, pas le jeu.
- Ce qui a ete enregistre entre-temps - textures, sprites, sons - reste dans les
  registres. On ne peut pas les en retirer, et ce sont quelques planches par essai
  dans une session ou l'on essaie justement.

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

## L'import

`IMPORT ARCHER`, en tete de la liste, reprend un archer deja installe : le sien,
exporte puis essaye en jeu, ou celui de quelqu'un d'autre.

Rien n'est demande a son auteur, parce que tout ce qu'il faut est deja dans ce que
le JEU exige de lui :

| Fichier du mod | Ce qu'on y lit |
|---|---|
| `GameData/archerData.xml` | Ce qu'est un archer : son nom affiche, ses couleurs, et quels sprites sont son corps et son cadavre. |
| `SpriteData/*.xml` | Comment ces planches se decoupent - taille d'une image, ancre - et quelle image joue quelle animation. |

Trois choses valent d'etre dites, parce qu'aucune ne se devine :

- **On part d'`archerData.xml`, pas des PNG.** Chercher des images sous
  `sprites/player` retrouve les memes fichiers, mais sous le nom du sprite -
  `GCBody` au lieu de `GreenClone` - et sans savoir lequel des huit fichiers du
  repertoire est le personnage. Tout element dont le nom finit par `Archer` en est
  un : `Archer`, `AltArcher` pour le costume, `SecretArcher` pour celui qu'on
  debloque.
- **Les poses se retrouvent par le NOM des animations, pas par le rang des
  images.** Les rangs ne sont pas les memes partout : chez les archers du jeu
  l'image 3 est le rebord, chez ceux que la forge exporte c'est la troisieme image
  de course. Le nom, lui, est le meme des deux cotes - c'est le jeu qui le cherche.
- **Une planche est une grille, pas une bande.** L'archer vert range ses huit
  images de glissade sur sa troisieme rangee ; les chercher sur une seule ligne en
  perdrait les trois quarts.

L'ancre lue devient la fenetre du dessin, et chaque planche qui n'a pas la meme
recoit le decalage qui l'y ramene. C'est ce qui permet de reprendre un archer de
20x20 comme un de 24x24 sans rien recaler a la main.

L'import **copie** : les poses entrent dans le vivier, le dessin les designe, et le
mod d'origine n'est pas touche. Le desinstaller ne casse pas ce qui a ete importe.

### La tete, qui n'est pas une planche comme les autres

Le jeu la pose lui-meme sur le corps, et il **ecrase l'origine de son sprite** a
chaque image :

```
Origin.Y = headYOrigins[image du corps]        // toujours, sans borne
Origin.X = headXOrigins[image du corps]        // seulement si le tableau est assez long
```

L'import reprend cette regle telle quelle. La tete entre dans les emplacements
cadres comme le corps, calee sur la pose DEBOUT ; les ecarts des autres poses -
chez l'archer vert 19 debout, 20 sur la premiere image de course, 15 accroupi -
sont releves et redeclares dans notre propre `HeadYOrigins`. La tete suit donc le
corps au lieu de rester rigide : sans eux, un personnage accroupi garderait la tete
quatre pixels trop haut.

`SlideHead` est repris du mod plutot que devine : les archers du jeu repondent non,
leurs images de glissade portant deja la tete, et en poser une seconde par-dessus en
ferait deux.

Un archer qui porte sa tete dans son corps - tout ce que la forge exporte - a une
planche de tete **entierement transparente** : `BronesNoHat.png` fait 50x10 et ne
contient pas un pixel opaque, quand celle de l'archer vert en contient 975. Une
image vide n'etant jamais importee, ce cas se resout tout seul.

Ce qui n'est pas repris : tout ce qui se deduit a la fabrication - variantes
d'equipe, silhouette, portraits, statue - et la meche arriere (`headBackSprite`),
que peu d'archers ont et pour laquelle la forge n'a pas d'emplacement.

Verifie hors du jeu sur les sept archers installes - Madeline, Badeline,
MadelineButPink, Brones, Brodread, GreenClone, GreenCloneAlt : chaque pose revient
identique au pixel pres, ancre sur ancre. Seul `run3` manque a GreenClone, et il
manque vraiment - son animation de course ne cite que deux images.

La tete est verifiee de la meme facon, en comparant ce que la forge assemble a ce que
le jeu dessine avec ses propres tableaux : **les neuf poses de GreenClone sont
identiques au pixel pres**, glissade et accroupi compris.

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
