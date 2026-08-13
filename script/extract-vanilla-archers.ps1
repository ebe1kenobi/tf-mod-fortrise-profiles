# Extrait tous les archers du jeu sous forme de mods FortRise autonomes.
#
# Un repertoire par personnage, contenant ses images, ses definitions de sprites et
# son archerData.xml - donc un mod qui se depose tel quel dans Mods/ et se modifie
# ensuite. C'est une base de travail, pas une copie a jouer : les archers d'origine
# sont deja dans le jeu.
#
# Ce que le script resout, et qui ne se devine pas :
#
# - Les archers vanilla n'ont pas d'attribut id ; les mods en exigent un. Le nom
#   vient du commentaire qui precede chaque bloc dans le fichier du jeu.
#
# - Les costumes ALT et les archers secrets HERITENT de leur parent tout ce qu'ils
#   ne declarent pas. On recopie donc leurs blocs tels quels, sans completer : les
#   completer figerait un heritage que le format sait faire vivre.
#
# - Le jeu a DEUX packs de contenu. Les planches des costumes ALT ne sont pas dans
#   l'atlas de base mais dans DarkWorldContent, alors que le spriteData de base les
#   cite. Chercher dans un seul pack fait paraitre manquantes des references
#   parfaitement valides.

param(
  [string]$Game = "C:\Program Files (x86)\Steam\steamapps\common\TowerFall",
  [string]$Out = "D:\__dev\code\archive\tf-archer\vanilla",
  [string]$Only = "*"
)

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = "Stop"

$spriteDir = Join-Path $Game "Content\Atlas\SpriteData"

# --- atlas, en couches -------------------------------------------------------

$packs = @(
  (Join-Path $Game "Content\Atlas"),
  (Join-Path $Game "DarkWorldContent\Atlas")
)

$atlases = @{}
foreach ($name in @("atlas", "menuAtlas")) {
  $layers = @()

  foreach ($pack in $packs) {
    $xmlPath = Join-Path $pack "$name.xml"
    if (-not (Test-Path $xmlPath)) { continue }

    $xml = [xml](Get-Content $xmlPath)
    $index = @{}
    foreach ($sub in $xml.TextureAtlas.SubTexture) { $index[$sub.name] = $sub }

    $layers += @{
      Image = (New-Object System.Drawing.Bitmap (Join-Path $pack "$name.png"))
      Index = $index
    }
  }

  $atlases[$name] = $layers
}

# --- sources de definitions --------------------------------------------------

$spriteDocs = @{}
foreach ($file in @("spriteData", "corpseSpriteData", "menuSpriteData")) {
  $spriteDocs[$file] = [xml](Get-Content (Join-Path $spriteDir "$file.xml"))
}

function Find-Sprite($file, $id) {
  $spriteDocs[$file].SpriteData.ChildNodes | Where-Object { $_.NodeType -eq 'Element' -and $_.id -eq $id }
}

# --- etat par personnage -----------------------------------------------------

$script:root = $null
$script:missing = @()
$script:repaired = @()
$script:copied = $null   # id vanilla -> id du mod, pour ne pas recopier deux fois

function Export-Sub($atlasName, $subName, $relative) {
  $node = $null
  $image = $null

  foreach ($layer in $atlases[$atlasName]) {
    if ($layer.Index.ContainsKey($subName)) {
      $node = $layer.Index[$subName]
      $image = $layer.Image
      break
    }
  }

  if (-not $node) { $script:missing += $subName; return $null }

  $w = [int]$node.width; $h = [int]$node.height
  $bmp = New-Object System.Drawing.Bitmap $w, $h
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CompositingMode = 'SourceCopy'
  $g.DrawImage($image, (New-Object System.Drawing.Rectangle 0, 0, $w, $h),
    (New-Object System.Drawing.Rectangle ([int]$node.x), ([int]$node.y), $w, $h), 'Pixel')
  $g.Dispose()

  $path = Join-Path $script:root $relative
  $dir = Split-Path $path -Parent
  if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()

  $relative.Replace('\', '/')
}

# Recopie une definition de sprite en remplacant ses noms d'atlas par des chemins.
# Rend le nouvel identifiant, ou null si la definition est introuvable.
function Copy-Sprite($file, $id, $target, $atlasName, $folder) {
  if ($script:copied.ContainsKey($id)) { return $script:copied[$id] }

  $node = Find-Sprite $file $id
  if (-not $node) { $script:missing += "sprite:$id"; return $null }

  $clone = $node.CloneNode($true)
  $newId = $id -replace '[^A-Za-z0-9_]', ''
  $clone.SetAttribute("id", $newId)

  # Texture en premier : les variantes d'equipe s'y rabattent quand elles manquent.
  $basePath = $null

  foreach ($field in @("Texture", "RedTexture", "BlueTexture", "RedTeam", "BlueTeam", "Flash")) {
    $elm = $clone[$field]
    if (-not $elm) { continue }

    $subName = $elm.InnerText.Trim()
    $leaf = ($subName -replace '[^A-Za-z0-9]', '_') + ".png"
    $path = Export-Sub $atlasName $subName "Content/Atlas/sprites/$folder/$leaf"

    if ($path) {
      $elm.InnerText = $path
      if ($field -eq "Texture") { $basePath = $path }
      continue
    }

    # Le jeu contient au moins un cas ou une variante d'equipe est declaree sans
    # exister nulle part : Orange_AltHead cite _red et _blue, absents des deux
    # atlas. Player les lit sans filet - TFGame.Atlas[nom] sur un dictionnaire -
    # donc l'entree telle quelle est une panne qui attend son mode de jeu.
    #
    # On la fait retomber sur la texture de base : le personnage garde ses couleurs
    # au lieu de tomber. C'est le seul endroit ou cette extraction s'ecarte
    # volontairement de l'original, et elle s'en ecarte en mieux.
    if ($field -ne "Texture" -and $basePath) {
      $elm.InnerText = $basePath
      $script:repaired += "$id/$field"
    }
    else {
      $clone.RemoveChild($elm) | Out-Null
    }
  }

  $target.DocumentElement.AppendChild($target.ImportNode($clone, $true)) | Out-Null
  $script:copied[$id] = $newId
  $newId
}

function New-SpriteDoc {
  $doc = New-Object System.Xml.XmlDocument
  $doc.AppendChild($doc.CreateElement("SpriteData")) | Out-Null
  $doc
}

function Save-Xml($doc, $relative) {
  $path = Join-Path $script:root $relative
  $dir = Split-Path $path -Parent
  if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
  $doc.Save($path)
}

# --- la voix -----------------------------------------------------------------

# Les archers du jeu ne declarent pas leurs sons : <SFX> porte un ENTIER, l'index
# d'un jeu de sons que le jeu tient en dur. Un mod qui reprend cet entier ne
# recopie donc rien - il pointe vers le jeu, et l'archer extrait n'a pas de voix a
# lui.
#
# Cette table est la correspondance de Sounds.Initialize : elle ne se devine pas,
# l'ordre des prefixes n'etant pas celui des archers. Les jeux numerotes (A2, B2...)
# sont ceux des costumes ALT ; ils sont INCOMPLETS et retombent sur leur jeu de
# base, exactement comme le jeu le fait avec le second argument de CharacterSounds.
$sfxSets = @{
  0  = @{ Prefix = "CHAR_B";  Fallback = $null }
  1  = @{ Prefix = "CHAR_C";  Fallback = $null }
  2  = @{ Prefix = "CHAR_D";  Fallback = $null }
  3  = @{ Prefix = "CHAR_A";  Fallback = $null }
  4  = @{ Prefix = "CHAR_F";  Fallback = $null }
  5  = @{ Prefix = "CHAR_E";  Fallback = $null }
  6  = @{ Prefix = "CHAR_H";  Fallback = $null }
  7  = @{ Prefix = "CHAR_G";  Fallback = $null }
  8  = @{ Prefix = "CHAR_I";  Fallback = $null }
  9  = @{ Prefix = "CHAR_E2"; Fallback = 5 }
  10 = @{ Prefix = "CHAR_B2"; Fallback = 0 }
  11 = @{ Prefix = "CHAR_A2"; Fallback = 3 }
  12 = @{ Prefix = "CHAR_D2"; Fallback = 2 }
  13 = @{ Prefix = "CHAR_I2"; Fallback = 8 }
}

# Les deux packs, comme pour les atlas : les jeux de sons des costumes ALT sont
# dans DarkWorldContent, pas dans le contenu de base.
$sfxDirs = @(
  (Join-Path $Game "Content\SFX"),
  (Join-Path $Game "DarkWorldContent\SFX")
)

# Tous les WAV d'un prefixe, par ACTION (le nom sans le prefixe).
function Get-SfxSet($prefix) {
  $found = [ordered]@{}

  foreach ($dir in $sfxDirs) {
    if (-not (Test-Path $dir)) { continue }

    foreach ($file in Get-ChildItem $dir -Filter "${prefix}_*.wav") {
      $action = $file.BaseName.Substring($prefix.Length + 1)
      if (-not $found.Contains($action)) { $found[$action] = $file.FullName }
    }
  }

  $found
}

# Recopie la voix d'un archer dans le mod, et remplace l'entier par un motif.
#
# Le repertoire porte l'identifiant du bloc : un archer et son costume ALT ont
# chacun leur jeu, et leurs actions portent les memes noms - un dossier commun les
# ferait s'ecraser.
function Export-Voice($clone, $id) {
  $node = $clone["SFX"]
  if (-not $node) { return }

  $index = 0
  if (-not [int]::TryParse($node.InnerText.Trim(), [ref]$index)) { return }
  if (-not $sfxSets.Contains($index)) { return }

  $set = $sfxSets[$index]
  $files = Get-SfxSet $set.Prefix

  # Le jeu de base comble ce que le jeu ALT ne fournit pas. Sans ce report, un
  # costume ALT extrait n'aurait que cinq sons sur vingt-trois.
  if ($null -ne $set.Fallback) {
    $base = Get-SfxSet $sfxSets[$set.Fallback].Prefix
    foreach ($action in $base.Keys) {
      if (-not $files.Contains($action)) { $files[$action] = $base[$action] }
    }
  }

  if ($files.Count -eq 0) { return }

  $dir = Join-Path $script:root "Content\SFX\$id"
  New-Item -ItemType Directory -Force -Path $dir | Out-Null

  foreach ($action in $files.Keys) {
    Copy-Item $files[$action] (Join-Path $dir "$action.wav") -Force
  }

  # Le motif que le chargeur developpe : {action} devient la cle de chaque son. Il
  # remplace l'entier, donc l'archer ne depend plus des sons du jeu.
  $node.InnerText = "Content/SFX/$id/{action}.wav"

  $script:voices += "$id ($($set.Prefix), $($files.Count))"
}

# --- un personnage -----------------------------------------------------------

function Export-Character($name, $entries) {
  $script:root = Join-Path $Out $name
  $script:copied = @{}
  if (Test-Path $script:root) { Remove-Item $script:root -Recurse -Force }

  $sprites = New-SpriteDoc
  $corpses = New-SpriteDoc
  $menus = New-SpriteDoc

  $archerDoc = New-Object System.Xml.XmlDocument
  $archersRoot = $archerDoc.CreateElement("Archers")
  $archerDoc.AppendChild($archersRoot) | Out-Null

  $parentId = $null

  foreach ($entry in $entries) {
    $clone = $archerDoc.ImportNode($entry.CloneNode($true), $true)

    # Identifiant : obligatoire pour un mod, absent chez les archers du jeu.
    $id = switch ($entry.Name) {
      "AltArcher" { "${name}Alt" }
      "SecretArcher" { "${name}Secret" }
      default { $name }
    }
    $clone.SetAttribute("id", $id)

    if ($entry.Name -eq "AltArcher") { $clone.SetAttribute("Alt", "@$parentId") }
    elseif ($entry.Name -eq "SecretArcher") { $clone.SetAttribute("Secret", "@$parentId") }
    else { $parentId = $id }

    Export-Voice $clone $id

    # Sprites : chaque enfant nomme une definition a recopier.
    if ($clone["Sprites"]) {
      foreach ($part in @($clone["Sprites"].ChildNodes)) {
        if ($part.NodeType -ne 'Element') { continue }
        $newId = Copy-Sprite "spriteData" $part.InnerText.Trim() $sprites "atlas" "player"
        if ($newId) { $part.InnerText = "@$newId" }
      }
    }

    if ($clone["Corpse"]) {
      $newId = Copy-Sprite "corpseSpriteData" $clone["Corpse"].InnerText.Trim() $corpses "atlas" "corpses"
      if ($newId) { $clone["Corpse"].InnerText = "@$newId" }
    }

    if ($clone["Gems"]) {
      $menu = $clone["Gems"]["Menu"]
      if ($menu) {
        $newId = Copy-Sprite "menuSpriteData" $menu.InnerText.Trim() $menus "menuAtlas" "portraits"
        if ($newId) { $menu.InnerText = "@$newId" }
      }

      $play = $clone["Gems"]["Gameplay"]
      if ($play) {
        # Meme identifiant que la gemme de menu chez tous les archers du jeu, mais
        # dans un autre dictionnaire : la table des deja-copies les confondrait.
        $script:copied.Remove($play.InnerText.Trim()) | Out-Null
        $newId = Copy-Sprite "spriteData" $play.InnerText.Trim() $sprites "atlas" "pickups"
        if ($newId) { $play.InnerText = "@$newId" }
      }
    }

    # Images isolees : tout ce qui designe une sous-texture et non une definition.
    $loose = @(
      @{ Path = @("Aimer"); Atlas = "atlas"; Folder = "aimers" },
      @{ Path = @("Hat", "Normal"); Atlas = "atlas"; Folder = "hat" },
      @{ Path = @("Hat", "Blue"); Atlas = "atlas"; Folder = "hat" },
      @{ Path = @("Hat", "Red"); Atlas = "atlas"; Folder = "hat" },
      @{ Path = @("Statue", "Image"); Atlas = "atlas"; Folder = "statues" },
      @{ Path = @("Statue", "Glow"); Atlas = "atlas"; Folder = "statues" },
      @{ Path = @("Hair", "Texture"); Atlas = "atlas"; Folder = "hair" },
      @{ Path = @("Hair", "TextureEnd"); Atlas = "atlas"; Folder = "hair" },
      @{ Path = @("Portraits", "NotJoined"); Atlas = "menuAtlas"; Folder = "portraits" },
      @{ Path = @("Portraits", "Joined"); Atlas = "menuAtlas"; Folder = "portraits" },
      @{ Path = @("Portraits", "Win"); Atlas = "menuAtlas"; Folder = "portraits" },
      @{ Path = @("Portraits", "Lose"); Atlas = "menuAtlas"; Folder = "portraits" }
    )

    foreach ($item in $loose) {
      $node = $clone
      foreach ($step in $item.Path) { $node = if ($node) { $node[$step] } else { $null } }
      if (-not $node) { continue }

      $subName = $node.InnerText.Trim()
      if ($subName.Length -eq 0) { continue }

      $leaf = ($subName -replace '[^A-Za-z0-9]', '_') + ".png"
      $path = Export-Sub $item.Atlas $subName "Content/Atlas/sprites/$($item.Folder)/$leaf"
      if ($path) { $node.InnerText = $path }
    }

    $archersRoot.AppendChild($clone) | Out-Null
  }

  Save-Xml $sprites "Content\Atlas\SpriteData\spriteData.xml"
  Save-Xml $corpses "Content\Atlas\SpriteData\corpseSpriteData.xml"
  Save-Xml $menus "Content\Atlas\SpriteData\menuSpriteData.xml"
  Save-Xml $archerDoc "Content\Atlas\GameData\archerData.xml"

  $meta = @{
    name = "Vanilla$name"
    description = "L'archer $name du jeu, extrait comme base de travail. Usage personnel."
    version = "0.1.0"
    author = "ebe1.kenobi"
    dependencies = @("FortRise:5.3.0", "FortRise.Content:5.3.0")
  }
  $metaPath = Join-Path $script:root "meta.json"
  Set-Content $metaPath ($meta | ConvertTo-Json) -Encoding UTF8

  $images = (Get-ChildItem $script:root -Recurse -Filter *.png).Count
  $sounds = (Get-ChildItem $script:root -Recurse -Filter *.wav).Count
  "{0,-8} {1} entrees, {2,3} images, {3,3} sons" -f $name, $entries.Count, $images, $sounds
}

# --- parcours ----------------------------------------------------------------

$archerData = [xml](Get-Content (Join-Path $Game "Content\Atlas\GameData\archerData.xml"))

$characters = [ordered]@{}
$current = $null
$comment = ""

foreach ($node in $archerData.Archers.ChildNodes) {
  if ($node.NodeType -eq 'Comment') { $comment = $node.Value.Trim(); continue }
  if ($node.NodeType -ne 'Element') { continue }

  # Les equipes ne sont pas des personnages : pas de sprites, pas de portraits.
  if ($node.Name -notin @("Archer", "AltArcher", "SecretArcher")) { continue }

  if ($node.Name -eq "Archer") {
    $current = $comment
    $characters[$current] = @()
  }

  if ($current) { $characters[$current] += $node }
}

if (-not (Test-Path $Out)) { New-Item -ItemType Directory -Force -Path $Out | Out-Null }

Write-Host "Extraction dans $Out`n"
$total = 0

foreach ($name in $characters.Keys) {
  if ($name -notlike $Only) { continue }

  $script:missing = @()
  $script:repaired = @()
  $script:voices = @()
  Write-Host (Export-Character $name $characters[$name])
  if ($script:repaired.Count -gt 0) {
    Write-Host ("         variantes d'equipe rabattues sur la texture de base : " + (($script:repaired | Sort-Object -Unique) -join ", "))
  }


  if ($script:voices.Count -gt 0) {
    Write-Host ("         voix copiees : " + ($script:voices -join ", "))
  }

  if ($script:missing.Count -gt 0) {
    Write-Host ("         introuvables : " + (($script:missing | Sort-Object -Unique) -join ", "))
  }

  $total++
}

foreach ($layers in $atlases.Values) { foreach ($layer in $layers) { $layer.Image.Dispose() } }

Write-Host "`n$total personnages extraits."
