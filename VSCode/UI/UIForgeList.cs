using System;
using System.Collections.Generic;
using FortRise;
using Microsoft.Xna.Framework;
using TowerFall;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// La liste des archers forges, ouverte depuis l'ecran des profils.
  ///
  /// Meme forme que la liste des profils, dont elle reprend la construction : une
  /// ligne de creation en tete, une ligne par archer, la suppression sur le bouton
  /// Alt. La difference tient a ce qui s'affiche a droite - l'etat de l'archer, qui
  /// est ce qu'on vient verifier : combien de poses restent a remplir, et s'il a deja
  /// ete essaye.
  /// </summary>
  public class UIForgeList : CustomMenuState
  {
    /// <summary>
    /// Archer que les ecrans suivants doivent ouvrir. Passe par un champ statique
    /// parce que MainMenu instancie les etats lui-meme : CallStateFunc n'a pas de
    /// place pour un argument.
    /// </summary>
    internal static ForgeDesign Editing;

    private const float FirstRowY = 54f;
    private const float RowStep = 16f;
    private const float RowX = 108f;
    private static readonly Vector2 PreviewPosition = new Vector2(50f, 106f);

    private readonly List<MenuItem> items = new List<MenuItem>();
    private UIForgePreview preview;

    public UIForgeList(MainMenu main) : base(main)
    {
    }

    public override void Create()
    {
      // Relit le vivier a chaque entree dans la forge. Sans cela le scan des
      // repertoires et des index.json n'a lieu qu'une fois par lancement du jeu :
      // un decoupage relance a cote continue d'etre juge sur les anciennes
      // dimensions, et les images refusees pour "taille inattendue".
      ForgeBank.Refresh();

      ScreenTitles.Apply(Main, ModRegisters.MenuState<UIForgeList>());

      Main.BackState = MenuNav.Arrive(Main, ModRegisters.MenuState<UIProfilesMenu>());
      Main.TweenBGCameraToY(2);

      preview = new UIForgePreview(PreviewPosition);
      Main.Add(preview);
      items.Clear();
      items.Add(preview);

      // Zero n'est le bon rang qu'a la premiere visite : au retour d'un sous-ecran,
      // on veut la ligne qu'on avait quittee.
      Build(MenuNav.Resume(Main, int.MaxValue));
    }

    public override void Destroy()
    {
      preview?.Release();
      items.Clear();
      preview = null;
    }

    private void Build(int selectIndex)
    {
      var rows = new List<UIMenuRow>();

      UIMenuRow addRow = MakeRow(0, "+ NEW ARCHER");
      addRow.OnConfirmed = AskNew;
      rows.Add(addRow);

      // Le vivier est la matiere premiere : vide, la forge ne peut rien faire, et le
      // dire ici evite de laisser decouvrir des listes vides trois ecrans plus loin.
      //
      // On compte les planches exploitables et non toutes celles presentes : les
      // ecrans de choix ne montrent que celles-la, et annoncer un nombre plus grand
      // ferait chercher les manquantes.
      int sources = ForgeBank.PickableCount;

      // Reprendre un archer installe plutot que d'en dessiner un. La ligne est en
      // haut avec la creation parce que c'est la meme chose : les deux facons de
      // commencer un archer, l'une a partir de rien, l'autre a partir d'un existant.
      UIMenuRow importRow = MakeRow(1, "IMPORT ARCHER");
      importRow.OnConfirmed = () => MenuNav.Push(Main, ModRegisters.MenuState<UIForgeImport>());
      rows.Add(importRow);

      UIMenuRow bankRow = MakeRow(2, "IMAGE BANK");
      bankRow.RightText = () => BankLabel(sources);
      rows.Add(bankRow);

      foreach (ForgeDesign design in ForgeStorage.Designs)
      {
        ForgeDesign captured = design;
        UIMenuRow row = MakeRow(rows.Count, DisplayName(captured));
        row.RightText = () => Status(captured);
        row.OnConfirmed = () => Edit(captured);
        row.OnAlt = () => AskDelete(captured);
        row.AltGuide = "DELETE";
        row.OnSelected = () => preview?.Show(captured);
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
      items.AddRange(rows);

      float lastY = FirstRowY + (rows.Count - 1) * RowStep;
      Main.MaxUICameraY = Math.Max(0f, lastY - 190f);

      MenuNav.Track(Main, rows);
      MenuItem toSelect = rows[Math.Clamp(selectIndex, 0, rows.Count - 1)];
      Main.ToStartSelected = toSelect;

      if (!Main.Transitioning)
      {
        toSelect.Selected = true;
      }
    }

    private UIMenuRow MakeRow(int index, string label)
    {
      float y = FirstRowY + index * RowStep;
      var from = new Vector2(index % 2 == 0 ? -200f : 520f, y);

      var row = new UIMenuRow(new Vector2(RowX, y), from, label);
      row.OnSelected = () => preview?.Show(null);
      return row;
    }

    private static string DisplayName(ForgeDesign design)
    {
      return string.IsNullOrEmpty(design.Name) ? "(UNNAMED)" : design.Name;
    }

    /// <summary>L'etat du vivier, en deux mots.</summary>
    private static string BankLabel(int sources)
    {
      if (sources == 0)
      {
        return "EMPTY";
      }

      return sources + " SHEETS";
    }

    /// <summary>
    /// Ce qu'il reste a faire sur un archer, en trois mots.
    ///
    /// L'ordre des reponses est celui dans lequel on veut les lire : ce qui manque
    /// passe avant ce qui est fait, parce que c'est ce qui appelle une action.
    /// </summary>
    private static string Status(ForgeDesign design)
    {
      int missing = design.Missing().Count;

      if (missing > 0)
      {
        return missing + " MISSING";
      }

      return ForgeRegister.IsRegistered(design) ? "IN GAME" : "READY";
    }

    private void AskNew()
    {
      Main.Add(new VirtualKeyboard(
          "NEW ARCHER",
          "",
          name => RejectName(name, null),
          name =>
          {
            var design = new ForgeDesign { Name = Clean(name), Name0 = Clean(name), Name1 = "FORGE" };
            ForgeStorage.Designs.Add(design);
            ForgeStorage.Save();
            Edit(design);
          }));
    }

    private void AskDelete(ForgeDesign design)
    {
      Dialogs.Confirm(Main, "DELETE ARCHER", DisplayName(design), () =>
      {
        int index = ForgeStorage.Designs.IndexOf(design);
        ForgeStorage.Designs.Remove(design);
        ForgeStorage.Save();
        Rebuild(index);
      });
    }

    private void Rebuild(int removedIndex)
    {
      foreach (MenuItem item in items)
      {
        if (item != preview)
        {
          Main.Remove(item);
        }
      }

      items.RemoveAll(item => item != preview);
      preview?.Show(null);

      // removedIndex compte dans la liste des archers ; les lignes ont en plus la
      // creation, l'import et le vivier en tete.
      Build(removedIndex + 3);
    }

    private void Edit(ForgeDesign design)
    {
      Editing = design;
      MenuNav.Push(Main, ModRegisters.MenuState<UIForgeEdit>());
    }

    /// <summary>
    /// Regles de nommage. Le nom sert d'identifiant de sprite et de nom de repertoire
    /// a l'export : deux archers homonymes ecraseraient leurs planches, et un nom a
    /// espaces ou a ponctuation ferait un chemin qu'on ne veut pas avoir a citer.
    /// </summary>
    internal static string RejectName(string name, ForgeDesign except)
    {
      string cleaned = Clean(name);

      if (string.IsNullOrEmpty(cleaned))
      {
        return "EMPTY NAME";
      }

      if (!ForgeStorage.NameAvailable(cleaned, except))
      {
        return "ALREADY IN THE LIST";
      }

      return null;
    }

    /// <summary>Ne garde que les lettres et les chiffres, en majuscules.</summary>
    internal static string Clean(string name)
    {
      if (string.IsNullOrEmpty(name))
      {
        return "";
      }

      var kept = new System.Text.StringBuilder(name.Length);

      foreach (char c in name.ToUpperInvariant())
      {
        if (char.IsLetterOrDigit(c))
        {
          kept.Append(c);
        }
      }

      return kept.ToString();
    }
  }
}
