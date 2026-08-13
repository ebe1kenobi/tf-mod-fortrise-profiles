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
  /// Les calques d'une pose : leur ordre, leur cadrage, et ce qu'on en retire.
  ///
  /// Empiler etait possible depuis l'ecran de choix, mais rien ne permettait ensuite
  /// de reprendre l'empilement : ni retirer le calque du milieu, ni changer l'ordre,
  /// ni corriger un bras pose trois pixels trop haut. Cet ecran est cette reprise.
  ///
  /// Tout s'y regle en gauche/droite, sur des lignes ordinaires. Un mode ou les
  /// fleches deplaceraient directement l'image serait plus direct d'un geste, mais
  /// les fleches servent deja a naviguer entre les lignes : il faudrait une bascule,
  /// donc un etat de plus a retenir, pour economiser un aller-retour.
  /// </summary>
  public class UIForgeLayers : CustomMenuState
  {
    /// <summary>Emplacement dont on regle les calques.</summary>
    internal static string EditingSlot;

    private const float FirstRowY = 60f;
    private const float RowStep = 15f;
    private const float RowX = 30f;
    private static readonly Vector2 PreviewPosition = new Vector2(250f, 104f);

    private ForgeDesign design;
    private ForgeSlot slot;
    private UIForgeLayerPreview preview;

    /// <summary>
    /// Rang du calque en cours de reglage.
    ///
    /// Statique : on revient sur cet ecran apres etre passe par le selecteur, et
    /// repartir du premier calque a chaque retour obligerait a recompter. Borne a
    /// l'ouverture, l'empilement ayant pu changer entre-temps.
    /// </summary>
    private static int current;

    /// <summary>Emplacement de la derniere ouverture, pour repartir de zero si l'on change.</summary>
    private static string lastSlot;

    /// <summary>Taille de l'empilement avant un aller au selecteur, ou -1.</summary>
    private static int countBeforePicker = -1;

    public UIForgeLayers(MainMenu main) : base(main)
    {
    }

    public override void Create()
    {
      MainMenu.MenuState framesState = ModRegisters.MenuState<UIForgeFrames>();

      design = UIForgeList.Editing;
      slot = ForgeSlots.Get(EditingSlot);

      if (design == null || slot == null)
      {
        MenuNav.Switch(Main, framesState);
        return;
      }

      ScreenTitles.Apply(Main, ModRegisters.MenuState<UIForgeLayers>());
      Main.BackState = MenuNav.Arrive(Main, framesState);
      Main.TweenBGCameraToY(2);

      Bound();

      Main.Add(new UIPickerHeader(new Vector2(160f, 34f), design.Name, slot.Label));

      preview = new UIForgeLayerPreview(PreviewPosition, Layer, SheetNote);
      preview.Show(design, slot.Key);
      Main.Add(preview);

      var rows = new List<UIMenuRow>();

      UIMenuRow layerRow = MakeRow(rows.Count, "LAYER");
      layerRow.RightText = LayerLabel;
      layerRow.OnLeft = () => Cycle(-1);
      layerRow.OnRight = () => Cycle(1);
      rows.Add(layerRow);

      // Deux niveaux qui font le meme geste et ne portent pas la meme distance : sur
      // la pose qu'on regarde, avec un seul calque, ils sont indiscernables. C'est
      // pourquoi ils se nomment d'apres CE QU'ILS DEPLACENT et non d'apres l'effet -
      // "DECALAGE X" deux fois ne disait rien - et pourquoi les lignes de planche
      // annoncent le nombre d'images qu'elles emmenent avec elles.
      UIMenuRow offsetXRow = MakeRow(rows.Count, "LAYER X");
      offsetXRow.RightText = () => Signed(Layer()?.OffsetX ?? 0);
      offsetXRow.OnLeft = () => MoveLayer(-1, 0);
      offsetXRow.OnRight = () => MoveLayer(1, 0);
      rows.Add(offsetXRow);

      UIMenuRow offsetYRow = MakeRow(rows.Count, "LAYER Y");
      offsetYRow.RightText = () => Signed(Layer()?.OffsetY ?? 0);
      offsetYRow.OnLeft = () => MoveLayer(0, -1);
      offsetYRow.OnRight = () => MoveLayer(0, 1);
      rows.Add(offsetYRow);

      // Deplace toutes les images qui viennent de cette planche, y compris celles des
      // autres poses de l'archer. C'est le reglage qui evite de reprendre dix-neuf
      // emplacements un par un quand une planche entiere est calee ailleurs.
      UIMenuRow sheetXRow = MakeRow(rows.Count, "SHEET X");
      sheetXRow.RightText = () => Reach(Sheet().X);
      sheetXRow.OnLeft = () => MoveSheet(-1, 0);
      sheetXRow.OnRight = () => MoveSheet(1, 0);
      rows.Add(sheetXRow);

      UIMenuRow sheetYRow = MakeRow(rows.Count, "SHEET Y");
      sheetYRow.RightText = () => Reach(Sheet().Y);
      sheetYRow.OnLeft = () => MoveSheet(0, -1);
      sheetYRow.OnRight = () => MoveSheet(0, 1);
      rows.Add(sheetYRow);

      // La taille et le rognage de CETTE image. Le meme ecran que celui de la fiche,
      // qui les pose sur toutes a la fois : une planche se regle d'abord en bloc, et
      // l'on ne descend ici que pour l'image qui ne suit pas.
      UIMenuRow adjustRow = MakeRow(rows.Count, "SIZE / CROP");
      adjustRow.RightText = AdjustLabel;
      adjustRow.OnConfirmed = OpenAdjust;
      rows.Add(adjustRow);

      // La hauteur de la tete sur CETTE pose. Seulement sur les poses du corps : le
      // jeu accroche la tete a une hauteur par image du corps, et nulle part ailleurs.
      //
      // C'est ce qui donne une tete vivante - chez l'archer vert elle descend de
      // quatre pixels quand il s'accroupit et monte d'un en courant. L'import releve
      // ces valeurs sur l'archer repris ; cette ligne est le seul moyen de les donner
      // a un archer dessine ici.
      if (slot.Sheet == ForgeSheet.Body)
      {
        UIMenuRow headRow = MakeRow(rows.Count, "HEAD Y");
        headRow.RightText = () => Signed(design.HeadOffsetOf(slot.Key));
        headRow.OnLeft = () => MoveHead(-1);
        headRow.OnRight = () => MoveHead(1);
        rows.Add(headRow);
      }

      UIMenuRow orderRow = MakeRow(rows.Count, "ORDRE");
      orderRow.RightText = OrderLabel;
      orderRow.OnLeft = () => Reorder(-1);
      orderRow.OnRight = () => Reorder(1);
      rows.Add(orderRow);

      // "PICK" et non "ADD" : le selecteur fait les deux - valider remplace la
      // pose, Alt empile - et promettre l'ajout ferait de la validation une perte.
      UIMenuRow addRow = MakeRow(rows.Count, "PICK A FRAME");
      addRow.OnConfirmed = OpenPicker;
      rows.Add(addRow);

      UIMenuRow dropRow = MakeRow(rows.Count, "REMOVE THIS LAYER");
      dropRow.OnConfirmed = Drop;
      rows.Add(dropRow);

      UIMenuRow clearRow = MakeRow(rows.Count, "TOUT VIDER");
      clearRow.OnConfirmed = Clear;
      rows.Add(clearRow);

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

      preview?.Release();
      preview = null;
    }

    // ------------------------------------------------------------------

    private UIMenuRow MakeRow(int index, string label)
    {
      float y = FirstRowY + index * RowStep;
      var from = new Vector2(index % 2 == 0 ? -260f : 580f, y);

      return new UIMenuRow(new Vector2(RowX, y), from, label) { ContentWidth = 150f };
    }

    private List<ForgePick> Stack()
    {
      return design.LayersOf(slot.Key);
    }

    /// <summary>Le calque courant, ou null si la pose est vide.</summary>
    private ForgePick Layer()
    {
      List<ForgePick> stack = Stack();
      return current >= 0 && current < stack.Count ? stack[current] : null;
    }

    private ForgeNudge Sheet()
    {
      ForgePick pick = Layer();
      return pick == null ? new ForgeNudge() : design.NudgeOf(pick.Source);
    }

    /// <summary>
    /// Descend ou remonte la tete sur cette pose.
    ///
    /// Vers le BAS quand on va vers la droite, comme partout ailleurs dans la forge :
    /// c'est le personnage qu'on deplace, pas l'ancre. La valeur enregistree, elle,
    /// est bien un ecart d'ancre - le sens s'inverse a l'enregistrement, une fois,
    /// dans ForgeSlots.HeadYOrigins.
    /// </summary>
    private void MoveHead(int delta)
    {
      design.MoveHead(slot.Key, delta);
      preview?.Show(design, slot.Key);
    }

    private void OpenAdjust()
    {
      if (Layer() == null)
      {
        Sounds.ui_invalid.Play(160f, 1f);
        return;
      }

      UIForgeAdjust.EditingSlot = slot.Key;
      UIForgeAdjust.EditingLayer = current;
      MenuNav.Push(Main, ModRegisters.MenuState<UIForgeAdjust>());
    }

    /// <summary>
    /// L'etat des retouches de ce calque, en tres peu de place.
    ///
    /// La taille toujours, le reste par un signe : la ligne fait cent cinquante
    /// pixels et "ROGNE MIROIR 90 DEGRES" n'y tiendrait pas. Ce qu'on veut savoir
    /// d'ici, c'est qu'il y a quelque chose - le detail est a un bouton.
    /// </summary>
    private string AdjustLabel()
    {
      ForgePick pick = Layer();

      if (pick == null)
      {
        return "";
      }

      string marks = "";

      if (pick.CropLeft + pick.CropRight + pick.CropTop + pick.CropBottom > 0)
      {
        marks += " ROGNE";
      }

      if (pick.FlipX || pick.FlipY)
      {
        marks += pick.FlipX && pick.FlipY ? " HV" : pick.FlipX ? " H" : " V";
      }

      if (pick.Rotation != 0)
      {
        marks += " " + pick.Rotation + "'";
      }

      return pick.Scale + "%" + marks;
    }

    /// <summary>
    /// Choisit sur quel calque s'ouvrir.
    ///
    /// Trois cas, dans cet ordre : un autre emplacement repart du premier calque, un
    /// retour du selecteur ayant ajoute une image se pose sur elle, et tout le reste
    /// garde le rang precedent en le bornant - l'empilement a pu se vider entre-temps.
    /// </summary>
    private void Bound()
    {
      int count = Stack().Count;

      if (!string.Equals(lastSlot, slot.Key, StringComparison.Ordinal))
      {
        current = 0;
        lastSlot = slot.Key;
      }
      else if (countBeforePicker >= 0 && count > countBeforePicker)
      {
        current = count - 1;
      }

      countBeforePicker = -1;
      current = count == 0 ? 0 : Math.Clamp(current, 0, count - 1);
    }

    private void Refresh()
    {
      ForgeStorage.Save();
      preview?.Show(design, slot.Key);
    }

    // ------------------------------------------------------------------

    private string LayerLabel()
    {
      List<ForgePick> stack = Stack();

      if (stack.Count == 0)
      {
        return "EMPTY";
      }

      ForgePick pick = stack[current];

      // Le rang, puis d'ou l'image vient : sans le rang on ne sait pas ce que les
      // lignes suivantes vont deplacer, sans la source on ne sait pas laquelle.
      return (current + 1) + "/" + stack.Count + " " + UIForgeEdit.Shorten(pick.Source);
    }

    private string OrderLabel()
    {
      List<ForgePick> stack = Stack();

      if (stack.Count == 0)
      {
        return "";
      }

      // Le premier calque est le fond, le dernier est celui qu'on voit par-dessus
      // tout. Le dire en mots plutot qu'en numero : "1/3" ne dit pas lequel est
      // devant, et c'est la seule chose qu'on veut savoir en reordonnant.
      if (stack.Count == 1)
      {
        return "SEUL";
      }

      if (current == 0)
      {
        return "TO BACK";
      }

      return current == stack.Count - 1 ? "DEVANT" : "TO MIDDLE";
    }

    private static string Signed(int value)
    {
      return value > 0 ? "+" + value : value.ToString();
    }

    /// <summary>
    /// Un decalage de planche, suivi du nombre d'images qu'il emporte.
    ///
    /// Sans ce compte, rien ne distingue ce reglage de celui du calque : sur la pose
    /// qu'on regarde, les deux deplacent la meme image du meme nombre de pixels.
    /// "x14" dit que treize autres suivent ailleurs, ce qui est toute la difference
    /// et ne se voit d'aucune facon depuis cet ecran.
    /// </summary>
    private string Reach(int value)
    {
      ForgePick pick = Layer();

      if (pick == null)
      {
        return Signed(value);
      }

      return Signed(value) + " x" + Using(pick.Source);
    }

    /// <summary>
    /// De quelle planche vient le calque courant, sous l'apercu.
    ///
    /// Le "x14" des lignes PLANCHE ne dit pas de QUOI il parle. Nommer la planche a
    /// cote de l'image evite de remonter a la ligne CALQUE pour le verifier, et c'est
    /// ce nom qui dit si le reglage va toucher un morceau isole ou tout le corps.
    /// </summary>
    private string SheetNote()
    {
      ForgePick pick = Layer();

      return pick == null
          ? null
          : UIForgeEdit.Shorten(pick.Source) + " x" + Using(pick.Source);
    }

    /// <summary>Combien d'images de tout l'archer viennent de cette planche.</summary>
    private int Using(string source)
    {
      int count = 0;

      foreach (ForgeSlot other in ForgeSlots.All)
      {
        foreach (ForgePick pick in design.LayersOf(other.Key))
        {
          if (pick != null
              && string.Equals(pick.Source, source, StringComparison.OrdinalIgnoreCase))
          {
            count++;
          }
        }
      }

      return count;
    }

    // ------------------------------------------------------------------

    private void Cycle(int direction)
    {
      int count = Stack().Count;

      if (count == 0)
      {
        return;
      }

      // Cyclique : trois calques se parcourent plus vite en bouclant qu'en butant
      // sur les extremites.
      current = (current + direction + count) % count;
      preview?.Show(design, slot.Key);
    }

    private void MoveLayer(int dx, int dy)
    {
      ForgePick pick = Layer();

      if (pick == null)
      {
        return;
      }

      pick.OffsetX += dx;
      pick.OffsetY += dy;
      design.Touch();
      Refresh();
    }

    private void MoveSheet(int dx, int dy)
    {
      ForgePick pick = Layer();

      if (pick == null)
      {
        return;
      }

      design.Nudge(pick.Source, dx, dy);
      Refresh();
    }

    private void Reorder(int direction)
    {
      List<ForgePick> stack = Stack();
      int target = current + direction;

      if (stack.Count < 2 || target < 0 || target >= stack.Count)
      {
        return;
      }

      (stack[current], stack[target]) = (stack[target], stack[current]);
      current = target;

      design.Touch();
      Refresh();
    }

    private void OpenPicker()
    {
      UIForgeFrames.EditingSlot = slot.Key;

      // Retenu pour savoir, au retour, si un calque a ete ajoute : c'est celui-la
      // qu'on voudra regler, et le chercher a la main apres l'avoir pose serait un
      // pas de trop dans un geste qu'on repete.
      countBeforePicker = Stack().Count;

      MenuNav.Push(Main, ModRegisters.MenuState<UIForgeFramePicker>());
    }

    private void Drop()
    {
      List<ForgePick> stack = Stack();

      if (stack.Count == 0)
      {
        return;
      }

      stack.RemoveAt(current);

      if (stack.Count == 0)
      {
        design.Set(slot.Key, null);
      }

      design.Touch();
      Bound();
      Refresh();

      Sounds.ui_move1.Play(160f, 1f);
    }

    private void Clear()
    {
      design.Set(slot.Key, null);
      current = 0;
      Refresh();

      Sounds.ui_move1.Play(160f, 1f);
    }
  }

  /// <summary>
  /// La pose assemblee, le calque courant en clair et les autres attenues.
  ///
  /// Deux images superposees et non une seule : c'est ce qui permet de voir ce que la
  /// ligne DECALAGE deplace. Un empilement de trois images ou tout est de la meme
  /// intensite ne dit pas lequel bouge, et on regle a l'aveugle.
  ///
  /// Les deux assemblages ont la meme taille et la meme fenetre - <see
  /// cref="ForgeCompose"/> calcule la boite englobante sur tous les calques quel que
  /// soit celui qu'on lui demande de dessiner - donc rien ne se decale entre les
  /// deux couches.
  /// </summary>
  public class UIForgeLayerPreview : UIForgePanel
  {
    private readonly Func<ForgePick> selected;

    /// <summary>
    /// Ligne libre sous l'apercu, relue a chaque image.
    ///
    /// Relue et non figee au moment du Show : elle suit le calque courant, qui change
    /// sans que l'apercu ait a etre refait.
    /// </summary>
    private readonly Func<string> note;

    private Texture2D all;
    private Texture2D one;
    private int width;
    private int height;
    private int windowX;
    private int windowY;
    private float zoom = 4f;
    private string caption;

    public UIForgeLayerPreview(Vector2 position, Func<ForgePick> selected, Func<string> note)
        : base(position)
    {
      this.selected = selected;
      this.note = note;
    }

    public void Show(ForgeDesign design, string slotKey)
    {
      Release();

      if (design == null)
      {
        return;
      }

      if (ForgeCompose.Count(design, slotKey) == 0)
      {
        caption = "EMPTY";
        return;
      }

      ForgePose pose = ForgeCompose.Pose(design, slotKey);

      if (pose == null)
      {
        caption = "SHEET MISSING";
        return;
      }

      try
      {
        all = Adopt(pose);

        width = pose.Width;
        height = pose.Height;
        windowX = pose.WindowX;
        windowY = pose.WindowY;
        zoom = ZoomFor(pose.Width, pose.Height);

        ForgePose isolated = ForgeCompose.Pose(design, slotKey, selected());
        one = isolated == null ? null : Adopt(isolated);

        caption = pose.Drawn > 1 ? pose.Drawn + " IMAGES" : null;
      }
      catch (Exception e)
      {
        Log.Error($"[Forge] apercu des calques impossible : {e.Message}");
        caption = "PREVIEW FAILED";
      }
    }

    private static Texture2D Adopt(ForgePose pose)
    {
      var texture = new Texture2D(Engine.Instance.GraphicsDevice, pose.Width, pose.Height);
      texture.SetData(pose.Pixels);
      return texture;
    }

    public void Release()
    {
      try { all?.Dispose(); } catch { }
      try { one?.Dispose(); } catch { }
      all = null;
      one = null;
      caption = null;
    }

    public override void Render()
    {
      base.Render();

      float boxWidth = (width > 0 ? width : ForgeSlots.SourceCell) * zoom;
      float boxHeight = (height > 0 ? height : ForgeSlots.SourceCell) * zoom;

      // C'est la FENETRE qu'on centre, pas le canevas.
      //
      // Le canevas grandit des qu'un calque deborde : le centrer ferait sauter tout
      // l'apercu d'un cran a chaque pixel de decalage, y compris le cadre orange, et
      // on reglerait contre une reference mobile. Ancre sur la fenetre, le cadre reste
      // immobile et seul le personnage bouge - ce qui est exactement ce que la touche
      // fait.
      float window = ForgeSlots.Frame * zoom;
      var corner = new Vector2(
          Position.X - window * 0.5f - windowX * zoom,
          Position.Y - window * 0.5f - windowY * zoom);

      Draw.Rect(corner.X, corner.Y, boxWidth, boxHeight, Color.Black * 0.55f);
      Draw.HollowRect(new Rectangle((int)corner.X, (int)corner.Y, (int)boxWidth, (int)boxHeight), Color.Gray);

      if (all != null && !all.IsDisposed)
      {
        Draw.SpriteBatch.Draw(all, corner, null, Color.White * 0.35f, 0f, Vector2.Zero,
            zoom, SpriteEffects.None, 0f);
      }

      if (one != null && !one.IsDisposed)
      {
        Draw.SpriteBatch.Draw(one, corner, null, Color.White, 0f, Vector2.Zero,
            zoom, SpriteEffects.None, 0f);
      }

      if (all != null && !all.IsDisposed)
      {
        // La place d'un archer du jeu. Dessinee par-dessus les deux couches : c'est
        // par rapport a elle qu'on aligne, elle ne doit jamais passer dessous.
        DrawVanillaFrame(corner, windowX + ForgeSlots.AnchorX, windowY + ForgeSlots.AnchorY, zoom);
      }

      float line = corner.Y + boxHeight + 8f;

      if (!string.IsNullOrEmpty(caption))
      {
        Draw.OutlineTextCentered(TFGame.Font, MenuText.Safe(caption),
            new Vector2(Position.X, line), Color.Gray, Color.Black);
        line += 10f;
      }

      string subtitle = note?.Invoke();

      if (!string.IsNullOrEmpty(subtitle))
      {
        Draw.OutlineTextCentered(TFGame.Font, MenuText.Safe(subtitle),
            new Vector2(Position.X, line), Color.Gray, Color.Black);
      }
    }

  }
}
