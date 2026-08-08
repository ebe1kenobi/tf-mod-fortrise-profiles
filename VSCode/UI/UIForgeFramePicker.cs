using System;
using System.Collections.Generic;
using FortRise;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using TowerFall;

namespace TFModFortRiseProfiles
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
    private const float FirstRowY = 52f;
    private const float RowStep = 15f;
    private const float RowX = 30f;
    private static readonly Vector2 ThumbPosition = new Vector2(250f, 100f);

    private ForgeDesign design;
    private ForgeSlot slot;
    private UIForgeCellPreview thumb;
    private UIForgeBackWatcher watcher;

    private readonly List<MenuItem> rows = new List<MenuItem>();

    /// <summary>Planche ouverte, ou null tant qu'on choisit parmi les planches.</summary>
    private ForgeSource opened;

    public UIForgeFramePicker(MainMenu main) : base(main)
    {
    }

    public override void Create()
    {
      MainMenu.MenuState framesState = ModRegisters.MenuState<UIForgeFrames>();

      design = UIForgeList.Editing;
      slot = ForgeSlots.Get(UIForgeFrames.EditingSlot);

      if (design == null || slot == null)
      {
        Main.State = framesState;
        return;
      }

      ScreenTitles.Apply(Main, ModRegisters.MenuState<UIForgeFramePicker>());
      Main.BackState = framesState;
      Main.TweenBGCameraToY(2);

      Main.Add(new UIPickerHeader(new Vector2(160f, 34f), design.Name, slot.Label));

      // Ne s'affiche qu'au niveau des planches, et seulement s'il y en a d'ecartees.
      Main.Add(new UIForgeHiddenNote(new Vector2(160f, 46f), () => opened == null));

      thumb = new UIForgeCellPreview(ThumbPosition);
      Main.Add(thumb);

      // CustomMenuState n'a pas d'Update : c'est cet objet qui ecoute le bouton
      // retour pour remonter d'un niveau. Voir UIForgeBackWatcher.
      watcher = new UIForgeBackWatcher(() => opened != null, BuildSheets);
      Main.Add(watcher);

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
      watcher = null;
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

      // Le bouton retour quitte l'ecran quand on est au premier niveau ; il remonte
      // d'un cran quand on est dans une planche. C'est Update qui s'en charge, il
      // faut donc rendre ici la sortie a MainMenu.
      Main.BackState = ModRegisters.MenuState<UIForgeFrames>();

      // Seulement les planches dont la forge sait tirer une pose. Une case de 64 ou
      // de 128 rendrait un coin de la creature au lieu de la creature, sans rien
      // signaler : mieux vaut ne pas la proposer que livrer une image fausse.
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
        row.RightText = () => source.FrameCount + "";
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

      // Tant qu'une planche est ouverte, le retour est intercepte par Update.
      // MainMenu.BackState est neutralise pour qu'il ne quitte pas aussi l'ecran.
      Main.BackState = ModRegisters.MenuState<UIForgeFramePicker>();

      List<ForgeCell> cells = ForgeBank.CellsOf(source);

      if (cells.Count == 0)
      {
        Main.Add(new UIForgeEmptySheet(new Vector2(160f, 120f), source.Name));
        Main.MaxUICameraY = 0f;
        Main.ToStartSelected = null;
        return;
      }

      var built = new List<UIMenuRow>();
      ForgeCell? canonical = ForgeLayout.Of(slot.Key);
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
      UIMenuRow doneRow = MakeRow(0, "<< TERMINER");
      doneRow.RightText = Stacked;
      doneRow.OnConfirmed = () => Main.State = ModRegisters.MenuState<UIForgeFrames>();

      // Les autres lignes montrent la case qu'on survole ; celle-ci montre la pose
      // telle qu'elle est devenue. C'est le seul endroit de l'ecran ou l'empilement
      // se verifie : les calques posent des bras qu'aucune case prise isolement ne
      // montre, et remonter d'un ecran pour les voir couterait le geste qu'on est
      // justement en train d'enchainer.
      doneRow.OnSelected = () => thumb?.ShowPose(design, slot.Key);
      built.Add(doneRow);

      for (int i = 0; i < cells.Count; i++)
      {
        ForgeCell cell = cells[i];

        UIMenuRow row = MakeRow(built.Count, cell.ToString().ToUpperInvariant());

        // La case canonique de cet emplacement est signalee : sur une planche de
        // personnage c'est presque toujours la bonne, et la retrouver dans cinq cents
        // lignes autrement demanderait de connaitre la table par coeur.
        bool suggested = canonical != null
                         && canonical.Value.Col == cell.Col
                         && canonical.Value.Row == cell.Row;

        if (suggested)
        {
          select = built.Count;
        }

        // Le rang de l'image dans l'empilement prime sur "SUGGESTED" : ce qu'on veut
        // savoir en parcourant la liste, c'est ce qu'on a deja pose et dans quel
        // ordre. Sans ce repere, empiler ne se voit nulle part et parait sans effet.
        ForgeSource captured = source;
        row.RightText = () => Mark(captured, cell, suggested);

        row.OnConfirmed = () => Choose(source, cell);
        row.OnAlt = () => Stack(source, cell);

        // Sans AltGuide, OnSelect efface le guide des boutons : l'action existe mais
        // rien ne dit qu'elle existe, ni sur quelle touche.
        row.AltGuide = "ADD LAYER";

        row.OnSelected = () => thumb?.Show(source, cell, design.WindowX, design.WindowY);
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

      MenuItem toSelect = built[Math.Clamp(select, 0, built.Count - 1)];
      Main.ToStartSelected = toSelect;

      // La liste est reconstruite en place, hors transition : c'est a nous de rendre
      // le focus, MainMenu ne le fera pas une seconde fois.
      if (!Main.Transitioning)
      {
        toSelect.Selected = true;
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
      ForgeStorage.Save();
      Main.State = ModRegisters.MenuState<UIForgeFrames>();
    }

    private void Stack(ForgeSource source, ForgeCell cell)
    {
      design.AddLayer(slot.Key, ForgePick.Of(source.Name, cell));
      ForgeStorage.Save();

      // On reste sur place : empiler des bras, une arme et un chapeau se fait
      // d'affilee, et repartir de la liste des poses a chaque calque couterait trois
      // pressions par image.
      Sounds.ui_move2.Play(160f, 1f);
    }

    /// <summary>Ce que porte la ligne de sortie : l'etat de la pose.</summary>
    private string Stacked()
    {
      int count = design.LayersOf(slot.Key).Count;

      if (count == 0)
      {
        return "VIDE";
      }

      return count == 1 ? "1 IMAGE" : count + " IMAGES";
    }

    /// <summary>
    /// Ce que porte une case a droite : son rang dans l'empilement, ou le fait
    /// qu'elle soit la case attendue pour cette pose.
    /// </summary>
    private string Mark(ForgeSource source, ForgeCell cell, bool suggested)
    {
      List<ForgePick> stack = design.LayersOf(slot.Key);

      for (int i = 0; i < stack.Count; i++)
      {
        if (stack[i] != null
            && stack[i].Col == cell.Col
            && stack[i].Row == cell.Row
            && string.Equals(stack[i].Source, source.Name, StringComparison.OrdinalIgnoreCase))
        {
          return stack.Count == 1 ? "CHOISIE" : "CALQUE " + (i + 1);
        }
      }

      return suggested ? "SUGGESTED" : "";
    }
  }

  /// <summary>
  /// Ecoute le bouton retour pour le compte d'un ecran a deux niveaux.
  ///
  /// CustomMenuState n'expose ni Update ni Render : un etat de menu ne peut pas
  /// lire l'entree lui-meme. Tout ce qui doit reagir a une touche passe donc par un
  /// objet pose dans le menu, et c'est le role de celui-ci.
  ///
  /// MainMenu ne quitte l'ecran sur le bouton retour que si BackState differe de
  /// State. L'ecran met donc les deux a la meme valeur tant qu'une planche est
  /// ouverte, ce qui neutralise la sortie et laisse ce guetteur remonter d'un cran.
  /// Les deux ne peuvent pas se declencher ensemble.
  /// </summary>
  public class UIForgeBackWatcher : UIForgePanel
  {
    private readonly Func<bool> active;
    private readonly Action onBack;

    public UIForgeBackWatcher(Func<bool> active, Action onBack) : base(Vector2.Zero)
    {
      this.active = active;
      this.onBack = onBack;
    }

    public override void Update()
    {
      base.Update();

      // CanAct tombe pendant une modale ou un clavier virtuel : sans ce test, fermer
      // le clavier fermerait aussi la planche ouverte derriere lui.
      if (MainMenu == null || !MainMenu.CanAct || MainMenu.Transitioning)
      {
        return;
      }

      if (active() && MenuInput.Back)
      {
        Sounds.ui_clickBack.Play(160f, 1f);
        onBack();
      }
    }

    public override void Render()
    {
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
    private const int Scale = 4;

    private Texture2D texture;
    private int cellWidth;
    private int cellHeight;
    private int windowX;
    private int windowY;

    public UIForgeCellPreview(Vector2 position) : base(position)
    {
    }

    public void Show(ForgeSource source, ForgeCell cell, int windowX, int windowY)
    {
      Release();

      Color[] pixels = ForgeBank.ReadCell(source, cell);

      if (pixels == null)
      {
        return;
      }

      Adopt(pixels, source.CellWidth, source.CellHeight, windowX, windowY);
    }

    /// <summary>La pose complete, calques fusionnes, telle qu'elle est enregistree.</summary>
    public void ShowPose(ForgeDesign design, string slotKey)
    {
      Release();

      Color[] merged = ForgeCompose.Pose(design, slotKey, out int width, out int height, out _);

      if (merged == null)
      {
        return;
      }

      Adopt(merged, width, height, design.WindowX, design.WindowY);
    }

    private void Adopt(Color[] pixels, int width, int height, int windowX, int windowY)
    {
      try
      {
        texture = new Texture2D(Engine.Instance.GraphicsDevice, width, height);
        texture.SetData(pixels);
        cellWidth = width;
        cellHeight = height;
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

      float boxWidth = cellWidth > 0 ? cellWidth * Scale : ForgeSlots.SourceCell * Scale;
      float boxHeight = cellHeight > 0 ? cellHeight * Scale : ForgeSlots.SourceCell * Scale;

      var corner = new Vector2(Position.X - boxWidth * 0.5f, Position.Y - boxHeight * 0.5f);
      Draw.Rect(corner.X, corner.Y, boxWidth, boxHeight, Color.Black * 0.55f);
      Draw.HollowRect(new Rectangle((int)corner.X, (int)corner.Y, (int)boxWidth, (int)boxHeight), Color.Gray);

      if (texture == null || texture.IsDisposed)
      {
        return;
      }

      Draw.SpriteBatch.Draw(texture, corner, null, Color.White, 0f, Vector2.Zero,
          Scale, SpriteEffects.None, 0f);

      Draw.HollowRect(new Rectangle(
          (int)(corner.X + windowX * Scale),
          (int)(corner.Y + windowY * Scale),
          ForgeSlots.Frame * Scale,
          ForgeSlots.Frame * Scale), Color.Orange * 0.8f);
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
      Draw.OutlineTextCentered(TFGame.Font, "AUCUNE IMAGE",
          Position + new Vector2(0f, 12f), Color.Gray, Color.Black);
    }

  }

  /// <summary>
  /// Combien de planches du vivier ne sont pas proposees.
  ///
  /// Le filtre est necessaire, mais il ne doit pas etre muet : une planche deposee
  /// dans le vivier et absente de la liste ferait chercher une faute de nom ou un
  /// decoupage rate, alors qu'elle est seulement au mauvais format. Cette ligne
  /// repond a la question avant qu'on la pose.
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
          ? "1 PLANCHE MASQUEE - CASE AUTRE QUE " + ForgeSlots.SourceCell
          : hidden + " PLANCHES MASQUEES - CASE AUTRE QUE " + ForgeSlots.SourceCell;

      Draw.OutlineTextCentered(TFGame.Font, MenuText.Safe(line), Position,
          Color.Gray, Color.Black);
    }

  }
}
