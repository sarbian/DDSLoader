using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DDSLoader
{
[KSPAddon(KSPAddon.Startup.Instantly, false)]
public class Settings : MonoBehaviour
{
    public static int MipmapBias = 0;
    public static int NormalMipmapBias = 0;

    static List<Regex> normalList = new List<Regex>();
    static List<Regex> readableList = new List<Regex>();

    public void Awake()
    {
        foreach (var config in GameDatabase.Instance.GetConfigNodes("DDSLoader"))
        {
            string sMipmapBias = config.GetValue("mipmapBias");
            if (sMipmapBias != null)
                int.TryParse(sMipmapBias, out MipmapBias);

            string sNormalMipmapBias = config.GetValue("normalMipmapBias");
            if (sNormalMipmapBias != null)
                int.TryParse(sNormalMipmapBias, out NormalMipmapBias);


            ConfigNode normals = config.GetNode("NORMAL_LIST");
            if (normals != null)
            {
                foreach (ConfigNode.Value texture in normals.values)
                {
                    Regex re = new Regex(texture.value, RegexOptions.None);
                    normalList.Add(re);
                }
            }

            ConfigNode readable = config.GetNode("READABLE_LIST");
            if (readable != null)
            {
                foreach (ConfigNode.Value texture in readable.values)
                {
                    Regex re = new Regex(texture.value, RegexOptions.None);
                    readableList.Add(re);
                }
            }
        }
        
        foreach (ConfigNode config in GameDatabase.Instance.GetConfigNodes("ACTIVE_TEXTURE_MANAGER_CONFIG"))
        {
            ConfigNode normals = config.GetNode("NORMAL_LIST");
            if (normals != null)
            {
                foreach (ConfigNode.Value texture in normals.values)
                {
                    Regex re = new Regex(texture.value, RegexOptions.None);
                    normalList.Add(re);
                    Debug.Log("DDSLoader normal " + texture.value);
                }
            }

            foreach (ConfigNode overrides in config.GetNodes("OVERRIDES"))
            {
                foreach (ConfigNode texture in overrides.nodes)
                {
                    string readable = texture.GetValue("make_not_readable");
                    if (readable != null && readable == "false")
                    {
                        Regex re = new Regex(texture.name, RegexOptions.None);
                        readableList.Add(re);
                        Debug.Log("DDSLoader readable " + texture.name);
                    }
                }
            }
        }
    }

    public static bool keepReadable(string url)
    {
        foreach (Regex regex in readableList)
        {
            if (regex.IsMatch(url))
                return true;
        }
        return false;
    }

    public static bool isNormal(string url)
    {
        foreach (Regex regex in normalList)
        {
            if (regex.IsMatch(url))
                return true;
        }
        return false;
    }

}
}
