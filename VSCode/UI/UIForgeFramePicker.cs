using System;
using System.Collections.Generic;
using FortRise;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using TowerFall;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Le choix d'une image pour un emplacement : d'abord la planche, puis la case.
  ///
  /// Deux listes successives dans le meme ecran plutot que deux ecrans. C'est le
  /// meme geste - on cherche une image - et le decouper en deux etats obligerait a
  /// retenir lequel ramene ou, pour un gain nul : le bouton retour remonte d'un
  /// niveau, ce qui est exactement ce qu'on attend de lui.
  ///
  /// Toutes les planches sont proposees ici, pas seulement celles qui savent
  /// pre-remplir : une pose manquante peut tres bien se trouver dans une planche
  /// dont la mise en page ne ressemble a rien. C'est meme le seul moyen de terminer
  /// un archer dont la source a des trous.
  /// </summary>
  public class UIForgeFramePicker : CustomMenuState
  {
    /// <summary>
    /// Vrai si l'ecran a ete ouvert depuis les calques, et doit y ramener.
    ///
    /// Le selecteur a deux portes d'entree qui ne veulent pas la meme sortie :
    /// ressortir sur la liste des poses apres avoir ajoute un calque ferait perdre le
    /// reglage en cours, et obligerait a redescendre pour y revenir. Un booleen
    /// statique plutot qu'un etat passe en parametre : CustomMenuState est construit
    /// par le jeu, on ne lui transmet rien.
    /// </summary>
    private const float FirstRowY = 52f;
    private const float RowStep = 15f;
    private const float RowX = 30f;
    private static readonly Vector2 ThumbPosition = new Vector2(250f, 100f);

    private ForgeDesign design;
    private ForgeSlot slot;
    private UIForgeCellPreview thumb;

    private readonly List<MenuItem> rows = new List<MenuItem>();

    /// <summary>Planche ouverte, ou null tant qu'on choisit parmi les planches.</summary>
    private ForgeSource opened;

    public UIForgeFramePicker(MainMenu main) : base(main)
    {
    }

    /// <summary>
    /// L'ecran d'ou l'on vient, donc celui ou toute sortie ramene. On y entre depuis
    /// les poses comme depuis les calques : c'est la pile qui tranche, et non un
    /// drapeau que les deux appelants devaient penser a poser.
    /// </summary>
    private MainMenu.MenuState Home;

    public override void Create()
    {
      design = UIForgeList.Editing;
      slot = ForgeSlots.Get(UIForgeFrames.EditingSlot);

      Home = MenuNav.Arrive(Main, ModRegisters.MenuState<UIForgeFrames>());

      if (design == null || slot == null)
      {
        MenuNav.Switch(Main, Home);
        return;
      }

      ScreenTitles.Apply(Main, ModRegisters.MenuState<UIForgeFramePicker>());

      // Le retour quitte l'ecran, quel que soit le niveau ou l'on se trouve, et c'est
      // MainMenu qui s'en charge comme partout ailleurs.
      //
      // Il a d'abord servi a remonter des cases vers les planches, ce qui paraissait
      // naturel - deux listes, deux crans - et ne l'est pas : on est venu ici depuis
      // la liste des poses, c'est la qu'on veut retourner. Le changement de planche
      // n'est pas un niveau au-dessus, c'est une action de cet ecran, et il a
      // maintenant sa ligne.
      Main.BackState = Home;

      Main.TweenBGCameraToY(2);

      Main.Add(new UIPickerHeader(new Vector2(160f, 34f), design.Name, slot.Label));

      // Ne s'affiche qu'au niveau des planches, et seulement s'il y en a d'ecartees.
      Main.Add(new UIForgeHiddenNote(new Vector2(160f, 46f), () => opened == null));

      thumb = new UIForgeCellPreview(ThumbPosition);
      Main.Add(thumb);

      opened = null;
      rows.Clear();

      // La planche deja employee pour cet emplacement, ou celle de la source, sont
      // les deux endroits ou l'on a le plus de chances de trouver : on ouvre
      // directement la premiere qui existe plutot que de faire redescendre la liste.
      ForgePick current = design.PickOf(slot.Key);
      ForgeSource start = ForgeBank.Find(current?.Source ?? design.Source);

      if (start != null)
      {
        BuildCells(start);
      }
      else
      {
        BuildSheets();
      }
    }

    public override void Destroy()
    {
      thumb?.Release();
      thumb = null;
      rows.Clear();
    }

    // ------------------------------------------------------------------

    private void Clear()
    {
      foreach (MenuItem row in rows)
      {
        Main.Remove(row);
      }

      rows.Clear();
      thumb?.Release();
    }

    private void BuildSheets()
    {
      Clear();
      opened = null;

      // Toutes les planches mesurables, quelle que soit leur case : une pose se
      // recadre calque par calque. Ne restent ecartees que celles dont l'index ne se
      // lit pas ou ne declare aucune image, qui n'ont rien a proposer.
      List<ForgeSource> sources = ForgeBank.PickableSources();

      if (sources.Count == 0)
      {
        Main.Add(new UIForgeBankHint(new Vector2(160f, 120f)));
        Main.MaxUICameraY = 0f;
        Main.ToStartSelected = null;
        return;
      }

      var built = new List<UIMenuRow>();

      for (int i = 0; i < sources.Count; i++)
      {
        ForgeSource source = sources[i];

        UIMenuRow row = MakeRow(i, UIForgeEdit.Shorten(source.Name));

        // La taille de case n'est dite que lorsqu'elle sort de l'ordinaire. Une
        // planche qui n'est pas au format habituel demandera un recadrage, et le
        // savoir AVANT d'y prendre trente poses evite de les reprendre ensuite.
        row.RightText = () => Sized(source);

        row.OnConfirmed = () => BuildCells(source);
        row.OnSelected = () => thumb?.Release();
        built.Add(row);
      }

      Publish(built, 0);
    }

    private void BuildCells(ForgeSource source)
    {
      Clear();
      opened = source;

      List<ForgeCell> cells = ForgeBank.CellsOf(source);

      if (cells.Count == 0)
      {
        Main.Add(new UIForgeEmptySheet(new Vector2(160f, 120f), source.Name));
        Main.MaxUICameraY = 0f;
        Main.ToStartSelected = null;
        return;
      }

      var built = new List<UIMenuRow>();

      // Le curseur s'ouvre sur l'image deja choisie pour cette pose, ou en tete.
      //
      // Il visait auparavant la case canonique de la table Broforce, disparue avec le
      // pre-remplissage : hors de cette mise en page elle designait une case au hasard.
      // Retomber sur ce qu'on a soi-meme pose est vrai pour toute planche, et c'est
      // ce qu'on cherche en rouvrant une pose - la retrouver dans cinq cents lignes
      // autrement demanderait de se souvenir de ses coordonnees.
      int select = 0;

      // Une sortie qui ne detruit rien, en tete de liste.
      //
      // Valider une case REMPLACE la pose : c'est ce qu'on veut quand on en choisit
      // une seule, et c'est exactement ce qu'on ne veut plus des qu'on en a empile
      // deux. Sans cette ligne, terminer un empilement demandait de deviner qu'il
      // faut sortir par le bouton retour - et de ne surtout pas valider.
      //
      // Il n'y a rien a confirmer : chaque calque est deja enregistre en arrivant.
      // Cette ligne ne valide pas, elle raccompagne.
      UIMenuRow doneRow = MakeRow(built.Count, "<< DONE");
      doneRow.RightText = Stacked;
      doneRow.OnConfirmed = () => MenuNav.Switch(Main, Home);

      // Les autres lignes montrent la case qu'on survole ; celle-ci montre la pose
      // telle qu'elle est devenue. C'est le seul endroit de l'ecran ou l'empilement
      // se verifie : les calques posent des bras qu'aucune case prise isolement ne
      // montre, et remonter d'un ecran pour les voir couterait le geste qu'on est
      // justement en train d'enchainer.
      doneRow.OnSelected = () => thumb?.ShowPose(design, slot.Key);
      built.Add(doneRow);

      // Changer de planche est une ligne et non le bouton retour.
      //
      // L'ecran s'ouvre directement dans la planche la plus probable, ce qui fait
      // gagner un cran neuf fois sur dix ; il faut donc bien un moyen d'en sortir pour
      // aller voir ailleurs. Mais ce moyen ne peut pas etre le retour, qui doit
      // ramener d'ou l'on vient - la liste des poses - et non vers un ecran qu'on n'a
      // jamais traverse.
      // Libelle court : la ligne porte aussi le nom de la planche courante, et
      // "CHANGE SHEET" plus un nom de seize caracteres se chevauchaient.
      UIMenuRow switchRow = MakeRow(built.Count, "<< SHEET");
      switchRow.RightText = () => UIForgeEdit.Shorten(source.Name);
      switchRow.OnConfirmed = BuildSheets;
      switchRow.OnSelected = () => thumb?.Release();
      built.Add(switchRow);

      for (int i = 0; i < cells.Count; i++)
      {
        ForgeCell cell = cells[i];

        UIMenuRow row = MakeRow(built.Count, cell.ToString().ToUpperInvariant());

        if (Rank(source, cell) == 0)
        {
          select = built.Count;
        }

        // Le rang de l'image dans l'empilement : ce qu'on veut savoir en parcourant la
        // liste, c'est ce qu'on a deja pose et dans quel ordre. Sans ce repere, empiler
        // ne se voit nulle part et parait sans effet.
        ForgeSource captured = source;
        row.RightText = () => Mark(captured, cell);

        row.OnConfirmed = () => Choose(source, cell);
        row.OnAlt = () => Stack(source, cell);

        // Sans AltGuide, OnSelect efface le guide des boutons : l'action existe mais
        // rien ne dit qu'elle existe, ni sur quelle touche.
        row.AltGuide = "ADD LAYER";

        row.OnSelected = () => thumb?.Show(source, cell, design);
        built.Add(row);
      }

      Publish(built, select);
    }

    private UIMenuRow MakeRow(int index, string label)
    {
      float y = FirstRowY + index * RowStep;
      var from = new Vector2(index % 2 == 0 ? -260f : 580f, y);

      return new UIMenuRow(new Vector2(RowX, y), from, label) { ContentWidth = 150f };
    }

    private void Publish(List<UIMenuRow> built, int select)
    {
      for (int i = 0; i < built.Count; i++)
      {
        if (i > 0)
        {
          built[i].UpItem = built[i - 1];
        }

        if (i + 1 < built.Count)
        {
          built[i].DownItem = built[i + 1];
        }
      }

      Main.Add(built);
      rows.AddRange(built);

      float lastY = FirstRowY + (built.Count - 1) * RowStep;
      Main.MaxUICameraY = Math.Max(0f, lastY - 180f);

      UIMenuRow toSelect = built[Math.Clamp(select, 0, built.Count - 1)];
      Main.ToStartSelected = toSelect;

      // La liste est reconstruite en place, hors transition : c'est a nous de rendre
      // le focus, MainMenu ne le fera pas une seconde fois.
      if (!Main.Transitioning)
      {
        toSelect.Selected = true;

        // Et c'est aussi a nous d'annoncer le bouton Alt. UIMenuRow le fait depuis
        // OnSelect, mais en lisant sa propriete MainMenu - qui n'est renseignee qu'a
        // l'entree dans la scene, donc pas encore : la ligne vient d'etre creee. Sans
        // ce rappel, le guide gardait le "ADD LAYER" des cases alors qu'on choisit une
        // planche, ou Alt ne fait rien.
        if (toSelect.AltGuide != null)
        {
          Main.ButtonGuideC.SetDetails(MenuButtonGuide.ButtonModes.Alt, toSelect.AltGuide);
        }
        else
        {
          Main.ButtonGuideC.Clear();
        }
      }
    }

    /// <summary>
    /// Valider REMPLACE la pose ; Alt AJOUTE l'image par-dessus les precedentes.
    ///
    /// Les deux gestes sur le meme ecran plutot que deux ecrans : on vient de
    /// parcourir trente mille images pour trouver celle-la, obliger a y revenir par
    /// une autre porte pour en empiler une seconde serait absurde.
    /// </summary>
    private void Choose(ForgeSource source, ForgeCell cell)
    {
      design.Set(slot.Key, ForgePick.Of(source.Name, cell));
      design.Source = source.Name;
      ForgeStorage.Save();
      MenuNav.Switch(Main, Home);
    }

    private void Stack(ForgeSource source, ForgeCell cell)
    {
      design.AddLayer(slot.Key, ForgePick.Of(source.Name, cell));
      design.Source = source.Name;
      ForgeStorage.Save();

      // On reste sur place : empiler des bras, une arme et un chapeau se fait
      // d'affilee, et repartir de la liste des poses a chaque calque couterait trois
      // pressions par image.
      Sounds.ui_move2.Play(160f, 1f);
    }

    /// <summary>Le compte d'images d'une planche.</summary>
    private static string Sized(ForgeSource source)
    {
      return source.FrameCount.ToString();
    }

    /// <summary>Ce que porte la ligne de sortie : l'etat de la pose.</summary>
    private string Stacked()
    {
      int count = design.LayersOf(slot.Key).Count;

      if (count == 0)
      {
        return "EMPTY";
      }

      return count == 1 ? "1 FRAME" : count + " IMAGES";
    }

    /// <summary>Ce que porte une case a droite : son rang dans l'empilement, ou rien.</summary>
    private string Mark(ForgeSource source, ForgeCell cell)
    {
      int rank = Rank(source, cell);

      if (rank < 0)
      {
        return "";
      }

      return design.LayersOf(slot.Key).Count == 1 ? "PICKED" : "LAYER " + (rank + 1);
    }

    /// <summary>
    /// Rang de cette case dans l'empilement de la pose, ou -1 si elle n'y est pas.
    ///
    /// Sert au libelle et au placement du curseur a l'ouverture, qui doivent designer
    /// la meme image : les separer laisserait le curseur deriver du marquage a la
    /// premiere retouche de l'un des deux.
    /// </summary>
    private int Rank(ForgeSource source, ForgeCell cell)
    {
      List<ForgePick> stack = design.LayersOf(slot.Key);

      for (int i = 0; i < stack.Count; i++)
      {
        if (stack[i] != null
            && stack[i].Cell.Equals(cell)
            && string.Equals(stack[i].Source, source.Name, StringComparison.OrdinalIgnoreCase))
        {
          return i;
        }
      }

      return -1;
    }
  }

  /// <summary>
  /// Apercu d'une case du vivier, avec la fenetre de decoupe posee dessus.
  ///
  /// Distinct de UIForgeCellThumb, qui lit dans le dessin : celui-ci montre une case
  /// qu'on envisage et qui n'est pas encore choisie.
  /// </summary>
  public class UIForgeCellPreview : UIForgePanel
  {
    private Texture2D texture;
    private int width;
    private int height;
    private int windowX;
    private int windowY;
    /// <summary>
    /// Derive et non memorise : le facteur peut changer a tout moment (gachette
    /// gauche), et un zoom fige a l'affichage n'aurait bouge qu'au changement
    /// d'ecran.
    /// </summary>
    private float zoom => ZoomFor(width, height);

    public UIForgeCellPreview(Vector2 position) : base(position)
    {
    }

    /// <summary>
    /// Une case du vivier, avec la fenetre posee ou elle tomberait.
    ///
    /// Le decalage de la planche est pris en compte, celui du calque non : ce dernier
    /// n'existe pas encore, la case n'etant pas choisie. La fenetre est donc montree
    /// la ou elle tomberait si l'on validait maintenant.
    /// </summary>
    public void Show(ForgeSource source, ForgeCell cell, ForgeDesign design)
    {
      Release();

      // Meme lecture que la composition : l'apercu doit montrer ce qui sera
      // reellement retenu, mise a l'echelle comprise.
      Color[] pixels = ForgeBank.ReadCellFitted(source, cell, out var size);

      if (pixels == null)
      {
        return;
      }

      ForgeNudge sheet = design.NudgeOf(source.Name);

      Adopt(pixels, size.X, size.Y,
          design.WindowX - sheet.X, design.WindowY - sheet.Y);
    }

    /// <summary>La pose complete, calques fusionnes et decales, telle qu'elle est enregistree.</summary>
    public void ShowPose(ForgeDesign design, string slotKey)
    {
      Release();

      ForgePose pose = ForgeCompose.Pose(design, slotKey);

      if (pose == null)
      {
        return;
      }

      Adopt(pose.Pixels, pose.Width, pose.Height, pose.WindowX, pose.WindowY);
    }

    private void Adopt(Color[] pixels, int width, int height, int windowX, int windowY)
    {
      try
      {
        texture = new Texture2D(Engine.Instance.GraphicsDevice, width, height);
        texture.SetData(pixels);
        this.width = width;
        this.height = height;
        this.windowX = windowX;
        this.windowY = windowY;
      }
      catch (Exception e)
      {
        Log.Error($"[Forge] apercu de case impossible : {e.Message}");
      }
    }

    public void Release()
    {
      // Parcourir une planche de cinq cents cases sans liberer en laisserait cinq
      // cents sur la carte graphique.
      try { texture?.Dispose(); } catch { }
      texture = null;
    }

    public override void Render()
    {
      base.Render();

      float boxWidth = (width > 0 ? width : ForgeSlots.SourceCell) * zoom;
      float boxHeight = (height > 0 ? height : ForgeSlots.SourceCell) * zoom;

      var corner = new Vector2(Position.X - boxWidth * 0.5f, Position.Y - boxHeight * 0.5f);
      Draw.Rect(corner.X, corner.Y, boxWidth, boxHeight, Color.Black * 0.55f);
      Draw.HollowRect(new Rectangle((int)corner.X, (int)corner.Y, (int)boxWidth, (int)boxHeight), Color.Gray);

      if (texture == null || texture.IsDisposed)
      {
        return;
      }

      Draw.SpriteBatch.Draw(texture, corner, null, Color.White, 0f, Vector2.Zero,
          zoom, SpriteEffects.None, 0f);

      DrawVanillaFrame(corner, windowX + ForgeSlots.AnchorX, windowY + ForgeSlots.AnchorY, zoom);
    }

  }

  /// <summary>
  /// Ce qui s'affiche quand le vivier ne contient aucune planche exploitable.
  ///
  /// Une liste vide sans un mot laisse croire a une panne du mod, alors qu'il manque
  /// seulement des images. Le message dit donc l'endroit exact ou les deposer : c'est
  /// la seule chose qui debloque, et la chercher dans la documentation quand on est
  /// deja dans l'ecran serait une perte.
  /// </summary>
  public class UIForgeBankHint : UIForgePanel
  {
    public UIForgeBankHint(Vector2 position) : base(position)
    {
    }

    public override void Render()
    {
      base.Render();

      Draw.OutlineTextCentered(TFGame.Font, "NO SHEET IN THE BANK",
          Position, Color.Gray, Color.Black);

      Draw.OutlineTextCentered(TFGame.Font, "DROP SLICED FRAMES IN",
          Position + new Vector2(0f, 14f), Color.Gray, Color.Black);

      // Le chemin est relu a chaque image : un fichier sprites.path peut le deplacer,
      // et afficher l'ancien enverrait deposer les images la ou rien ne les lira.
      Draw.OutlineTextCentered(TFGame.Font, MenuText.Safe(ForgeBank.Root ?? ""),
          Position + new Vector2(0f, 26f), Color.Gray, Color.Black);
    }

  }

  /// <summary>Ce qui s'affiche quand une planche n'a aucune case a proposer.</summary>
  public class UIForgeEmptySheet : UIForgePanel
  {
    private readonly string sheet;

    public UIForgeEmptySheet(Vector2 position, string sheet) : base(position)
    {
      this.sheet = sheet;
    }

    public override void Render()
    {
      base.Render();

      Draw.OutlineTextCentered(TFGame.Font, MenuText.Safe(UIForgeEdit.Shorten(sheet)),
          Position, Color.Gray, Color.Black);
      Draw.OutlineTextCentered(TFGame.Font, "NO FRAME",
          Position + new Vector2(0f, 12f), Color.Gray, Color.Black);
    }

  }

  /// <summary>
  /// Combien de planches du vivier ne sont pas proposees.
  ///
  /// Le filtre ne doit pas etre muet : une planche deposee dans le vivier et absente
  /// de la liste ferait chercher une faute de nom ou un decoupage rate. Il ne reste
  /// que deux raisons de l'ecarter - un index.json qu'on ne sait pas lire, ou qui ne
  /// declare aucune image - et cette ligne le dit avant qu'on cherche.
  /// </summary>
  public class UIForgeHiddenNote : UIForgePanel
  {
    private readonly Func<bool> visible;

    public UIForgeHiddenNote(Vector2 position, Func<bool> visible) : base(position)
    {
      this.visible = visible;
    }

    public override void Render()
    {
      base.Render();

      if (!visible())
      {
        return;
      }

      int hidden = ForgeBank.UnusableCount();
      if (hidden == 0)
      {
        return;
      }

      string line = hidden == 1
          ? "1 SHEET HIDDEN - UNREADABLE OR EMPTY INDEX"
          : hidden + " SHEETS HIDDEN - UNREADABLE OR EMPTY INDEX";

      Draw.OutlineTextCentered(TFGame.Font, MenuText.Safe(line), Position,
          Color.Gray, Color.Black);
    }

  }
}
