using UnityEngine;

namespace DDSLoader
{
[KSPAddon(KSPAddon.Startup.Instantly, false)]
public class Settings : MonoBehaviour
{
    public static int MipmapBias = 0;
    public static int NormalMipmapBias = 0;

    public void Awake()
    {
        foreach (var config in GameDatabase.Instance.GetConfigs("DDSLoader"))
        {
            string sMipmapBias = config.config.GetValue("mipmapBias");
            if (sMipmapBias != null)
                int.TryParse(sMipmapBias, out MipmapBias);

            string sNormalMipmapBias = config.config.GetValue("normalMipmapBias");
            if (sNormalMipmapBias != null)
                int.TryParse(sNormalMipmapBias, out NormalMipmapBias);
        }
    }
}
}
