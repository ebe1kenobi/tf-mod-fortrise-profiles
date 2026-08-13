using System;
using System.Xml;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Le sprite de l'archer, anime et a sa taille de jeu, avec les couleurs du profil.
  ///
  /// Corps et tete sont deux sprites distincts, et leur assemblage n'est pas fixe :
  /// le corps porte une liste HeadYOrigins avec une valeur par frame, et le jeu
  /// rappelle a chaque image <c>headSprite.Origin.Y = headYOrigins[frame du corps]</c>
  /// (Player.cs). C'est ce qui fait suivre la tete quand le corps se baisse ou court.
  /// Poser les deux sprites au meme point sans cela met la tete n'importe ou.
  ///
  /// Les deux jeux d'animations n'ont rien en commun non plus : le corps connait
  /// stand / run / jump / duck, la tete idle / duck / idleJump. D'ou la correspondance
  /// explicite plus bas.
  /// </summary>
  public class UISpritePreview : MenuItem
  {
    /// <summary>Animation du corps, et celle de tete qui lui repond.</summary>
    private static readonly (string Body, string Head)[] Animations =
    {
      ("stand", "idle"),
      ("run", "idle"),
      ("jump", "idleJump"),
      ("duck", "duck")
    };

    /// <summary>
    /// Les trois coiffes, dans l'ordre ou l'aperÃ§u les fait defiler. Chacune est une
    /// planche distincte : recolorer la tete normale ne touche pas celle sans chapeau.
    /// </summary>
    private static readonly string[] HeadParts =
    {
      SpriteRecolor.Head, SpriteRecolor.HeadNoHat, SpriteRecolor.HeadCrown
    };

    /// <summary>Les six poses du cadavre. Toutes tiennent en une seule image.</summary>
    private static readonly string[] CorpseAnimations =
    {
      "ground", "fall", "pinned", "slouched", "flying", "ledge"
    };

    /// <summary>
    /// Les poses de l'arc. "drawn" est l'arc bande : ses images ne sont pas celles du
    /// repos et n'emploient pas forcement les memes teintes, il faut donc pouvoir les
    /// juger recolorees.
    /// </summary>
    private static readonly string[] BowAnimations = { "idle", "drawn", "drawnEmpty" };

    /// <summary>
    /// Pose de l'arc ou la fleche est encochee. "drawnEmpty" est justement l'arc bande
    /// sans fleche : le jeu n'y montre pas l'aimer non plus.
    /// </summary>
    private const string DrawnBow = "drawn";

    /// <summary>Ce que Player fait de la fleche encochee : ArrowOffset et Origin.Y.</summary>
    private static readonly Vector2 AimOffset = new Vector2(0f, -1f);
    private const float AimOriginY = 5f;

    private const float SecondsPerAnimation = 2f;

    private readonly Vector2 tweenFrom;
    private readonly Vector2 tweenTo;

    /// <summary>Place voulue a l'ecran, hors defilement de la liste.</summary>
    private Vector2 anchor;

    /// <summary>Decalage du cadavre par rapport au joueur, pour les voir cote a cote.</summary>
    private static readonly Vector2 CorpseOffset = new Vector2(34f, 0f);

    private ProfileData profile;
    private Sprite<string> body;
    private Sprite<string> head;
    private Sprite<string> corpse;
    private Sprite<string> bow;
    private Image aim;
    private Vector2 bowBase;
    private int[] headXOrigins;
    private int[] headYOrigins;
    private int[] bowXOffsets;
    private int[] bowYOffsets;
    private int animation;
    private int headVariant;
    private int corpseAnimation;
    private int bowAnimation;
    private float animationTimer;

    public UISpritePreview(Vector2 position) : base(position)
    {
      tweenTo = position;
      anchor = position;
      tweenFrom = position - Vector2.UnitY * 60f;
    }

    /// <summary>Reconstruit l'apercu : a l'ouverture, et apres chaque couleur changee.</summary>
    public void Rebuild(ProfileData profile)
    {
      this.profile = profile;

      // Entity.Remove ne tolere pas le null : au premier appel les sprites n'existent
      // pas encore.
      if (body != null)
      {
        Remove(body);
        body = null;
      }

      if (head != null)
      {
        Remove(head);
        head = null;
      }

      if (corpse != null)
      {
        Remove(corpse);
        corpse = null;
      }

      if (bow != null)
      {
        Remove(bow);
        bow = null;
      }

      if (aim != null)
      {
        Remove(aim);
        aim = null;
      }

      headXOrigins = null;
      headYOrigins = null;
      bowXOffsets = null;
      bowYOffsets = null;

      ArcherData archer = ArcherCatalog.DataOf(profile);
      if (archer == null)
      {
        return;
      }

      body = Make(archer.Sprites.Body, SpriteRecolor.Body);

      // L'arc est ajoute juste apres le corps pour passer devant lui, et avant la
      // tete pour rester dessous - c'est l'ordre du jeu.
      bow = Make(archer.Sprites.Bow, SpriteRecolor.Bow);
      if (bow != null)
      {
        // Sa position de repos vient de sa propre definition ; les decalages par frame
        // s'y ajoutent ensuite, sans quoi ils s'accumuleraient d'une image a l'autre.
        bowBase = bow.Position;
        PlayBow(bow);
      }

      aim = MakeAim(archer);

      corpse = MakeCorpse(archer);
      ReadHeadOrigins(archer);
      BuildHead(archer);
    }

    /// <summary>
    /// (Re)construit la tete pour la coiffe courante. Elle change de planche et non
    /// d'animation : chaque coiffe est un sprite distinct dans les donnees du jeu.
    /// </summary>
    private void BuildHead(ArcherData archer)
    {
      if (head != null)
      {
        Remove(head);
        head = null;
      }

      string part = HeadParts[headVariant];
      string spriteId = part switch
      {
        SpriteRecolor.HeadNoHat => archer.Sprites.HeadNoHat,
        SpriteRecolor.HeadCrown => archer.Sprites.HeadCrown,
        _ => archer.Sprites.HeadNormal
      };

      head = Make(spriteId, part);
      PlayCurrent();
    }

    /// <summary>
    /// La fleche encochee, telle que Player la pose : au decalage de tir, origine
    /// remontee de 5 pixels pour que la hampe tombe sur la corde.
    ///
    /// Elle n'existe que visee, et l'apercu ne vise pas : on la montre pendant la pose
    /// "drawn" de l'arc, la seule ou le jeu l'affiche aussi.
    /// </summary>
    private Image MakeAim(ArcherData archer)
    {
      try
      {
        Subtexture source = SpriteRecolor.Baked(profile, SpriteRecolor.Aim, fromDiskFirst: false)
            ?? archer.Aimer;

        if (source == null)
        {
          return null;
        }

        // La pose de l'arc survit aux reconstructions : on accorde la fleche a celle en
        // cours, sinon une reconstruction faite pendant "drawn" la ferait disparaitre
        // jusqu'a la pose suivante - soit deux secondes de doute apres chaque retouche.
        var image = new Image(source, null)
        {
          Position = AimOffset,
          Visible = BowAnimations[bowAnimation] == DrawnBow
        };

        image.Origin.Y = AimOriginY;

        Add(image);
        return image;
      }
      catch (Exception e)
      {
        Log.Error($"[Preview] fleche encochee indisponible : {e.Message}");
        return null;
      }
    }

    private Sprite<string> MakeCorpse(ArcherData archer)
    {
      if (string.IsNullOrEmpty(archer.Corpse))
      {
        return null;
      }

      try
      {
        // Le cadavre vient d'une autre table de sprites que le joueur.
        Sprite<string> sprite = TFGame.CorpseSpriteData.GetSpriteString(archer.Corpse);
        if (sprite == null)
        {
          return null;
        }

        Subtexture recolored = SpriteRecolor.Baked(profile, SpriteRecolor.Corpse, fromDiskFirst: false);
        if (recolored != null)
        {
          sprite.SwapSubtexture(recolored);
        }

        // Le sprite de cadavre ne declare ni origine ni decalage : il se dessine coin
        // haut-gauche sur sa position, alors que le corps du joueur a son origine aux
        // pieds. On lui donne la meme convention pour que les deux partagent le sol,
        // sinon le cadavre pend sous le cadre.
        sprite.Origin = new Vector2(sprite.Width * 0.5f, sprite.Height);
        sprite.Position = CorpseOffset + Vector2.UnitY * 8f;

        PlayCorpse(sprite);
        Add(sprite);
        return sprite;
      }
      catch (Exception e)
      {
        Log.Error($"[Preview] cadavre {archer.Corpse} indisponible : {e.Message}");
        return null;
      }
    }

    /// <summary>
    /// Les positions de tete, une par frame du corps, telles que le jeu les lit.
    /// HeadXOrigins est facultatif ; HeadYOrigins est toujours present.
    /// </summary>
    private void ReadHeadOrigins(ArcherData archer)
    {
      try
      {
        XmlElement xml = TFGame.SpriteData.GetXML(archer.Sprites.Body);
        if (xml == null)
        {
          return;
        }

        if (xml.HasChild("HeadXOrigins"))
        {
          headXOrigins = Calc.ReadCSVInt(xml["HeadXOrigins"].InnerText);
        }

        if (xml.HasChild("HeadYOrigins"))
        {
          headYOrigins = Calc.ReadCSVInt(xml["HeadYOrigins"].InnerText);
        }

        // Les decalages de l'arc sont facultatifs : beaucoup d'archers n'en declarent
        // pas, leur arc reste alors a sa position de repos.
        if (xml.HasChild("BowXOffsets"))
        {
          bowXOffsets = Calc.ReadCSVInt(xml["BowXOffsets"].InnerText);
        }

        if (xml.HasChild("BowYOffsets"))
        {
          bowYOffsets = Calc.ReadCSVInt(xml["BowYOffsets"].InnerText);
        }
      }
      catch (Exception e)
      {
        Log.Error($"[Preview] positions de tete illisibles : {e.Message}");
      }
    }

    private Sprite<string> Make(string spriteId, string part)
    {
      if (string.IsNullOrEmpty(spriteId))
      {
        return null;
      }

      try
      {
        Sprite<string> sprite = TFGame.SpriteData.GetSpriteString(spriteId);
        if (sprite == null)
        {
          return null;
        }

        // Fabrication depuis l'atlas et non depuis le PNG deja exporte : c'est ce qui
        // rend le changement visible immediatement, sans quoi l'apercu reafficherait
        // la version enregistree au passage precedent.
        Subtexture recolored = SpriteRecolor.Baked(profile, part, fromDiskFirst: false);
        if (recolored != null)
        {
          sprite.SwapSubtexture(recolored);
        }

        Add(sprite);
        return sprite;
      }
      catch (Exception e)
      {
        Log.Error($"[Preview] sprite {spriteId} indisponible : {e.Message}");
        return null;
      }
    }

    public override void Update()
    {
      base.Update();

      Position = MenuCamera.Fixed(MainMenu, anchor);

      animationTimer += Engine.DeltaTime;
      if (animationTimer >= SecondsPerAnimation)
      {
        animationTimer = 0f;
        animation = (animation + 1) % Animations.Length;

        // La coiffe change une fois le tour des animations boucle : on voit ainsi
        // chaque tete dans toutes les poses, plutot que de changer les deux a la fois.
        if (animation == 0)
        {
          headVariant = (headVariant + 1) % HeadParts.Length;

          ArcherData archer = ArcherCatalog.DataOf(profile);
          if (archer != null)
          {
            BuildHead(archer);
          }
        }
        else
        {
          PlayCurrent();
        }

        // Le cadavre avance a son propre rythme : ses six poses ne tombent pas juste
        // sur les quatre animations du joueur, et rien n'oblige a les synchroniser.
        corpseAnimation = (corpseAnimation + 1) % CorpseAnimations.Length;
        PlayCorpse(corpse);

        bowAnimation = (bowAnimation + 1) % BowAnimations.Length;
        PlayBow(bow);
      }

      FollowHead();
    }

    /// <summary>
    /// Recale la tete sur la frame courante du corps, comme le fait Player a chaque
    /// image. Sans ce suivi la tete reste a l'origine de sa planche, c'est-a-dire
    /// sous le corps.
    /// </summary>
    private void FollowHead()
    {
      if (body == null || head == null)
      {
        return;
      }

      int frame = body.CurrentFrame;

      if (headXOrigins != null && frame < headXOrigins.Length)
      {
        head.Origin.X = headXOrigins[frame];
      }

      if (headYOrigins != null && frame < headYOrigins.Length)
      {
        head.Origin.Y = headYOrigins[frame];
      }

      FollowBow(frame);
    }

    /// <summary>
    /// Recale l'arc sur la frame courante du corps. Le jeu repart de sa position de
    /// repos a chaque image avant d'ajouter le decalage, et non de la precedente :
    /// cumuler ferait deriver l'arc hors du personnage.
    /// </summary>
    private void FollowBow(int frame)
    {
      if (bow == null)
      {
        return;
      }

      Vector2 position = bowBase;

      if (bowXOffsets != null && frame < bowXOffsets.Length)
      {
        position.X += bowXOffsets[frame];
      }

      if (bowYOffsets != null && frame < bowYOffsets.Length)
      {
        position.Y += bowYOffsets[frame];
      }

      bow.Position = position;
    }

    private void PlayCorpse(Sprite<string> sprite)
    {
      Play(sprite, CorpseAnimations[corpseAnimation]);
    }

    private void PlayBow(Sprite<string> sprite)
    {
      Play(sprite, BowAnimations[bowAnimation]);

      if (aim != null)
      {
        aim.Visible = BowAnimations[bowAnimation] == DrawnBow;
      }
    }

    private void PlayCurrent()
    {
      Play(body, Animations[animation].Body);
      Play(head, Animations[animation].Head);
    }

    private static void Play(Sprite<string> sprite, string animationId)
    {
      if (sprite == null)
      {
        return;
      }

      try
      {
        if (sprite.ContainsAnimation(animationId))
        {
          sprite.Play(animationId, false);
        }
        else if (sprite.ContainsAnimation("idle"))
        {
          sprite.Play("idle", false);
        }
        else if (sprite.ContainsAnimation("stand"))
        {
          sprite.Play("stand", false);
        }
      }
      catch (Exception e)
      {
        Log.Error($"[Preview] animation {animationId} : {e.Message}");
      }
    }

    public override void Render()
    {
      ArcherData archer = ArcherCatalog.DataOf(profile);
      if (archer != null)
      {
        // Fond uni derriere le sprite : sur le fond anime du menu, un pixel art sombre
        // devient illisible et on ne juge plus la couleur.
        // Le cadre couvre le joueur et le cadavre pose a sa droite.
        Draw.Rect(Position.X - 22f, Position.Y - 34f, 78f, 44f, Color.Black * 0.55f);
        Draw.HollowRect(new Rectangle((int)Position.X - 22, (int)Position.Y - 34, 78, 44), archer.ColorA * 0.8f);
      }

      base.Render();
    }

    public override void TweenIn()
    {
      anchor = tweenFrom;
      Tween tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeOut, 20, true);
      tween.OnUpdate = t => anchor = Vector2.Lerp(tweenFrom, tweenTo, t.Eased);
      Add(tween);
    }

    public override void TweenOut()
    {
      Vector2 start = anchor;
      Tween tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeIn, 12, true);
      tween.OnUpdate = t => anchor = Vector2.Lerp(start, tweenFrom, t.Eased);
      Add(tween);
    }

    protected override void OnSelect() { }

    protected override void OnDeselect() { }

    protected override void OnConfirm() { }
  }
}
