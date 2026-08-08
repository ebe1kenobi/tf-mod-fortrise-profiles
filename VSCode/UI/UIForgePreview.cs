using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using TowerFall;

namespace TFModFortRiseProfiles
{
  /// <summary>
  /// Apercu de l'archer en cours de forge : le corps assemble, anime.
  ///
  /// Une planche immobile ne dit pas grand-chose. Ce qui se juge, dans un archer,
  /// c'est la course - trois images qui doivent s'enchainer sans que le personnage
  /// saute ni s'enfonce dans le sol. C'est aussi la seule facon de voir qu'une
  /// fenetre de decoupe est mal calee d'un pixel.
  ///
  /// Les planches sont refabriquees quand le dessin change, et pas plus souvent : le
  /// numero de revision du dessin sert exactement a cela. Sans lui, chaque image de
  /// l'ecran relirait seize PNG.
  /// </summary>
  public class UIForgePreview : UIForgePanel
  {
    private const float BoxWidth = 76f;
    private const float BoxHeight = 76f;
    private const int Scale = 2;

    /// <summary>Images de la course, dans l'ordre ou l'animation les enchaine.</summary>
    private static readonly int[] RunFrames = { 2, 1, 2, 3 };

    private const float RunDelay = 8f;

    private ForgeDesign shown;
    private int shownRevision = -1;
    private Texture2D body;
    private int frameCount;
    private int missing;
    private string failure;

    private float timer;

    public UIForgePreview(Vector2 position) : base(position)
    {
    }

    public void Show(ForgeDesign design)
    {
      if (design == null)
      {
        Release();
        shown = null;
        shownRevision = -1;
        return;
      }

      if (shown == design && shownRevision == design.Revision)
      {
        return;
      }

      Release();
      shown = design;
      shownRevision = design.Revision;

      if (design.PickOf("stand") == null)
      {
        failure = "PAS DE POSE DEBOUT";
        return;
      }

      // Le corps seul : c'est tout ce qui s'affiche ici, et cette methode est
      // rappelee a chaque ligne survolee.
      ForgeImage strip = ForgeBuild.BuildBody(design);

      if (strip == null)
      {
        failure = "ASSEMBLAGE IMPOSSIBLE";
        return;
      }

      try
      {
        body = new Texture2D(Engine.Instance.GraphicsDevice, strip.Width, strip.Height);
        body.SetData(strip.Pixels);
        frameCount = strip.Width / ForgeSlots.Frame;
        missing = design.Missing().Count;
        failure = null;
      }
      catch (Exception e)
      {
        Log.Error($"[Forge] apercu impossible : {e.Message}");
        failure = "APERCU IMPOSSIBLE";
      }
    }

    public void Release()
    {
      try { body?.Dispose(); } catch { }
      body = null;
      frameCount = 0;
      missing = 0;
      failure = null;
    }

    public override void Update()
    {
      base.Update();
      timer += Engine.TimeMult;
    }

    public override void Render()
    {
      base.Render();

      var corner = new Vector2(Position.X - BoxWidth * 0.5f, Position.Y - BoxHeight * 0.5f);
      Draw.Rect(corner.X, corner.Y, BoxWidth, BoxHeight, Color.Black * 0.55f);
      Draw.HollowRect(new Rectangle((int)corner.X, (int)corner.Y, (int)BoxWidth, (int)BoxHeight), Color.Gray);

      if (failure != null)
      {
        Draw.OutlineTextCentered(TFGame.Font, MenuText.Safe(failure), Position, Color.Gray, Color.Black);
        return;
      }

      if (body == null || body.IsDisposed || frameCount == 0)
      {
        return;
      }

      int index = RunFrames[(int)(timer / RunDelay) % RunFrames.Length];

      // Une planche a trous a moins d'images que d'emplacements. Sortir du cadre
      // dessinerait la moitie d'une autre pose, ce qui se prend pour un defaut de
      // decoupe alors que ce n'en est pas un.
      if (index >= frameCount)
      {
        index = 0;
      }

      var source = new Rectangle(index * ForgeSlots.Frame, 0, ForgeSlots.Frame, ForgeSlots.Frame);
      var origin = new Vector2(ForgeSlots.Frame / 2f, ForgeSlots.Frame / 2f);

      Draw.SpriteBatch.Draw(body, Position, source, Color.White, 0f, origin,
          Scale, SpriteEffects.None, 0f);

      if (missing > 0)
      {
        Draw.OutlineTextCentered(TFGame.Font, MenuText.Safe(missing + " A REMPLIR"),
            new Vector2(Position.X, corner.Y + BoxHeight + 8f), Color.Orange, Color.Black);
      }
    }

  }
}
