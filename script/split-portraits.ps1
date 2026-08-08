<#
.SYNOPSIS
  Decoupe les planches de portraits du mod Customize en fichiers individuels,
  utilisables dans le vivier d'images du mod Profiles.

.DESCRIPTION
  Customize empaquette huit sous-images par joueur dans un seul PNG, decrit par un
  XML d'atlas a cote. Profiles attend au contraire un fichier par emplacement.

  Le script lit chaque couple .xml / .png du dossier source, et ecrit une image par
  SubTexture, nommee <JOUEUR>_<EMPLACEMENT>.png.

  Les noms de SubTexture de Customize se terminent par le nom du joueur
  ("portraits/winAltEric") : la classification se fait donc sur le debut du nom, en
  testant les variantes ALT et NOTJOINED avant les formes courtes qui en sont des
  prefixes.

.PARAMETER Source
  Dossier contenant les couples .xml / .png. Par defaut celui du mod Customize.

.PARAMETER Destination
  Dossier de sortie. Par defaut le vivier d'images de Profiles.

.PARAMETER WhatIf
  Affiche ce qui serait ecrit sans rien creer.

.EXAMPLE
  .\split-portraits.ps1
  .\split-portraits.ps1 -WhatIf
  .\split-portraits.ps1 -Source "D:\mes\portraits" -Destination "D:\sortie"
#>

[CmdletBinding(SupportsShouldProcess = $true)]
param(
  [string] $Source = "D:\__dev\code\tf-mod-fortrise-customize-private\Content\Atlas\PLAYER",
  [string] $Destination = "C:\Program Files (x86)\Steam\steamapps\common\TowerFall\FortRise\Saves\Ebe1.Profiles\images"
)

Add-Type -AssemblyName System.Drawing

# L'ordre compte : "joined" est un suffixe de "notJoined", et "win" de "winAlt".
# Tester les formes longues d'abord evite de classer une image dans le mauvais
# emplacement.
$slotRules = @(
  @{ Prefix = 'notJoinedAlt'; Slot = 'ARCHER_ALT_NOTJOINED' }
  @{ Prefix = 'joinedAlt';    Slot = 'ARCHER_ALT' }
  @{ Prefix = 'notJoined';    Slot = 'ARCHER_NOTJOINED' }
  @{ Prefix = 'joined';       Slot = 'ARCHER' }
  @{ Prefix = 'winAlt';       Slot = 'WIN_ALT' }
  @{ Prefix = 'loseAlt';      Slot = 'LOSE_ALT' }
  @{ Prefix = 'win';          Slot = 'WIN' }
  @{ Prefix = 'lose';         Slot = 'LOSE' }
)

function Get-Slot([string] $subTextureName) {
  $bare = $subTextureName -replace '^.*/', ''
  foreach ($rule in $slotRules) {
    if ($bare -like "$($rule.Prefix)*") { return $rule.Slot }
  }
  return $null
}

if (-not (Test-Path $Source)) {
  Write-Error "Dossier source introuvable : $Source"
  exit 1
}

if (-not (Test-Path $Destination)) {
  if ($PSCmdlet.ShouldProcess($Destination, "Creer le dossier")) {
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
  }
}

$written = 0
$skipped = 0

foreach ($xmlFile in Get-ChildItem $Source -Filter *.xml) {
  $pngPath = Join-Path $Source ($xmlFile.BaseName + '.png')
  if (-not (Test-Path $pngPath)) {
    Write-Warning "$($xmlFile.Name) : pas de PNG a cote, ignore"
    continue
  }

  $player = $xmlFile.BaseName.ToUpperInvariant()
  $xml = [xml](Get-Content $xmlFile.FullName)
  $sheet = [System.Drawing.Image]::FromFile($pngPath)

  try {
    foreach ($sub in $xml.TextureAtlas.SubTexture) {
      $slot = Get-Slot $sub.name
      if (-not $slot) {
        Write-Warning "$player : '$($sub.name)' ne correspond a aucun emplacement, ignore"
        $skipped++
        continue
      }

      $x = [int]$sub.x; $y = [int]$sub.y
      $w = [int]$sub.width; $h = [int]$sub.height

      # Une zone qui deborde de la planche produirait une image noire plutot qu'une
      # erreur : on la refuse explicitement.
      if ($x -lt 0 -or $y -lt 0 -or ($x + $w) -gt $sheet.Width -or ($y + $h) -gt $sheet.Height) {
        Write-Warning "$player/$slot : zone ${x},${y} ${w}x${h} hors de la planche $($sheet.Width)x$($sheet.Height), ignore"
        $skipped++
        continue
      }

      $outPath = Join-Path $Destination "$($player)_$slot.png"

      if ($PSCmdlet.ShouldProcess($outPath, "Ecrire $($w)x$($h)")) {
        $crop = New-Object System.Drawing.Bitmap $w, $h
        $g = [System.Drawing.Graphics]::FromImage($crop)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
        $g.DrawImage($sheet,
          (New-Object System.Drawing.Rectangle 0, 0, $w, $h),
          (New-Object System.Drawing.Rectangle $x, $y, $w, $h),
          [System.Drawing.GraphicsUnit]::Pixel)
        $g.Dispose()
        $crop.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
        $crop.Dispose()
      }

      "{0,-28} {1}x{2}" -f "$($player)_$slot.png", $w, $h
      $written++
    }
  }
  finally {
    $sheet.Dispose()
  }
}

""
"$written image(s) ecrite(s), $skipped ignoree(s) -> $Destination"
