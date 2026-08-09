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
  /// Les seize emplacements, un par ligne : la voie manuelle.
  ///
  /// Ce qui compte ici est de voir d'un coup d'oeil ce qui manque. Un emplacement
  /// vide affiche EMPTY et sa vignette reste noire ; c'est ce qui distingue un
  /// archer fini d'un archer qui boitera. La vignette montre la pose elle-meme et
  /// non le nom de sa case : "r04c21" ne dit rien, un personnage qui saute si.
  ///
  /// Le bouton Alt vide un emplacement. Utile pour repartir d'une planche source
  /// propre apres avoir bricole, ce que la conservation des retouches empeche
  /// autrement.
  /// </summary>
  public class UIForgeFrames : CustomMenuState
  {
    /// <summary>Emplacement que le selecteur doit ouvrir.</summary>
    internal static string EditingSlot;

    private const float FirstRowY = 46f;
    private const float RowStep = 15f;
    private const float RowX = 40f;
    private static readonly Vector2 ThumbPosition = new Vector2(250f, 100f);

    private ForgeDesign design;
    private UIForgeCellThumb thumb;

    public UIForgeFrames(MainMenu main) : base(main)
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

      ScreenTitles.Apply(Main, ModRegisters.MenuState<UIForgeFrames>());
      Main.BackState = editState;
      Main.TweenBGCameraToY(2);

      thumb = new UIForgeCellThumb(ThumbPosition);
      Main.Add(thumb);

      var rows = new List<UIMenuRow>();

      // Les entetes sont posees dans le menu mais absentes de cette liste : c'est
      // elle qui forme la chaine haut/bas, et un titre n'a pas a s'y selectionner.
      var selectable = new List<UIMenuRow>();

      // Rang de l'emplacement qu'on vient de quitter pour le selecteur, afin d'y
      // revenir. L'ecran est reconstruit a chaque entree : sans ce reperage, revenir
      // du selecteur rendrait la selection a la premiere ligne, et la vignette
      // montrerait la pose debout au lieu de celle qu'on vient de composer.
      int resume = 0;

      // Une entete par destination. Dix-neuf lignes d'affilee ne disent pas ou finit
      // le personnage et ou commencent ses cadavres ; les separer le dit sans un mot.
      var groups = new (ForgeSheet Sheet, string Title)[]
      {
        (ForgeSheet.Body, "-- CORPS --"),
        (ForgeSheet.Corpse, "-- CADAVRE --"),
        (ForgeSheet.Hat, "-- CHAPEAU --"),

        // Facultative, et posee en dernier pour cette raison : une planche qui dessine
        // deja la tete dans le corps n'en veut pas une seconde.
        (ForgeSheet.Head, "-- TETE (FACULTATIF) --")
      };

      foreach (var group in groups)
      {
        rows.Add(Header(rows.Count, group.Title));

        foreach (ForgeSlot slot in ForgeSlots.Of(group.Sheet))
        {
          ForgeSlot captured = slot;
          int index = rows.Count;
          float y = FirstRowY + index * RowStep;
          var from = new Vector2(index % 2 == 0 ? -260f : 580f, y);

          var row = new UIMenuRow(new Vector2(RowX, y), from, slot.Label)
          {
            ContentWidth = 165f,
            RightText = () => Label(captured),
            OnConfirmed = () => Pick(captured),
            OnSelected = () => thumb.Show(design, captured.Key),

            // Valider mene au choix d'une image, Alt aux calques deja poses. Le
            // chemin le plus court reste celui qu'on emprunte a chaque pose ; le
            // reglage fin, lui, ne sert qu'une fois la pose en place.
            OnAlt = () => Layers(captured),
            AltGuide = "CALQUES"
          };

          if (string.Equals(slot.Key, EditingSlot, StringComparison.Ordinal))
          {
            resume = selectable.Count;
          }

          rows.Add(row);
          selectable.Add(row);
        }
      }

      for (int i = 0; i < selectable.Count; i++)
      {
        if (i > 0)
        {
          selectable[i].UpItem = selectable[i - 1];
        }

        if (i + 1 < selectable.Count)
        {
          selectable[i].DownItem = selectable[i + 1];
        }
      }

      Main.Add(rows);

      float lastY = FirstRowY + (rows.Count - 1) * RowStep;
      Main.MaxUICameraY = Math.Max(0f, lastY - 180f);
      Main.ToStartSelected = selectable[Math.Clamp(resume, 0, selectable.Count - 1)];
    }

    public override void Destroy()
    {
      thumb?.Release();
      thumb = null;
    }

    /// <summary>
    /// Une entete de categorie.
    ///
    /// Elle est posee dans le menu mais tenue HORS de la chaine haut/bas : une ligne
    /// sans action y resterait traversable et ferait un cran mort a chaque groupe.
    /// UIMenuRow n'a pas de notion de ligne inerte, et lui en ajouter une pour trois
    /// titres reviendrait a toucher la selection de tout le mod.
    /// </summary>
    private UIMenuRow Header(int index, string title)
    {
      float y = FirstRowY + index * RowStep;
      var from = new Vector2(index % 2 == 0 ? -260f : 580f, y);

      return new UIMenuRow(new Vector2(RowX, y), from, title) { ContentWidth = 165f };
    }

    private string Label(ForgeSlot slot)
    {
      // Une pose faite de plusieurs images le dit : c'est ce qui distingue un bras
      // pose par-dessus d'un bras qu'on a cru poser.
      int layers = design.LayersOf(slot.Key).Count;

      if (layers > 1)
      {
        return layers + " IMAGES";
      }

      ForgePick pick = design.PickOf(slot.Key);

      if (pick == null)
      {
        return "EMPTY";
      }

      // Le nom de la planche plutot que celui de la case : on veut savoir de quel
      // personnage la pose vient, ce qui se voit quand on en a melange plusieurs.
      // La case, elle, est sous les yeux dans la vignette.
      return UIForgeEdit.Shorten(pick.Source);
    }

    private void Pick(ForgeSlot slot)
    {
      EditingSlot = slot.Key;
      UIForgeFramePicker.ReturnToLayers = false;
      Main.State = ModRegisters.MenuState<UIForgeFramePicker>();
    }

    /// <summary>
    /// Ouvre les calques de cet emplacement : ordre, cadrage, suppression.
    ///
    /// C'est la qu'est parti le vidage, qui occupait cette touche : il y voisine avec
    /// la suppression d'un seul calque, et vider une pose est de toute facon plus
    /// rare que la composer.
    /// </summary>
    private void Layers(ForgeSlot slot)
    {
      EditingSlot = slot.Key;
      UIForgeLayers.EditingSlot = slot.Key;
      Main.State = ModRegisters.MenuState<UIForgeLayers>();
    }
  }

  /// <summary>
  /// La vignette d'un emplacement : la pose choisie, telle qu'elle sera decoupee.
  ///
  /// La fenetre de decoupe est dessinee par-dessus la case entiere, et c'est le
  /// point de l'affaire : on voit a la fois ce que la source contient et ce que la
  /// forge en gardera. Un personnage dont les pieds sortent du cadre se repere ici
  /// et nulle part ailleurs.
  ///
  /// Elle montre la pose ENTIERE, calques fusionnes, et non sa premiere image :
  /// empiler des bras sur un corps ne se verifie qu'en regardant le resultat, et une
  /// vignette qui s'arrete au fond dirait que l'empilement n'a rien fait.
  /// </summary>
  public class UIForgeCellThumb : UIForgePanel
  {
    private Texture2D texture;
    private int width;
    private int height;
    private int windowX;
    private int windowY;
    private float zoom = 4f;
    private string caption;

    public UIForgeCellThumb(Vector2 position) : base(position)
    {
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
        // Des calques enregistres mais pas une image lisible : la planche a ete
        // renommee ou sortie du vivier. Le dire, plutot que laisser une case noire
        // qu'on prendrait pour un emplacement vide.
        caption = "CELL MISSING";
        return;
      }

      try
      {
        texture = new Texture2D(Engine.Instance.GraphicsDevice, pose.Width, pose.Height);
        texture.SetData(pose.Pixels);
        width = pose.Width;
        height = pose.Height;
        windowX = pose.WindowX;
        windowY = pose.WindowY;
        zoom = ZoomFor(pose.Width, pose.Height);

        // Le nom de la case ne veut plus rien dire des qu'il y en a plusieurs : on
        // dit alors combien, ce qui est la seule chose verifiable d'un coup d'oeil.
        caption = pose.Drawn > 1
            ? pose.Drawn + " IMAGES"
            : design.PickOf(slotKey).Cell.ToString().ToUpperInvariant();
      }
      catch (Exception e)
      {
        Log.Error($"[Forge] vignette impossible : {e.Message}");
        caption = "PREVIEW FAILED";
      }
    }

    public void Release()
    {
      try { texture?.Dispose(); } catch { }
      texture = null;
      caption = null;
    }

    public override void Render()
    {
      base.Render();

      float boxWidth = (width > 0 ? width : ForgeSlots.SourceCell) * zoom;
      float boxHeight = (height > 0 ? height : ForgeSlots.SourceCell) * zoom;

      var corner = new Vector2(Position.X - boxWidth * 0.5f, Position.Y - boxHeight * 0.5f);
      Draw.Rect(corner.X, corner.Y, boxWidth, boxHeight, Color.Black * 0.55f);
      Draw.HollowRect(new Rectangle((int)corner.X, (int)corner.Y, (int)boxWidth, (int)boxHeight), Color.Gray);

      if (texture != null && !texture.IsDisposed)
      {
        Draw.SpriteBatch.Draw(texture, corner, null, Color.White, 0f, Vector2.Zero,
            zoom, SpriteEffects.None, 0f);

        // Le cadre de ce que la forge gardera.
        Draw.HollowRect(new Rectangle(
            (int)(corner.X + windowX * zoom),
            (int)(corner.Y + windowY * zoom),
            (int)(ForgeSlots.Frame * zoom),
            (int)(ForgeSlots.Frame * zoom)), Color.Orange * 0.8f);
      }

      if (!string.IsNullOrEmpty(caption))
      {
        Draw.OutlineTextCentered(TFGame.Font, MenuText.Safe(caption),
            new Vector2(Position.X, corner.Y + boxHeight + 8f), Color.Gray, Color.Black);
      }
    }

  }
}
