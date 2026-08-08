# Archers du jeu, extraits

Les neuf archers de TowerFall, chacun sous forme de **mod FortRise autonome** : ses
images, ses definitions de sprites, son `archerData.xml`, son `meta.json`. Un
repertoire se depose tel quel dans `FortRise/Mods/` et se modifie ensuite.

C'est une base de travail, pas une copie a jouer — les archers d'origine sont deja
dans le jeu.

Produit par `FR5tf-mod-fortrise-profiles/script/extract-vanilla-archers.ps1`.

## Usage personnel

Les images viennent des atlas du jeu. **A ne pas distribuer.**

## Ce que contient chacun

| Perso | Entrees | Images du corps | Glissade | PNG | Particularite |
|---|---|---|---|---|---|
| Green | 2 | **44** | **8 img** | 45 | le plus complet |
| Cyan | 2 | 32 | **8 img** | 45 | |
| Red | 2 | 24 | 1 | 39 | |
| Orange | 2 | 20 | 1 | 37 | |
| Blue | 2 | 17 | 1 | 36 | |
| Purple | 2 | 17 | 1 | 41 | tetes en 96x64 |
| White | 3 | 12 | 1 | 40 | archer secret + cheveux |
| Yellow | 2 | 12 | 1 | 41 | |
| Pink | 3 | 11 | 1 | 55 | archer secret (Kyle) |

« Entrees » compte l'archer, son costume ALT, et son archer secret quand il en a un.

## Ce que ce tableau apprend

**Les archers du jeu ne se ressemblent pas.** De 11 a 44 images de corps, des tailles
d'image qui vont de 10x10 a 96x64 pour les tetes, des arcs de 3 a 9 images. Rien
n'est uniforme, donc rien n'est impose : un archer riche et un archer sobre passent
par le meme format.

Deux seulement — le vert et le cyan — ont une vraie glissade murale de 8 images. Les
autres se contentent d'une image fixe, comme nos archers forges.

## Points a savoir

**Deux packs de contenu.** Les planches des costumes ALT ne sont pas dans l'atlas de
base mais dans `DarkWorldContent`, alors que le `spriteData.xml` de base les cite.
Chercher dans un seul pack fait paraitre manquantes des references valides.

**L'heritage des ALT est reel.** Un `<AltArcher>` reprend toute la configuration de
son parent et ne redefinit que ce qu'il declare. Les blocs sont recopies tels quels,
sans etre completes : les completer figerait un heritage que le format sait faire
vivre. C'est pourquoi certains ALT n'ont ni portraits ni gemmes ici.

**Une reparation, une seule.** `Orange_AltHead` declare des textures d'equipe
(`_red`, `_blue`) qui n'existent dans aucun atlas — un defaut de donnees du jeu.
`Player` les lit sans filet, sur un dictionnaire : l'entree telle quelle est une
panne qui attend son mode de jeu. Elles sont rabattues sur la texture de base. C'est
le seul endroit ou cette extraction s'ecarte de l'original.

## Verifier apres modification

```bash
powershell -File ../../../FR5tf-mod-fortrise-archer-greenclone/script/check.ps1 -Mod <repertoire>
```

Il controle ce qui ne se voit pas au chargement : chaque image citee existe, chaque
index d'animation tient dans sa planche, et `HeadYOrigins` est assez long. Ce dernier
point est le plus vicieux — `Player` l'indexe par l'image courante du corps **sans
borne**, un tableau trop court fait tomber le jeu en plein match et pas au
chargement.
