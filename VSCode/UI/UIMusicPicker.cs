using System;
using System.Collections.Generic;
using System.IO;
using FortRise;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Ce sur quoi le choix de musique porte : lire la valeur courante, et l'ecrire.
  ///
  /// L'ecran sert a la forge comme aux profils, qui rangent leur musique dans deux
  /// objets differents. Deux delegues plutot que deux ecrans presque identiques - ils
  /// n'auraient differe que par une ligne d'affectation.
  /// </summary>
  internal static class MusicEditing
  {
    public static Func<string> Get;
    public static Action<string> Set;

    /// <summary>Ce que l'entete affiche : le nom du profil ou de l'archer.</summary>
    public static string Subject;
  }

  /// <summary>
  /// Choix d'un FICHIER comme musique de victoire.
  ///
  /// Les pistes du jeu se font defiler sur la ligne meme, a la fleche : elles sont
  /// treize et se connaissent par coeur. Un fichier apporte, non - il y en a autant
  /// qu'on en depose, et les faire defiler un a un pour arriver au dernier n'est pas
  /// un choix, c'est une epreuve. D'ou cet ecran, calque sur celui des sons.
  ///
  /// Les deux banques sont proposees ensemble : celle des musiques et celle des WAV.
  /// Un jingle de victoire est un WAV comme un autre, et obliger a le recopier dans
  /// un second dossier ne servirait qu'a expliquer pourquoi il y a deux dossiers.
  /// </summary>
  public class UIMusicPicker : CustomMenuState
  {
    private const float FirstRowY = 52f;
    private const float RowStep = 15f;
    private const float RowX = 20f;

    public UIMusicPicker(MainMenu main) : base(main)
    {
    }

    public override void Create()
    {
      MainMenu.MenuState back = MenuNav.Arrive(Main, ModRegisters.MenuState<UIProfilesMenu>());

      if (MusicEditing.Get == null || MusicEditing.Set == null)
      {
        MenuNav.Switch(Main, back);
        return;
      }

      ScreenTitles.Apply(Main, ModRegisters.MenuState<UIMusicPicker>());
      Main.BackState = back;
      Main.TweenBGCameraToY(2);

      Main.Add(new UIPickerHeader(new Vector2(160f, 34f), MusicEditing.Subject ?? "", "VICTORY MUSIC"));

      List<string> files = ForgeMusic.BankFiles();

      if (files.Count == 0)
      {
        Main.Add(new UIMusicBankHint(new Vector2(160f, 120f)));
        Main.MaxUICameraY = 0f;
        Main.ToStartSelected = null;
        return;
      }

      var rows = new List<UIMenuRow>();

      // La sortie par le haut : reprendre une piste du jeu, c'est-a-dire ne plus
      // designer de fichier. Sans elle, un fichier choisi ne se retirerait plus.
      UIMenuRow none = MakeRow(rows.Count, "< NO FILE >");
      none.RightText = () => ForgeMusic.IsFile(MusicEditing.Get()) ? "" : "[X]";
      none.OnConfirmed = () =>
      {
        MusicEditing.Set(ForgeMusic.Auto);
        Sounds.ui_click.Play(160f, 1f);
      };
      rows.Add(none);

      foreach (string file in files)
      {
        string captured = file;

        UIMenuRow row = MakeRow(rows.Count, Path.GetFileNameWithoutExtension(file).ToUpperInvariant());
        row.RightText = () => Chosen(captured) ? "[X]" : "";
        row.OnConfirmed = () =>
        {
          MusicEditing.Set(ForgeMusic.FilePrefix + captured);
          Sounds.ui_click.Play(160f, 1f);
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

      MenuNav.Track(Main, rows);
      Main.ToStartSelected = rows[MenuNav.Resume(Main, rows.Count)];
    }

    public override void Destroy()
    {
      // Les delegues designent le profil ou l'archer qu'on vient de quitter : les
      // garder ferait ecrire dans un objet qui n'est plus a l'ecran.
      MusicEditing.Get = null;
      MusicEditing.Set = null;
      MusicEditing.Subject = null;
    }

    private static bool Chosen(string file)
    {
      return string.Equals(ForgeMusic.FileNameOf(MusicEditing.Get()), file,
          StringComparison.OrdinalIgnoreCase);
    }

    private UIMenuRow MakeRow(int index, string label)
    {
      float y = FirstRowY + index * RowStep;
      var from = new Vector2(index % 2 == 0 ? -280f : 600f, y);

      return new UIMenuRow(new Vector2(RowX, y), from, label) { ContentWidth = 240f };
    }
  }

  /// <summary>Ou deposer ses musiques, quand la banque est vide.</summary>
  public class UIMusicBankHint : Entity
  {
    private readonly Vector2 at;

    public UIMusicBankHint(Vector2 position) : base(0)
    {
      at = position;
      Depth = -100;
    }

    public override void Render()
    {
      base.Render();

      Draw.OutlineTextCentered(TFGame.Font, "DROP WAV OR OGG FILES IN",
          at + new Vector2(0f, -12f), Color.White, Color.Black, 1f);

      // Le chemin en entier, coupe en deux lignes : c'est la seule chose que cet
      // ecran a a dire, et la deviner demanderait de fouiller les sauvegardes.
      Draw.OutlineTextCentered(TFGame.Font, "SAVES/ARCHER/MUSIC",
          at + new Vector2(0f, 2f), Color.Gray, Color.Black, 1f);

      Draw.OutlineTextCentered(TFGame.Font, "OR SAVES/ARCHER/WAV",
          at + new Vector2(0f, 14f), Color.Gray, Color.Black, 1f);
    }
  }
}
