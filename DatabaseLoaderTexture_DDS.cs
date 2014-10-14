using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DDSLoader
{
    [DatabaseLoaderAttrib(new[] {"dds"})]
    public class DatabaseLoaderTexture_DDS : DatabaseLoader<GameDatabase.TextureInfo>
    {
        private const int DDSD_MIPMAPCOUNT_BIT = 0x00020000;
        private const int DDPF_ALPHAPIXELS = 0x00000001;
        private const int DDPF_FOURCC = 0x00000004;
        private const int DDPF_RGB = 0x00000040;

        private static string error;
        private static bool isCompressed;

        public override IEnumerator Load(UrlDir.UrlFile urlFile, FileInfo file)
        {
            Texture2D texture = LoadDDS(file.FullName);

            if (texture == null)
            {
                Debug.LogWarning("DDSLoader Texture load error with '" + file.FullName + "': " + error);
                successful = false;
                obj = null;
            }
            else
            {
                // This assume the loaded normal texture is already in the right format
                bool isNormalMap = Path.GetFileNameWithoutExtension(file.Name).EndsWith("NRM");
                GameDatabase.TextureInfo textureInfo = new GameDatabase.TextureInfo(texture, isNormalMap, false,
                    isCompressed);
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
            if (!File.Exists(filename))
            {
                error = "File does not exist";
                return null;
            }
            using (BinaryReader reader = new BinaryReader(File.Open(filename, FileMode.Open, FileAccess.Read)))
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

                long dxtBytesLength = reader.BaseStream.Length - 128;

                TextureFormat textureFormat = TextureFormat.ARGB32;
                isCompressed = false;

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

                byte[] dxtBytes = reader.ReadBytes((int)dxtBytesLength);

                // Swap red and blue.
                if (!isCompressed)
                {
                    int mipmapWidth = dwWidth;
                    int mipmapHeight = dwHeight;
                    int lineStart = 0;

                    for (int i = 0; i < dwMipMapCount; ++i)
                    {
                        int mipmapPitch = ((mipmapWidth * pixelSize + 3) / 4) * 4;

                        for (int y = 0; y < mipmapHeight; ++y, lineStart += mipmapPitch)
                        {
                            int pos = lineStart;

                            for (int x = 0; x < mipmapWidth; ++x, pos += pixelSize)
                            {
                                byte r = dxtBytes[pos + 0];
                                byte b = dxtBytes[pos + 2];

                                dxtBytes[pos + 0] = b;
                                dxtBytes[pos + 2] = r;
                            }
                        }

                        mipmapWidth = Math.Max(1, mipmapWidth / 2);
                        mipmapHeight = Math.Max(1, mipmapHeight / 2);
                    }
                }

                Texture2D texture = new Texture2D(dwWidth, dwHeight, textureFormat, dwMipMapCount > 1);
                texture.LoadRawTextureData(dxtBytes);
                texture.Apply();
                return texture;
            }
        }

        private static bool fourCCEquals(IList<byte> bytes, string s)
        {
            return bytes[0] == s[0] && bytes[1] == s[1] && bytes[2] == s[2] && bytes[3] == s[3];
        }
    }
}