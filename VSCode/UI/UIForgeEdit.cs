using System;
using System.Collections.Generic;
using FortRise;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// La fiche d'un archer forge : ses noms, sa source, ses poses, l'essai et l'export.
  ///
  /// Les deux facons d'assembler tiennent dans deux lignes voisines, et c'est
  /// volontaire. `SOURCE` pose les seize poses d'un coup depuis un personnage ;
  /// `FRAMES` les ouvre une par une. Qui veut tout choisir a la main ne touche jamais
  /// a `SOURCE` ; qui veut aller vite ne descend jamais jusqu'a `FRAMES`. Aucun mode
  /// a declarer, aucune bascule : les deux chemins mènent au meme dessin.
  /// </summary>
  public class UIForgeEdit : CustomMenuState
  {
    private const float FirstRowY = 46f;
    private const float RowStep = 16f;
    private const float RowX = 120f;
    private static readonly Vector2 PreviewPosition = new Vector2(56f, 108f);

    private ForgeDesign design;
    private UIForgePreview preview;

    /// <summary>
    /// Ce que la derniere action a repondu, affiche a droite de sa ligne. Efface des
    /// qu'on retouche le dessin : un "EXPORTE" qui survit a trois modifications
    /// devient un mensonge.
    /// </summary>
    private string notice;

    private string noticeRow;

    /// <summary>
    /// Ligne ou revenir en rouvrant la fiche.
    ///
    /// La fiche est reconstruite a chaque entree, et on en sort sans arret : les
    /// poses, la source, la voix et la musique sont autant d'ecrans qui y ramenent.
    /// Repartir de NAME a chaque retour obligeait a redescendre la liste entiere pour
    /// reprendre la ou l'on etait, ce qui se paie a chaque aller-retour - et regler un
    /// archer n'est fait que d'allers-retours.
    /// </summary>
    private static int resume;

    /// <summary>Dessin de la derniere ouverture : un autre archer repart du haut.</summary>
    private static string resumeId;

    public UIForgeEdit(MainMenu main) : base(main)
    {
    }

    public override void Create()
    {
      MainMenu.MenuState listState = ModRegisters.MenuState<UIForgeList>();

      design = UIForgeList.Editing;

      if (design == null)
      {
        MenuNav.Switch(Main, listState);
        return;
      }

      notice = null;
      noticeRow = null;

      if (!string.Equals(resumeId, design.Id, StringComparison.Ordinal))
      {
        resume = 0;
        resumeId = design.Id;
      }

      ScreenTitles.Apply(Main, ModRegisters.MenuState<UIForgeEdit>());
      Main.BackState = MenuNav.Arrive(Main, listState);
      Main.TweenBGCameraToY(2);

      preview = new UIForgePreview(PreviewPosition);
      preview.Show(design);
      Main.Add(preview);

      var rows = new List<UIMenuRow>();

      UIMenuRow nameRow = MakeRow(rows.Count, "NAME");
      nameRow.RightText = () => design.Name;
      nameRow.OnConfirmed = AskRename;
      rows.Add(nameRow);

      UIMenuRow name0Row = MakeRow(rows.Count, "TOP NAME");
      name0Row.RightText = () => design.Name0;
      name0Row.OnConfirmed = () => AskLine("TOP NAME", design.Name0, value => design.Name0 = value);
      rows.Add(name0Row);

      UIMenuRow name1Row = MakeRow(rows.Count, "BOTTOM NAME");
      name1Row.RightText = () => design.Name1;
      name1Row.OnConfirmed = () => AskLine("BOTTOM NAME", design.Name1, value => design.Name1 = value);
      rows.Add(name1Row);

      // Il n'y a plus de ligne SOURCE.
      //
      // Elle choisissait une planche pour pre-remplir les dix-neuf poses d'un coup,
      // ce qui supposait la mise en page Broforce et ne valait donc que pour elle.
      // Sur toute autre planche elle posait dix-neuf images prises au hasard, qu'il
      // fallait ensuite defaire une par une - plus long que de partir de rien.
      //
      // La planche se choisit maintenant la ou l'on choisit les images, par la ligne
      // << PLANCHE du selecteur, et elle vaut pour la pose qu'on est en train de
      // remplir plutot que pour l'archer entier.
      UIMenuRow framesRow = MakeRow(rows.Count, "FRAMES");
      framesRow.RightText = FramesLabel;
      framesRow.OnConfirmed = () => MenuNav.Push(Main, ModRegisters.MenuState<UIForgeFrames>());
      rows.Add(framesRow);

      // Juste apres les poses, et avant le reste : une image reprise ailleurs est
      // souvent trop grande, et tout ce qui suit - fenetre, couleurs, essai - se
      // juge sur un personnage a sa taille definitive.
      UIMenuRow adjustRow = MakeRow(rows.Count, "SIZE / CROP");
      adjustRow.RightText = AdjustLabel;
      adjustRow.OnConfirmed = OpenAdjust;
      rows.Add(adjustRow);

      // Les memes ecrans que les profils, sur un autre sujet. Poses juste apres les
      // poses : on recolore ce qu'on vient de composer, et une palette relevee sur un
      // archer a moitie rempli n'aurait pas ses vraies couleurs.
      UIMenuRow colorsRow = MakeRow(rows.Count, "COLORS");
      colorsRow.RightText = ColorsLabel;
      colorsRow.OnConfirmed = OpenColors;
      rows.Add(colorsRow);

      // La fenetre de decoupe se regle ici et non dans l'ecran des poses : elle vaut
      // pour toutes a la fois, et c'est en regardant la course qu'on voit qu'elle est
      // d'un pixel trop haut - donc en ayant l'apercu sous les yeux.
      UIMenuRow windowRow = MakeRow(rows.Count, "WINDOW Y");
      windowRow.RightText = () => design.WindowY.ToString();
      windowRow.OnLeft = () => MoveWindow(0, -1);
      windowRow.OnRight = () => MoveWindow(0, 1);
      rows.Add(windowRow);

      UIMenuRow windowXRow = MakeRow(rows.Count, "WINDOW X");
      windowXRow.RightText = () => design.WindowX.ToString();
      windowXRow.OnLeft = () => MoveWindow(-1, 0);
      windowXRow.OnRight = () => MoveWindow(1, 0);
      rows.Add(windowXRow);

      // Facultatif, et volontairement place apres les poses : un archer seul est un
      // archer valide. Rien ne force a en faire une paire.
      UIMenuRow altRow = MakeRow(rows.Count, "ALT COSTUME OF");
      altRow.RightText = AltLabel;
      altRow.OnLeft = () => CycleParent(-1);
      altRow.OnRight = () => CycleParent(1);
      rows.Add(altRow);

      UIMenuRow voiceRow = MakeRow(rows.Count, "VOICE");
      voiceRow.RightText = VoiceLabel;
      voiceRow.OnConfirmed = () => MenuNav.Push(Main, ModRegisters.MenuState<UIForgeVoice>());
      rows.Add(voiceRow);

      // Facultative aussi, mais jamais vide a l'arrivee : le cran AUTO la fait
      // suivre la voix plutot que de laisser le champ nul, ce que le jeu ne
      // supporte pas.
      UIMenuRow musicRow = MakeRow(rows.Count, "VICTORY MUSIC");
      musicRow.RightText = () => ForgeMusic.Label(design);
      musicRow.OnLeft = () => CycleMusic(-1);
      musicRow.OnRight = () => CycleMusic(1);

      // Valider ouvre la liste des fichiers ; les fleches font defiler les pistes du
      // jeu. Les treize pistes se connaissent par coeur et se prennent au vol, alors
      // qu'un fichier apporte se cherche dans une liste - il y en a autant qu'on en
      // depose.
      musicRow.OnConfirmed = PickMusicFile;
      rows.Add(musicRow);

      UIMenuRow hueRow = MakeRow(rows.Count, "BORROWED HUE");
      hueRow.RightText = () => ((int)design.Hue).ToString();
      hueRow.OnLeft = () => MoveHue(-10f);
      hueRow.OnRight = () => MoveHue(10f);
      rows.Add(hueRow);

      UIMenuRow testRow = MakeRow(rows.Count, "TEST IN GAME");
      testRow.RightText = () => Notice("TEST", ForgeRegister.IsRegistered(design) ? "IN GAME" : "");
      testRow.OnConfirmed = TestInGame;
      rows.Add(testRow);

      UIMenuRow exportRow = MakeRow(rows.Count, "EXPORT AS MOD");
      exportRow.RightText = () => Notice("EXPORT", "");
      exportRow.OnConfirmed = Export;
      rows.Add(exportRow);

      UIMenuRow saveRow = MakeRow(rows.Count, "SAVE");
      saveRow.OnConfirmed = SaveAndClose;
      rows.Add(saveRow);

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
      Main.MaxUICameraY = Math.Max(0f, lastY - 190f);
      Main.ToStartSelected = rows[Math.Clamp(resume, 0, rows.Count - 1)];
    }

    public override void Destroy()
    {
      // La fiche se quitte aussi par le bouton retour, qui ne passe pas par SAVE.
      // Enregistrer ici plutot que la : perdre le nom qu'on vient de taper parce
      // qu'on est ressorti par ou l'on etait entre serait une mauvaise surprise.
      ForgeStorage.Save();

      preview?.Release();
      preview = null;
    }

    private UIMenuRow MakeRow(int index, string label)
    {
      float y = FirstRowY + index * RowStep;
      var from = new Vector2(index % 2 == 0 ? -200f : 520f, y);

      return new UIMenuRow(new Vector2(RowX, y), from, label)
      {
        // Le rang est retenu au passage du focus et non a la sortie de l'ecran : au
        // moment ou Destroy s'execute, plus rien ne porte la selection.
        OnSelected = () => resume = index
      };
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// Ouvre les ecrans de couleur sur cet archer.
    ///
    /// Ce sont ceux des profils, sans copie : la porte d'entree pose le sujet et
    /// l'ecran de retour, le reste est commun. Voir ColorEditing.
    ///
    /// On entre par les familles et non par le detail couleur par couleur, comme les
    /// profils : regler une famille touche les teintes voisines d'un coup, ce qui est
    /// ce qu'on veut au premier passage.
    /// </summary>
    private void OpenColors()
    {
      ColorEditing.Subject = new ForgeColorSubject(design);
      ColorEditing.BackState = ModRegisters.MenuState<UIForgeEdit>();

      MenuNav.Push(Main, ModRegisters.MenuState<UIProfileColorGroups>());
    }

    /// <summary>Ce que porte la ligne COLORS : de quoi voir si quelque chose est regle.</summary>
    private string ColorsLabel()
    {
      ColorTrial colors = design.Colors;

      if (colors == null || colors.IsEmpty)
      {
        return "NONE";
      }

      int swaps = colors.Palette?.Count ?? 0;

      if (swaps == 0)
      {
        return "ADJUSTED";
      }

      return swaps + (colors.HasAdjustments ? " + ADJ" : "");
    }

    private string FramesLabel()
    {
      int missing = design.Missing().Count;
      int total = ForgeSlots.All.Length;
      return missing == 0 ? total + "/" + total : (total - missing) + "/" + total;
    }

    /// <summary>
    /// De qui ce dessin est le costume ALT. "NONE" est un etat normal, pas un
    /// manque : c'est celui de tout archer qui se suffit a lui-meme.
    /// </summary>
    private string AltLabel()
    {
      // Le refus doit se lire sur la ligne qu'on vient d'actionner. Sans cela,
      // appuyer sur droite ne produit rien du tout et laisse croire a une panne -
      // c'est le cas quand la forge ne contient qu'un seul archer.
      string said = Notice("ALT", null);

      if (said != null)
      {
        return said;
      }

      ForgeDesign parent = ForgeStorage.ParentOf(design);

      if (parent != null)
      {
        return Shorten(parent.Name);
      }

      // Un archer qui a lui-meme un ALT ne peut pas en devenir un : la chaine
      // s'arreterait a deux et le jeu n'en connait qu'un niveau.
      return ForgeStorage.AltOf(design) != null ? "HAS ONE" : "NONE";
    }

    /// <summary>
    /// Fait defiler les parents possibles, en passant par "aucun".
    ///
    /// Un dessin qui a deja un costume ALT ne se propose pas comme ALT d'un autre :
    /// la bascule du jeu n'a qu'un cran.
    /// </summary>
    private void CycleParent(int direction)
    {
      if (ForgeStorage.AltOf(design) != null)
      {
        Say("ALT", "IT ALREADY HAS AN ALT COSTUME");
        return;
      }

      List<ForgeDesign> parents = ForgeStorage.PossibleParents(design);

      if (parents.Count == 0)
      {
        Say("ALT", "NO OTHER ARCHER");
        return;
      }

      // Le cycle a une position de plus que la liste : celle du "aucun parent".
      int none = parents.Count;
      int position = none;

      for (int i = 0; i < parents.Count; i++)
      {
        if (parents[i].Id == design.AltOf)
        {
          position = i;
          break;
        }
      }

      int next = ((position + direction) % (none + 1) + none + 1) % (none + 1);
      design.AltOf = next == none ? "" : parents[next].Id;
      notice = null;
    }

    private void CycleMusic(int direction)
    {
      design.VictoryMusic = ForgeMusic.Next(design.VictoryMusic, direction);
    }

    /// <summary>Ouvre la liste des fichiers de la banque pour cet archer.</summary>
    private void PickMusicFile()
    {
      ForgeDesign captured = design;

      MusicEditing.Subject = captured.Name;
      MusicEditing.Get = () => captured.VictoryMusic;
      MusicEditing.Set = value =>
      {
        captured.VictoryMusic = value;
        ForgeStorage.Save();
      };

      MenuNav.Push(Main, ModRegisters.MenuState<UIMusicPicker>());
    }

    private string VoiceLabel()
    {
      int assigned = ForgeVoice.AssignedCount(design);
      string fallback = ForgeVoice.FallbackLabel(design.VoiceFallback);
      return assigned == 0 ? fallback : fallback + " +" + assigned;
    }

    private string Notice(string row, string fallback)
    {
      return noticeRow == row && notice != null ? notice : fallback;
    }

    private void Say(string row, string message)
    {
      noticeRow = row;
      notice = message;
    }

    private void MoveWindow(int dx, int dy)
    {
      design.WindowX = Math.Clamp(design.WindowX + dx, 0, 32);
      design.WindowY = Math.Clamp(design.WindowY + dy, 0, 32);
      design.Touch();
      Say(null, null);
      preview?.Show(design);
    }

    private void MoveHue(float delta)
    {
      design.Hue = (design.Hue + delta + 360f) % 360f;
      design.Touch();
      Say(null, null);
    }

    private void AskRename()
    {
      Main.Add(new VirtualKeyboard(
          "ARCHER NAME",
          design.Name,
          name => UIForgeList.RejectName(name, design),
          name =>
          {
            design.Name = UIForgeList.Clean(name);
            ForgeStorage.Save();
          }));
    }

    private void AskLine(string title, string current, Action<string> apply)
    {
      Main.Add(new VirtualKeyboard(
          title,
          current,
          value => string.IsNullOrWhiteSpace(value) ? "EMPTY NAME" : null,
          value =>
          {
            apply(value.Trim().ToUpperInvariant());
            ForgeStorage.Save();
          }));
    }

    private void OpenAdjust()
    {
      // Portee : toutes les images. L'ecran sert aux deux, et c'est l'appelant qui
      // dit laquelle - les calques l'ouvrent sur une seule.
      UIForgeAdjust.EditingSlot = null;
      MenuNav.Push(Main, ModRegisters.MenuState<UIForgeAdjust>());
    }

    /// <summary>
    /// Ce que les reglages de taille font a l'archer, en deux mots.
    ///
    /// On compte les images retouchees plutot que d'afficher un facteur : sur seize
    /// images il n'y en a pas forcement un seul, et "3 IMAGES" dit ce qu'un
    /// pourcentage moyen cacherait.
    /// </summary>
    private string AdjustLabel()
    {
      int touched = 0;
      int total = 0;

      foreach (ForgeSlot slot in ForgeSlots.All)
      {
        foreach (ForgePick pick in design.LayersOf(slot.Key))
        {
          total++;

          if (!pick.Untouched)
          {
            touched++;
          }
        }
      }

      if (touched == 0)
      {
        return "";
      }

      return touched == total ? "ALL" : touched + " IMAGES";
    }

    private void TestInGame()
    {
      ForgeStorage.Save();

      // Avant l'appel : c'est lui qui pose l'archer, et le mot a dire n'est pas le
      // meme selon qu'on vient de le poser ou de le refaire.
      bool again = ForgeRegister.IsRegistered(design);

      string failure = ForgeRegister.Register(design);

      if (failure != null)
      {
        Say("TEST", failure);
        Sounds.ui_invalid.Play(160f, 1f);
        return;
      }

      // Un archer refait ne change d'aspect qu'a la prochaine apparition : le joueur
      // deja en piste garde les planches avec lesquelles il est ne. Sortir de la
      // partie et en relancer une suffit, et le dire evite de croire que rien n'a
      // pris.
      Say("TEST", again ? "REFAIT - RELANCER LA PARTIE" : "IN GAME");
    }

    private void Export()
    {
      ForgeStorage.Save();
      string failure = ForgeExport.Write(design);

      if (failure != null)
      {
        Say("EXPORT", failure);
        Sounds.ui_invalid.Play(160f, 1f);
        return;
      }

      // Le mod n'est charge qu'au prochain lancement : le dire evite d'aller le
      // chercher dans la liste des archers tout de suite, et de croire que l'export
      // a echoue parce qu'il n'y est pas.
      Say("EXPORT", "ON RESTART");
    }

    private void SaveAndClose()
    {
      ForgeStorage.Save();
      MenuNav.Switch(Main, ModRegisters.MenuState<UIForgeList>());
    }

    /// <summary>
    /// Nom de planche raccourci par la fin, en gardant le debut.
    ///
    /// Les noms de planches Broforce se distinguent par leur debut - "mookSuicide_P1"
    /// et "mookSuicide_P2" ne different qu'a l'avant-derniere lettre, mais on ne les
    /// confond pas si l'on voit le debut. Tronquer par le debut les rendrait tous
    /// identiques.
    /// </summary>
    internal static string Shorten(string text)
    {
      if (string.IsNullOrEmpty(text))
      {
        return "";
      }

      string upper = text.ToUpperInvariant();
      return upper.Length > 16 ? upper.Substring(0, 15) + "." : upper;
    }
  }
}
