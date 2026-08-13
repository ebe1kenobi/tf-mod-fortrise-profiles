using System;
using System.Collections.Generic;
using FortRise;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// La taille et le rognage des images : sur toutes d'un coup, ou sur une seule.
  ///
  /// C'est ce qui manquait pour se servir d'une planche qu'on n'a pas dessinee. Le
  /// cadre orange des apercus dit depuis peu de combien un personnage depasse un
  /// archer du jeu ; il fallait bien pouvoir y repondre autrement qu'en retournant
  /// redecouper ses images ailleurs.
  ///
  /// Les deux portees sont le meme ecran, et c'est voulu : ce sont les memes
  /// reglages, appliques a un nombre different d'images. Un ecran par portee aurait
  /// double les lignes pour ne rien changer aux gestes.
  ///
  /// Rien n'est ecrit dans le vivier : une retouche est un reglage du dessin, elle se
  /// defait et se change. La ligne REINITIALISER n'est pas une precaution, c'est la
  /// contrepartie normale de ce choix.
  /// </summary>
  public class UIForgeAdjust : CustomMenuState
  {
    /// <summary>
    /// L'emplacement dont on regle l'image, ou null pour toutes les images.
    ///
    /// Statique, comme partout ici : MainMenu instancie les etats lui-meme et
    /// CallStateFunc n'a pas de place pour un argument.
    /// </summary>
    internal static string EditingSlot;

    /// <summary>Rang du calque regle dans l'empilement, quand la portee est une image.</summary>
    internal static int EditingLayer;

    /// <summary>Ou l'on retourne : les calques quand on vient d'eux, la fiche sinon.</summary>
    private const float FirstRowY = 60f;
    private const float RowStep = 15f;
    private const float RowX = 30f;
    private static readonly Vector2 PreviewPosition = new Vector2(250f, 104f);

    private const int ScaleStep = 5;
    private const int ScaleMin = 5;
    private const int ScaleMax = 400;

    // Quinze degres : le pas divise 90, donc les quarts de tour - les seuls angles
    // sans perte - tombent juste en six pressions.
    private const int RotationStep = 15;

    // Un rognage se compte en pixels d'image source. Le plafond n'est pas une regle
    // du jeu, seulement de quoi ne pas avoir a tenir la touche : au-dela on ne rogne
    // plus, on redecoupe.
    private const int CropMax = 64;

    private ForgeDesign design;
    private ForgeSlot slot;
    private UIForgeCellThumb thumb;

    public UIForgeAdjust(MainMenu main) : base(main)
    {
    }

    public override void Create()
    {
      design = UIForgeList.Editing;
      slot = EditingSlot == null ? null : ForgeSlots.Get(EditingSlot);

      // Cet ecran s'ouvre depuis DEUX endroits - la fiche de l'archer et l'ecran des
      // calques - et c'est la pile qui sait lequel, plutot qu'un drapeau que chaque
      // appelant devait penser a poser.
      MainMenu.MenuState home = MenuNav.Arrive(Main, ModRegisters.MenuState<UIForgeEdit>());

      if (design == null)
      {
        MenuNav.Switch(Main, ModRegisters.MenuState<UIForgeList>());
        return;
      }

      ScreenTitles.Apply(Main, ModRegisters.MenuState<UIForgeAdjust>());
      Main.BackState = home;

      Main.TweenBGCameraToY(2);

      Main.Add(new UIPickerHeader(new Vector2(160f, 34f), design.Name,
          slot == null ? "ALL FRAMES" : slot.Label));

      thumb = new UIForgeCellThumb(PreviewPosition);
      Main.Add(thumb);
      Refresh();

      var rows = new List<UIMenuRow>();

      UIMenuRow scaleRow = MakeRow(rows.Count, "SIZE");
      scaleRow.RightText = () => Label(p => p.Scale, "%");
      scaleRow.OnLeft = () => Scale(-ScaleStep);
      scaleRow.OnRight = () => Scale(ScaleStep);
      rows.Add(scaleRow);

      // Les quatre bords separement plutot qu'une marge unique : une image decoupee
      // dans une grille n'a presque jamais le meme vide des quatre cotes, et une
      // valeur commune obligerait a rogner le plus grand partout.
      rows.Add(CropRow(rows.Count, "CROP LEFT", p => p.CropLeft, (p, v) => p.CropLeft = v));
      rows.Add(CropRow(rows.Count, "CROP RIGHT", p => p.CropRight, (p, v) => p.CropRight = v));
      rows.Add(CropRow(rows.Count, "CROP TOP", p => p.CropTop, (p, v) => p.CropTop = v));
      rows.Add(CropRow(rows.Count, "CROP BOTTOM", p => p.CropBottom, (p, v) => p.CropBottom = v));

      // Le miroir horizontal n'est pas un effet mais une reprise : les archers du jeu
      // sont dessines tournes vers la droite, et une planche dessinee vers la gauche
      // donne un personnage qui court a reculons. Sur toutes les images a la fois,
      // c'est le geste qui remet une planche entiere dans le bon sens.
      UIMenuRow flipXRow = MakeRow(rows.Count, "MIROIR H");
      flipXRow.RightText = () => Label(p => p.FlipX ? 1 : 0, "", Yes);
      flipXRow.OnConfirmed = () => Flip(true);
      flipXRow.OnLeft = () => Flip(true);
      flipXRow.OnRight = () => Flip(true);
      rows.Add(flipXRow);

      UIMenuRow flipYRow = MakeRow(rows.Count, "MIROIR V");
      flipYRow.RightText = () => Label(p => p.FlipY ? 1 : 0, "", Yes);
      flipYRow.OnConfirmed = () => Flip(false);
      flipYRow.OnLeft = () => Flip(false);
      flipYRow.OnRight = () => Flip(false);
      rows.Add(flipYRow);

      // Par quarts de tour d'abord, puis par crans de quinze degres : les quarts sont
      // exacts et sont ceux dont on se sert - coucher un cadavre, redresser une
      // planche - alors qu'un angle quelconque abime un dessin au pixel.
      UIMenuRow rotationRow = MakeRow(rows.Count, "ROTATION");
      rotationRow.RightText = () => Label(p => p.Rotation, "'");
      rotationRow.OnLeft = () => Rotate(-RotationStep);
      rotationRow.OnRight = () => Rotate(RotationStep);
      rows.Add(rotationRow);

      // Le detourage lit les pixels de chaque image et retire ce qui est transparent
      // autour. C'est le rognage qu'on aurait fini par faire a la main, en seize fois.
      UIMenuRow trimRow = MakeRow(rows.Count, "DETOURER");
      trimRow.RightText = () => "AUTO";
      trimRow.OnConfirmed = Trim;
      rows.Add(trimRow);

      UIMenuRow resetRow = MakeRow(rows.Count, "REINITIALISER");
      resetRow.OnConfirmed = Reset;
      rows.Add(resetRow);

      for (int i = 0; i < rows.Count; i++)
      {
        if (i > 0)
        {
          rows[i].UpItem = rows[i - 1];
        }

        if (i + 1 < rows.Count)
        {
          rows[i].DownItem = rows[i + 1];
        }
      }

      Main.Add(rows);

      float lastY = FirstRowY + (rows.Count - 1) * RowStep;
      Main.MaxUICameraY = Math.Max(0f, lastY - 180f);
      // Le curseur retrouve la ligne qu'on avait quittee, et non la premiere :
      // sans cela, chaque aller-retour dans un sous-ecran oblige a redescendre.
      MenuNav.Track(Main, rows);
      Main.ToStartSelected = rows[MenuNav.Resume(Main, rows.Count)];
    }

    public override void Destroy()
    {
      ForgeStorage.Save();

      thumb?.Release();
      thumb = null;
    }

    // ------------------------------------------------------------------

    private UIMenuRow MakeRow(int index, string label)
    {
      float y = FirstRowY + index * RowStep;
      var from = new Vector2(index % 2 == 0 ? -260f : 580f, y);

      return new UIMenuRow(new Vector2(RowX, y), from, label) { ContentWidth = 150f };
    }

    private UIMenuRow CropRow(
        int index, string label, Func<ForgePick, int> read, Action<ForgePick, int> write)
    {
      UIMenuRow row = MakeRow(index, label);
      row.RightText = () => Label(read, "");
      row.OnLeft = () => Crop(read, write, -1);
      row.OnRight = () => Crop(read, write, 1);
      return row;
    }

    /// <summary>
    /// Les images que l'ecran regle : celle d'un calque, ou toutes celles du dessin.
    /// </summary>
    private List<ForgePick> Targets()
    {
      var picks = new List<ForgePick>();

      if (slot != null)
      {
        List<ForgePick> stack = design.LayersOf(slot.Key);

        if (EditingLayer >= 0 && EditingLayer < stack.Count)
        {
          picks.Add(stack[EditingLayer]);
        }

        return picks;
      }

      foreach (ForgeSlot each in ForgeSlots.All)
      {
        picks.AddRange(design.LayersOf(each.Key));
      }

      return picks;
    }

    /// <summary>
    /// La valeur commune aux images reglees, ou MIXED.
    ///
    /// Une portee qui couvre seize images peut en trouver deux reglees autrement -
    /// on vient peut-etre d'en corriger une seule. Afficher la premiere ferait croire
    /// a un reglage d'ensemble qui n'existe pas ; le dire permet de choisir en
    /// connaissance de cause, la valeur suivante posee ecrasant tout.
    /// </summary>
    private string Label(Func<ForgePick, int> read, string unit)
    {
      return Label(read, unit, null);
    }

    /// <param name="format">
    /// Comment ecrire la valeur commune, ou null pour l'ecrire telle quelle suivie de
    /// son unite. Sert aux lignes qui n'ont pas une valeur mais un etat : un miroir
    /// vaut OUI ou rien, et "1" ne se lit pas.
    /// </param>
    private string Label(Func<ForgePick, int> read, string unit, Func<int, string> format)
    {
      List<ForgePick> picks = Targets();

      if (picks.Count == 0)
      {
        return "-";
      }

      int first = read(picks[0]);

      foreach (ForgePick pick in picks)
      {
        if (read(pick) != first)
        {
          return "MIXED";
        }
      }

      return format != null ? format(first) : first + unit;
    }

    /// <summary>Un etat plutot qu'un nombre : ce qui n'est pas mis ne s'affiche pas.</summary>
    private static string Yes(int value)
    {
      return value != 0 ? "OUI" : "";
    }

    private void Scale(int step)
    {
      List<ForgePick> picks = Targets();

      if (picks.Count == 0)
      {
        Sounds.ui_invalid.Play(160f, 1f);
        return;
      }

      // On part de la valeur affichee et non de celle de chaque image : sur une
      // portee melangee, avancer chacune de son cote garderait l'ecart pour
      // toujours. La premiere pression aligne, les suivantes deplacent.
      int value = Common(picks, p => p.Scale, 100) + step;
      value = Math.Clamp(value, ScaleMin, ScaleMax);

      foreach (ForgePick pick in picks)
      {
        pick.Scale = value;
      }

      Changed();
    }

    private void Crop(Func<ForgePick, int> read, Action<ForgePick, int> write, int step)
    {
      List<ForgePick> picks = Targets();

      if (picks.Count == 0)
      {
        Sounds.ui_invalid.Play(160f, 1f);
        return;
      }

      int value = Math.Clamp(Common(picks, read, 0) + step, 0, CropMax);

      foreach (ForgePick pick in picks)
      {
        write(pick, value);
      }

      Changed();
    }

    /// <summary>
    /// Met ou retire le miroir sur toutes les images reglees.
    ///
    /// Toutes prennent le meme etat, et non chacune l'inverse du sien : sur une
    /// portee melangee, inverser chacune de son cote garderait le melange pour
    /// toujours, et l'on ne pourrait jamais remettre une planche d'aplomb.
    /// </summary>
    private void Flip(bool horizontal)
    {
      List<ForgePick> picks = Targets();

      if (picks.Count == 0)
      {
        Sounds.ui_invalid.Play(160f, 1f);
        return;
      }

      bool value = !(Common(picks, p => (horizontal ? p.FlipX : p.FlipY) ? 1 : 0, 0) != 0);

      foreach (ForgePick pick in picks)
      {
        if (horizontal)
        {
          pick.FlipX = value;
        }
        else
        {
          pick.FlipY = value;
        }
      }

      Changed();
    }

    private void Rotate(int step)
    {
      List<ForgePick> picks = Targets();

      if (picks.Count == 0)
      {
        Sounds.ui_invalid.Play(160f, 1f);
        return;
      }

      // L'angle tourne en rond plutot que de buter : 345 puis un cran a droite doit
      // revenir a zero, sinon il faudrait vingt-trois pressions pour y retourner.
      int value = (Common(picks, p => p.Rotation, 0) + step + 360) % 360;

      foreach (ForgePick pick in picks)
      {
        pick.Rotation = value;
      }

      Changed();
    }

    /// <summary>La valeur commune, ou celle de repli si les images divergent.</summary>
    private static int Common(List<ForgePick> picks, Func<ForgePick, int> read, int fallback)
    {
      int first = read(picks[0]);

      foreach (ForgePick pick in picks)
      {
        if (read(pick) != first)
        {
          return fallback;
        }
      }

      return first;
    }

    /// <summary>
    /// Rogne chaque image sur ses pixels opaques.
    ///
    /// Image par image et non d'une valeur commune : c'est tout l'interet: chacune a
    /// son propre vide autour d'elle, et une valeur commune ne detourerait bien
    /// qu'une seule.
    ///
    /// Le facteur de taille n'est pas touche : detourer sert a supprimer du vide, pas
    /// a changer la taille du personnage.
    /// </summary>
    private void Trim()
    {
      int done = 0;

      foreach (ForgePick pick in Targets())
      {
        ForgeSource source = ForgeBank.Find(pick.Source);

        if (source == null)
        {
          continue;
        }

        // Les pixels du FICHIER, sans les retouches en cours : les marges se comptent
        // sur la source, comme les valeurs de rognage elles-memes.
        Color[] pixels = ForgeBank.ReadCell(source, pick.Cell, out var size);

        if (pixels == null)
        {
          continue;
        }

        var margins = ForgePixels.Margins(pixels, size);

        pick.CropLeft = margins.Left;
        pick.CropRight = margins.Right;
        pick.CropTop = margins.Top;
        pick.CropBottom = margins.Bottom;
        done++;
      }

      if (done == 0)
      {
        Sounds.ui_invalid.Play(160f, 1f);
        return;
      }

      Changed();
    }

    private void Reset()
    {
      List<ForgePick> picks = Targets();

      if (picks.Count == 0)
      {
        Sounds.ui_invalid.Play(160f, 1f);
        return;
      }

      foreach (ForgePick pick in picks)
      {
        pick.Scale = 100;
        pick.CropLeft = 0;
        pick.CropRight = 0;
        pick.CropTop = 0;
        pick.CropBottom = 0;
        pick.FlipX = false;
        pick.FlipY = false;
        pick.Rotation = 0;
      }

      Changed();
    }

    private void Changed()
    {
      design.Touch();
      Refresh();
      Sounds.ui_move2.Play(160f, 1f);
    }

    /// <summary>
    /// Refait la vignette. La pose debout quand la portee est le dessin entier :
    /// c'est celle qu'on connait par coeur, donc celle sur laquelle un changement de
    /// taille se juge.
    /// </summary>
    private void Refresh()
    {
      thumb?.Show(design, slot == null ? "stand" : slot.Key);
    }
  }
}
