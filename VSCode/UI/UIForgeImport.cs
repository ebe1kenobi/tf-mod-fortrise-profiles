using System;
using System.Collections.Generic;
using FortRise;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Le choix d'un archer installe a reprendre dans la forge.
  ///
  /// Ce que la forge exporte, elle sait le relire : un archer exporte, essaye en
  /// jeu, puis a corriger n'oblige plus a garder son dessin d'origine - et un mod
  /// d'archer fait par quelqu'un d'autre devient un point de depart plutot qu'une
  /// chose a regarder.
  ///
  /// L'import COPIE : les poses entrent dans le vivier et le dessin les designe. Le
  /// mod d'origine n'est pas touche, et l'archer repris s'en detache aussitot - le
  /// desinstaller ne casse rien de ce qui a ete importe.
  /// </summary>
  public class UIForgeImport : CustomMenuState
  {
    private const float FirstRowY = 56f;
    private const float RowStep = 15f;
    private const float RowX = 20f;

    public UIForgeImport(MainMenu main) : base(main)
    {
    }

    public override void Create()
    {
      MainMenu.MenuState listState = ModRegisters.MenuState<UIForgeList>();

      ScreenTitles.Apply(Main, ModRegisters.MenuState<UIForgeImport>());
      Main.BackState = MenuNav.Arrive(Main, listState);
      Main.TweenBGCameraToY(2);

      List<ForgeImportCandidate> candidates = ForgeImport.Candidates();

      if (candidates.Count == 0)
      {
        Main.Add(new UIForgeImportHint(new Vector2(160f, 120f)));
        Main.MaxUICameraY = 0f;
        Main.ToStartSelected = null;
        return;
      }

      var rows = new List<UIMenuRow>();

      for (int i = 0; i < candidates.Count; i++)
      {
        ForgeImportCandidate candidate = candidates[i];

        float y = FirstRowY + i * RowStep;
        var from = new Vector2(i % 2 == 0 ? -260f : 580f, y);

        var row = new UIMenuRow(new Vector2(RowX, y), from, Label(candidate.Name))
        {
          ContentWidth = 280f,

          // Le mod d'origine plutot que la taille des images : c'est ce qui distingue
          // deux archers homonymes, et le seul renseignement qu'on ait envie d'avoir
          // avant de choisir.
          RightText = () => Label(candidate.Mod),
          OnConfirmed = () => Take(candidate, listState)
        };

        rows.Add(row);
      }

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
    }

    /// <summary>
    /// Reprend l'archer et ouvre sa fiche.
    ///
    /// On enchaine sur l'edition plutot que de revenir a la liste : un archer importe
    /// demande presque toujours un coup d'oeil - au cadrage, aux poses que la planche
    /// n'avait pas - et c'est le moment de le donner.
    /// </summary>
    private void Take(ForgeImportCandidate candidate, MainMenu.MenuState listState)
    {
      ForgeDesign design = ForgeImport.Import(candidate);

      if (design == null)
      {
        // La raison est au journal : ici il n'y a pas la place de l'ecrire, et elle
        // tient rarement en trois mots.
        Sounds.ui_invalid.Play(160f, 1f);
        return;
      }

      // Seul le nom court est un identifiant - repertoire d'export et id de sprite -
      // et lui seul se nettoie. Les deux lignes affichees sont du texte : les priver
      // de leurs espaces recollerait les mots.
      design.Name = Available(UIForgeList.Clean(design.Name));

      ForgeStorage.Designs.Add(design);
      ForgeStorage.Save();

      Sounds.ui_click.Play(160f, 1f);

      UIForgeList.Editing = design;
      MenuNav.Switch(Main, ModRegisters.MenuState<UIForgeEdit>());
    }

    /// <summary>
    /// Un nom libre, derive de celui demande.
    ///
    /// Le nom sert d'identifiant de sprite et de repertoire a l'export : importer
    /// deux fois le meme archer, ou un archer homonyme d'un dessin existant, ferait
    /// deux archers qui ecrasent leurs planches. On numerote plutot que de refuser -
    /// l'import est parfois justement la pour comparer deux versions.
    /// </summary>
    private static string Available(string name)
    {
      if (string.IsNullOrEmpty(name))
      {
        name = "IMPORT";
      }

      if (ForgeStorage.NameAvailable(name, null))
      {
        return name;
      }

      for (int suffix = 2; suffix < 100; suffix++)
      {
        string tried = name + suffix;

        if (ForgeStorage.NameAvailable(tried, null))
        {
          return tried;
        }
      }

      return name;
    }

    private static string Label(string text)
    {
      return UIForgeEdit.Shorten((text ?? "").ToUpperInvariant());
    }
  }

  /// <summary>Ce qui s'affiche quand aucun archer n'est importable.</summary>
  public class UIForgeImportHint : UIForgePanel
  {
    public UIForgeImportHint(Vector2 position) : base(position)
    {
    }

    public override void Render()
    {
      base.Render();

      string[] lines =
      {
        "NO ARCHER TO IMPORT",
        "",
        "ARCHERS ARE READ FROM",
        "THE INSTALLED MODS, SHEETS",
        "AND SPRITEDATA INCLUDED"
      };

      for (int i = 0; i < lines.Length; i++)
      {
        Draw.OutlineTextCentered(TFGame.Font, MenuText.Safe(lines[i]),
            Position + new Vector2(0f, i * 12f), Color.Gray, Color.Black);
      }
    }
  }
}
