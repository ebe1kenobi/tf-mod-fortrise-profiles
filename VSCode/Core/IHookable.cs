using FortRise;

namespace TFModFortRiseProfiles;

public interface IHookable
{
  abstract static void Load(IHarmony harmony);
}
