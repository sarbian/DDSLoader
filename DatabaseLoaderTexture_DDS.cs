using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace DDSLoader
{
    [DatabaseLoaderAttrib(new[] {"dds"})]
    public class DatabaseLoaderTexture_DDS : DatabaseLoader<GameDatabase.TextureInfo>
    {
        private static string error;

        public override IEnumerator Load(UrlDir.UrlFile urlFile, FileInfo file)
        {
            Texture2D texture = LoadDDS(file.FullName);

            if (texture == null)
            {
                Debug.LogWarning("DDSLoader Texture load error with '" + file.FullName + "': " + error);
                successful = false;
                obj = null;
            }
            else if (!Path.GetFileNameWithoutExtension(file.Name).EndsWith("NRM"))
            {
                GameDatabase.TextureInfo textureInfo = new GameDatabase.TextureInfo(texture, false, false, true);
                obj = textureInfo;
                successful = true;
            }
            else
            {
                // This assume the loaded normal texture is already in the right format
                GameDatabase.TextureInfo textureInfo = new GameDatabase.TextureInfo(texture, true, false, true);
                obj = textureInfo;
                successful = true;
            }
            yield return null;
        }

        // DDS Texture loader inspired by
        // http://answers.unity3d.com/questions/555984/can-you-load-dds-textures-during-runtime.html#answer-707772
        // http://msdn.microsoft.com/en-us/library/bb943992.aspx
        // http://msdn.microsoft.com/en-us/library/windows/desktop/bb205578(v=vs.85).aspx
        public static Texture2D LoadDDS(string filename)
        {
            using (BinaryReader reader = new BinaryReader(File.Open(filename, FileMode.Open, FileAccess.Read)))
            {
                int dwMagic = (int)reader.ReadUInt32();

                int dwSize = (int)reader.ReadUInt32();
                //this header byte should be 124 for DDS image files
                if (dwSize != 124)
                {
                    error = "Invalid header size";
                    return null;
                }

                int dwFlags = (int)reader.ReadUInt32();
                int dwHeight = (int)reader.ReadUInt32();
                int dwWidth = (int)reader.ReadUInt32();

                int dwPitchOrLinearSize = (int)reader.ReadUInt32();
                int dwDepth = (int)reader.ReadUInt32();
                int dwMipMapCount = (int)reader.ReadUInt32();

                // dwReserved1
                for (int i = 0; i < 11; i++)
                {
                    reader.ReadUInt32();
                }

                // DDS_PIXELFORMAT
                int dds_pxlf_dwSize = (int)reader.ReadUInt32();
                int dds_pxlf_dwFlags = (int)reader.ReadUInt32();
                byte[] dds_pxlf_dwFourCC = reader.ReadBytes(4);
                string fourCC = Encoding.ASCII.GetString(dds_pxlf_dwFourCC);
                int dds_pxlf_dwRGBBitCount = (int)reader.ReadUInt32();
                int dds_pxlf_dwRBitMask = (int)reader.ReadUInt32();
                int dds_pxlf_dwGBitMask = (int)reader.ReadUInt32();
                int dds_pxlf_dwBBitMask = (int)reader.ReadUInt32();
                int dds_pxlf_dwABitMask = (int)reader.ReadUInt32();

                int dwCaps = (int)reader.ReadUInt32();
                int dwCaps2 = (int)reader.ReadUInt32();
                int dwCaps3 = (int)reader.ReadUInt32();
                int dwCaps4 = (int)reader.ReadUInt32();
                int dwReserved2 = (int)reader.ReadUInt32();

                long dxtBytesLength = reader.BaseStream.Length - 128;
                TextureFormat textureFormat = TextureFormat.ARGB32;

                if (fourCC == "DXT1")
                {
                    textureFormat = TextureFormat.DXT1;
                }
                else if (fourCC == "DXT5")
                {
                    textureFormat = TextureFormat.DXT5;
                }
                byte[] dxtBytes = reader.ReadBytes((int)dxtBytesLength);

                if (textureFormat == TextureFormat.DXT1 || textureFormat == TextureFormat.DXT5)
                {
                    Texture2D texture = new Texture2D(dwWidth, dwHeight, textureFormat, dwMipMapCount > 0);
                    texture.LoadRawTextureData(dxtBytes);
                    texture.Apply();
                    return texture;
                }
                error = "Only DXT1 and DXT5 are supported";
                return null;
            }
        }
    }
}