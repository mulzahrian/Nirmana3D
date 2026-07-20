using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using OpenTK.Graphics.OpenGL4;
using PixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat;

namespace nirmana.Rendering
{
    /// <summary>
    /// Load gambar (PNG/JPG/BMP, apapun yang didukung System.Drawing) dan
    /// upload ke GPU sebagai GL texture 2D. Bisa dari file di disk, atau
    /// dari byte array di memori (dipakai untuk texture yang di-embed di
    /// dalam file .glb/.fbx waktu import).
    /// </summary>
    public class Texture : IDisposable
    {
        public int Handle { get; }
        public string FilePath { get; } // null kalau texture ini berasal dari embedded data (bukan file)

        public Texture(string filePath)
        {
            FilePath = filePath;
            using (Bitmap bitmap = new Bitmap(filePath))
            {
                Handle = UploadBitmap(bitmap);
            }
        }

        /// <summary>Bikin texture dari data gambar mentah di memori (mis. hasil ekstrak embedded texture).</summary>
        public Texture(byte[] rawImageBytes)
        {
            FilePath = null;
            using (MemoryStream ms = new MemoryStream(rawImageBytes))
            using (Bitmap bitmap = new Bitmap(ms))
            {
                Handle = UploadBitmap(bitmap);
            }
        }

        private static int UploadBitmap(Bitmap bitmap)
        {
            int handle = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, handle);

            BitmapData data = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            GL.TexImage2D(
                TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                bitmap.Width, bitmap.Height, 0,
                PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0);

            bitmap.UnlockBits(data);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

            GL.BindTexture(TextureTarget.Texture2D, 0);
            return handle;
        }

        public void Bind(TextureUnit unit = TextureUnit.Texture0)
        {
            GL.ActiveTexture(unit);
            GL.BindTexture(TextureTarget.Texture2D, Handle);
        }

        public void Dispose()
        {
            GL.DeleteTexture(Handle);
        }
    }
}