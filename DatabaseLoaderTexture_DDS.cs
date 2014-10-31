using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DDSLoader
{
    [DatabaseLoaderAttrib(new[] { "dds" })]
    public class DatabaseLoaderTexture_DDS : DatabaseLoader<GameDatabase.TextureInfo>
    {
        private const uint DDSD_MIPMAPCOUNT_BIT = 0x00020000;
        private const uint DDPF_ALPHAPIXELS = 0x00000001;
        private const uint DDPF_FOURCC = 0x00000004;
        private const uint DDPF_RGB = 0x00000040;
        private const uint DDPF_NORMAL = 0x80000000;

        private static string error;

        public override IEnumerator Load(UrlDir.UrlFile urlFile, FileInfo file)
        {
            obj = LoadDDS(urlFile);
            successful = obj != null;

            if (!successful)
                Debug.LogWarning("DDSLoader Texture load error with '" + urlFile.url + "': " + error);

            yield return null;
        }

        // DDS Texture loader inspired by
        // http://answers.unity3d.com/questions/555984/can-you-load-dds-textures-during-runtime.html#answer-707772
        // http://msdn.microsoft.com/en-us/library/bb943992.aspx
        // http://msdn.microsoft.com/en-us/library/windows/desktop/bb205578(v=vs.85).aspx
        public static GameDatabase.TextureInfo LoadDDS(UrlDir.UrlFile urlFile)
        {
            if (!File.Exists(urlFile.fullPath))
            {
                error = "File does not exist";
                return null;
            }
            using (BinaryReader reader = new BinaryReader(File.Open(urlFile.fullPath, FileMode.Open, FileAccess.Read)))
            {
                byte[] dwMagic = reader.ReadBytes(4);

                if (!fourCCEquals(dwMagic, "DDS "))
                {
                    error = "Invalid DDS file";
                    return null;
                }

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

                if ((dwFlags & DDSD_MIPMAPCOUNT_BIT) == 0)
                {
                    dwMipMapCount = 1;
                }

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
                int pixelSize = dds_pxlf_dwRGBBitCount / 8;
                int dds_pxlf_dwRBitMask = (int)reader.ReadUInt32();
                int dds_pxlf_dwGBitMask = (int)reader.ReadUInt32();
                int dds_pxlf_dwBBitMask = (int)reader.ReadUInt32();
                int dds_pxlf_dwABitMask = (int)reader.ReadUInt32();

                int dwCaps = (int)reader.ReadUInt32();
                int dwCaps2 = (int)reader.ReadUInt32();
                int dwCaps3 = (int)reader.ReadUInt32();
                int dwCaps4 = (int)reader.ReadUInt32();
                int dwReserved2 = (int)reader.ReadUInt32();

                TextureFormat textureFormat = TextureFormat.ARGB32;
                bool isCompressed = false;
                bool isNormalMap = (dds_pxlf_dwFlags & DDPF_NORMAL) != 0 || urlFile.name.EndsWith("NRM");

                if ((dds_pxlf_dwFlags & DDPF_FOURCC) != 0)
                {
                    // Texture dos not contain RGB data, check FourCC for format
                    isCompressed = true;

                    if (fourCCEquals(dds_pxlf_dwFourCC, "DXT1"))
                    {
                        textureFormat = TextureFormat.DXT1;
                    }
                    else if (fourCCEquals(dds_pxlf_dwFourCC, "DXT5"))
                    {
                        textureFormat = TextureFormat.DXT5;
                    }
                }
                else if ((dds_pxlf_dwFlags & DDPF_RGB) != 0)
                {
                    // RGB or RGBA format
                    textureFormat = (dds_pxlf_dwFlags & DDPF_ALPHAPIXELS) != 0
                        ? TextureFormat.RGBA32
                        : TextureFormat.RGB24;
                }
                else
                {
                    error = "Only DXT1, DXT5, RGB24 and RGBA32 are supported";
                    return null;
                }

                long dataBias = 128;

                if (Settings.MipmapBias != 0 || Settings.NormalMipmapBias != 0)
                {
                    int bias = isNormalMap ? Settings.NormalMipmapBias : Settings.MipmapBias;
                    int blockSize = textureFormat == TextureFormat.DXT1 ? 8 : 16;
                    int levels = Math.Min(bias, dwMipMapCount - 1);

                    for (int i = 0; i < levels; ++i)
                    {
                        dataBias += isCompressed
                            ? ((dwWidth + 3) / 4) * ((dwHeight + 3) / 4) * blockSize
                            : dwWidth * dwHeight * pixelSize;

                        dwWidth = Math.Max(1, dwWidth / 2);
                        dwHeight = Math.Max(1, dwHeight / 2);
                    }
                }

                long dxtBytesLength = reader.BaseStream.Length - dataBias;
                reader.BaseStream.Seek(dataBias, SeekOrigin.Begin);
                byte[] dxtBytes = reader.ReadBytes((int)dxtBytesLength);

                // Swap red and blue.
                if (!isCompressed)
                {
                    for (int i = 0; i < dxtBytes.Length; i += pixelSize)
                    {
                        byte b = dxtBytes[i + 0];
                        byte r = dxtBytes[i + 2];

                        dxtBytes[i + 0] = r;
                        dxtBytes[i + 2] = b;
                    }
                }

                Texture2D texture = new Texture2D(dwWidth, dwHeight, textureFormat, dwMipMapCount > 1);
                texture.LoadRawTextureData(dxtBytes);
                texture.Apply(false, true);

                return new GameDatabase.TextureInfo(texture, isNormalMap, false, isCompressed);
            }
        }

        private static bool fourCCEquals(IList<byte> bytes, string s)
        {
            return bytes[0] == s[0] && bytes[1] == s[1] && bytes[2] == s[2] && bytes[3] == s[3];
        }
    }
}
