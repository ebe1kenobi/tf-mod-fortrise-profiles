using System;
using System.Collections.Generic;
using System.IO;
using FortRise;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace TFModFortRiseProfiles
{
  /// <summary>
  /// Le choix du personnage source : la voie rapide.
  ///
  /// Valider une planche pose les seize poses d'un coup, aux coordonnees canoniques.
  /// C'est ce qui rend la forge utilisable : sans cet ecran il faudrait seize
  /// parcours dans trente mille images pour obtenir ce qu'on obtient ici en deux
  /// pressions.
  ///
  /// Seules les planches qui s'y pretent sont proposees - cases de 32x32 et grille
  /// assez grande pour que les seize cases existent. Une planche de decor n'a pas de
  /// pose debout a la case (0,0), la proposer ferait des archers vides.
  ///
  /// L'apercu suit la ligne survolee et montre la course : c'est en la voyant qu'on
  /// choisit, pas en lisant un nom de fichier.
  /// </summary>
  public class UIForgeSource : CustomMenuState
  {
    private const float FirstRowY = 52f;
    private const float RowStep = 15f;
    private const float RowX = 30f;
    private static readonly Vector2 PreviewPosition = new Vector2(250f, 110f);

    private ForgeDesign design;
    private UIForgePreview preview;

    /// <summary>
    /// Dessin d'essai, refait pour chaque ligne survolee.
    ///
    /// L'apercu ne peut pas montrer la planche survolee sans l'assembler, et
    /// l'assembler dans le vrai dessin ecraserait des poses qu'on n'a pas encore
    /// choisi de remplacer. D'ou cette copie jetable : on ne touche au dessin qu'a
    /// la validation.
    /// </summary>
    private ForgeDesign trial;

    public UIForgeSource(MainMenu main) : base(main)
    {
    }

    public override void Create()
    {
      MainMenu.MenuState editState = ModRegisters.MenuState<UIForgeEdit>();

      design = UIForgeList.Editing;

      if (design == null)
      {
        Main.State = ModRegisters.MenuState<UIForgeList>();
        return;
      }

      ScreenTitles.Apply(Main, ModRegisters.MenuState<UIForgeSource>());
      Main.BackState = editState;
      Main.TweenBGCameraToY(2);

      preview = new UIForgePreview(PreviewPosition);
      Main.Add(preview);

      trial = new ForgeDesign { Name = "TRIAL" };

      List<ForgeSource> sources = ForgeBank.PrefillableSources();

      if (sources.Count == 0)
      {
        Main.Add(new UIForgeBankHint(new Vector2(160f, 120f)));
        Main.MaxUICameraY = 0f;
        Main.ToStartSelected = null;
        return;
      }

      var rows = new List<UIMenuRow>();

      for (int i = 0; i < sources.Count; i++)
      {
        ForgeSource source = sources[i];
        float y = FirstRowY + i * RowStep;
        var from = new Vector2(i % 2 == 0 ? -260f : 580f, y);

        var row = new UIMenuRow(new Vector2(RowX, y), from, UIForgeEdit.Shorten(source.Name))
        {
          ContentWidth = 150f,
          RightText = () => Filled(source) + "/" + ForgeSlots.All.Length,
          OnConfirmed = () => Apply(source, editState),
          OnSelected = () => ShowTrial(source)
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
      Main.ToStartSelected = rows[0];
    }

    public override void Destroy()
    {
      preview?.Release();
      preview = null;
      trial = null;
    }

    /// <summary>Combien des seize poses cette planche sait remplir.</summary>
    private static int Filled(ForgeSource source)
    {
      int count = 0;

      foreach (ForgeSlot slot in ForgeSlots.All)
      {
        ForgeCell? cell = ForgeLayout.Of(slot.Key);

        if (cell != null && ForgeBank.Has(source, cell.Value))
        {
          count++;
        }
      }

      return count;
    }

    private void ShowTrial(ForgeSource source)
    {
      trial.Picks.Clear();
      trial.WindowX = design.WindowX;
      trial.WindowY = design.WindowY;
      trial.Prefill(source);
      preview?.Show(trial);
    }

    private void Apply(ForgeSource source, MainMenu.MenuState editState)
    {
      // Les poses deja choisies sont conservees. Changer de source apres avoir
      // corrige trois poses a la main ne doit pas effacer ces trois corrections :
      // on essaie une autre planche, on ne recommence pas de zero. Repartir a neuf
      // se fait en retirant les poses depuis l'ecran des images.
      design.Prefill(source, keepEdits: true);
      ForgeStorage.Save();
      Main.State = editState;
    }
  }

  /// <summary>
  /// Ce qui s'affiche quand aucune planche ne convient.
  ///
  /// Un ecran vide laisse croire a une panne. Ici il n'y en a pas : il manque
  /// seulement des images, et le joueur ne peut pas le deviner. On lui dit donc ou
  /// les mettre, et on rappelle l'option de decoupage sans laquelle des poses
  /// disparaissent en silence.
  /// </summary>
  public class UIForgeBankHint : UIForgePanel
  {
    public UIForgeBankHint(Vector2 position) : base(position)
    {
    }

    public override void Render()
    {
      base.Render();

      string[] lines =
      {
        "AUCUNE PLANCHE UTILISABLE",
        "",
        "DECOUPER DES PNG AVEC",
        "SLICE_SHEETS.PY --KEEP-DUPLICATES",
        "PUIS LES DEPOSER DANS",

        // Le chemin complet deborde de l'ecran et n'apprend rien : ce qu'on cherche,
        // c'est le dossier a ouvrir, pas l'endroit ou le jeu est installe.
        "SAVES/" + Path.GetFileName(Path.GetDirectoryName(ForgeBank.Root) ?? "")
            + "/" + Path.GetFileName(ForgeBank.Root)
      };

      for (int i = 0; i < lines.Length; i++)
      {
        Draw.OutlineTextCentered(TFGame.Font, MenuText.Safe(lines[i]),
            Position + new Vector2(0f, i * 12f), Color.Gray, Color.Black);
      }
    }

  }
}
