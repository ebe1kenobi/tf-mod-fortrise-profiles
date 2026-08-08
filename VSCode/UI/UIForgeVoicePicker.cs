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
  /// Le choix d'un WAV pour une action de voix.
  ///
  /// La banque est celle des profils - le dossier `wav` de l'espace de sauvegarde -
  /// et non une seconde qui lui serait propre. Ce sont les memes fichiers, deposes
  /// par la meme personne : deux dossiers a tenir a jour seraient un dossier de trop.
  ///
  /// La ligne survolee joue son fichier. C'est le seul moyen de choisir un son : lire
  /// un nom de fichier n'apprend rien sur ce qu'on va entendre.
  /// </summary>
  public class UIForgeVoicePicker : CustomMenuState
  {
    private const float FirstRowY = 52f;
    private const float RowStep = 14f;
    private const float RowX = 30f;

    private ForgeDesign design;
    private ForgeVoiceAction action;

    /// <summary>
    /// L'ecoute en cours, gardee pour pouvoir la couper.
    ///
    /// SoundEffect.Play() lance un son qu'on ne rattrape plus : il faut passer par
    /// une instance. Sans elle, survoler trois sons longs les superpose, et quitter
    /// l'ecran laisse le dernier finir par-dessus le menu.
    /// </summary>
    private Microsoft.Xna.Framework.Audio.SoundEffectInstance playing;

    public UIForgeVoicePicker(MainMenu main) : base(main)
    {
    }

    public override void Create()
    {
      MainMenu.MenuState voiceState = ModRegisters.MenuState<UIForgeVoice>();

      design = UIForgeList.Editing;
      action = ForgeVoice.Get(UIForgeVoice.EditingAction);

      if (design == null || action == null)
      {
        Main.State = voiceState;
        return;
      }

      ScreenTitles.Apply(Main, ModRegisters.MenuState<UIForgeVoicePicker>());
      Main.BackState = voiceState;
      Main.TweenBGCameraToY(2);

      Main.Add(new UIPickerHeader(new Vector2(160f, 34f), design.Name, action.Label));

      ProfileSfx.RefreshPool();
      IReadOnlyList<SoundFile> pool = ProfileSfx.Pool;

      var rows = new List<UIMenuRow>();

      // Premiere ligne : revenir au son de la voix de repli. C'est le geste inverse
      // du choix, il doit etre au meme endroit et pas cache sur une touche.
      UIMenuRow noneRow = MakeRow(rows.Count, "VOIX DE REPLI");
      noneRow.RightText = () => ForgeVoice.FileOf(design, action.Key) == null ? "OUI" : "";
      noneRow.OnConfirmed = () => Choose(null);
      rows.Add(noneRow);

      foreach (SoundFile file in pool)
      {
        SoundFile captured = file;

        UIMenuRow row = MakeRow(rows.Count,
            UIForgeEdit.Shorten(Path.GetFileNameWithoutExtension(captured.Name).ToUpperInvariant()));

        row.RightText = () => IsChosen(captured) ? "OUI" : "";
        row.OnConfirmed = () => Choose(captured.Name);
        row.OnSelected = () => Preview(captured);
        rows.Add(row);
      }

      if (pool.Count == 0)
      {
        Main.Add(new UIForgeVoiceHint(new Vector2(160f, 120f)));
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
      Main.MaxUICameraY = Math.Max(0f, lastY - 190f);
      Main.ToStartSelected = rows[0];
    }

    public override void Destroy()
    {
      // Le choix est deja enregistre a la validation : il n'y a rien a sauver ici.
      // Il y a en revanche un son a couper - quitter l'ecran doit couper ce qu'on
      // etait en train d'ecouter.
      Stop();
    }

    private void Stop()
    {
      try
      {
        if (playing != null && !playing.IsDisposed)
        {
          playing.Stop();
          playing.Dispose();
        }
      }
      catch (Exception e)
      {
        Log.Error($"[Forge] ecoute non arretee : {e.Message}");
      }

      playing = null;
    }

    private UIMenuRow MakeRow(int index, string label)
    {
      float y = FirstRowY + index * RowStep;
      var from = new Vector2(index % 2 == 0 ? -200f : 520f, y);
      return new UIMenuRow(new Vector2(RowX, y), from, label);
    }

    private bool IsChosen(SoundFile file)
    {
      return string.Equals(ForgeVoice.FileOf(design, action.Key), file.Name,
          StringComparison.OrdinalIgnoreCase);
    }

    private void Choose(string file)
    {
      ForgeVoice.Assign(design, action.Key, file);
      ForgeStorage.Save();
      Sounds.ui_click.Play(160f, 1f);
      Main.State = ModRegisters.MenuState<UIForgeVoice>();
    }

    private void Preview(SoundFile file)
    {
      // Le son precedent s'arrete avant que le suivant parte : on compare deux sons,
      // on ne les mélange pas.
      Stop();

      try
      {
        using Stream stream = file.Open();
        var effect = Microsoft.Xna.Framework.Audio.SoundEffect.FromStream(stream);

        if (effect == null)
        {
          return;
        }

        playing = effect.CreateInstance();
        playing.Volume = Audio.MasterVolume;
        playing.Pitch = Audio.MasterPitch;
        playing.Play();
      }
      catch (Exception e)
      {
        // Un WAV illisible ne doit pas fermer l'ecran : on l'ecoute justement pour
        // decouvrir qu'il ne va pas.
        Log.Error($"[Forge] {file.Name} non joue : {e.Message}");
        Sounds.ui_invalid.Play(160f, 1f);
      }
    }
  }

  /// <summary>Ce qui s'affiche quand la banque WAV est vide.</summary>
  public class UIForgeVoiceHint : UIForgePanel
  {
    public UIForgeVoiceHint(Vector2 position) : base(position)
    {
    }

    public override void Render()
    {
      base.Render();

      string[] lines =
      {
        "AUCUN SON DANS LA BANQUE",
        "",
        "DEPOSER DES WAV DANS",
        "SAVES/EBE1.PROFILES/WAV"
      };

      for (int i = 0; i < lines.Length; i++)
      {
        Draw.OutlineTextCentered(TFGame.Font, MenuText.Safe(lines[i]),
            Position + new Vector2(0f, i * 12f), Color.Gray, Color.Black);
      }
    }
  }
}
