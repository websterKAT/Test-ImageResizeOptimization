using BenchmarkDotNet.Attributes;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace ImageResizeTest
{
    public class ResizeBenchmark
    {
        private byte[] imageBytes;

        [GlobalSetup]
        public void Setup()
        {
            string inputPath = "25mb.jpg";
            if (!File.Exists(inputPath))
            {
                throw new FileNotFoundException("Input file not found.", inputPath);
            }
            imageBytes = File.ReadAllBytes(inputPath);
        }

        [Benchmark]
        public byte[] ResizeNew() => ResizeImage(imageBytes, 800, 600);

        [Benchmark]
        public byte[] ResizeImageOld() => ResizeImageOld(imageBytes, 800, 600);

        public static byte[] ResizeImage(byte[] imageBytes, int desiredWidth, int desiredHeight)
        {
            using (var inputStream = new MemoryStream(imageBytes))
            using (var originalImage = Image.FromStream(inputStream, useEmbeddedColorManagement: true, validateImageData: true))
            {
                // Calculate resize ratios
                var ratioX = (double)desiredWidth / originalImage.Width;
                var ratioY = desiredHeight > 0 ? (double)desiredHeight / originalImage.Height : 0;
                var ratio = desiredHeight > 0 ? Math.Min(ratioX, ratioY) : ratioX;

                var newWidth = (int)(originalImage.Width * ratio);
                var newHeight = (int)(originalImage.Height * ratio);

                // Avoid creating huge bitmaps
                if (newWidth <= 0 || newHeight <= 0)
                    throw new ArgumentException("Calculated dimensions are invalid.");

                using (var destImage = new Bitmap(newWidth, newHeight, PixelFormat.Format24bppRgb)) // Better compatibility and less memory
                {
                    destImage.SetResolution(originalImage.HorizontalResolution, originalImage.VerticalResolution);

                    using (var graphics = Graphics.FromImage(destImage))
                    {
                        graphics.CompositingMode = CompositingMode.SourceCopy;
                        graphics.CompositingQuality = CompositingQuality.HighQuality;
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.SmoothingMode = SmoothingMode.HighQuality;
                        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                        using (var wrapMode = new ImageAttributes())
                        {
                            wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                            graphics.DrawImage(originalImage, new Rectangle(0, 0, newWidth, newHeight), 0, 0, originalImage.Width, originalImage.Height, GraphicsUnit.Pixel, wrapMode);
                        }
                    }

                    using (var outputStream = new MemoryStream())
                    {
                        var jpgEncoder = GetEncoder(ImageFormat.Jpeg);
                        var encoderParams = new EncoderParameters(1);
                        encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 85L); // Better compression control

                        destImage.Save(outputStream, jpgEncoder, encoderParams);
                        return outputStream.ToArray();
                    }
                }
            }
        }

        public static byte[] ResizeImageOld(byte[] imageBytes, int desiredWidth, int desiredHeight)
        {
            using (MemoryStream ms = new MemoryStream(imageBytes))
            {
                // Convert byte[] to Image
                ms.Write(imageBytes, 0, imageBytes.Length);
                Image image = Image.FromStream(ms, true);

                var ratioX = (double)desiredWidth / image.Width;
                var ratioY = desiredHeight > 0 ? (double)desiredHeight / image.Height : 0;
                var ratio = desiredHeight > 0 ? Math.Min(ratioX, ratioY) : ratioX;

                var newWidth = (int)(image.Width * ratio);
                var newHeight = (int)(image.Height * ratio);

                var destRect = new Rectangle(0, 0, newWidth, newHeight);

                var destImage = new Bitmap(newWidth, newHeight, PixelFormat.Format16bppRgb555);

                destImage.SetResolution(8, 8);

                using (var graphics = Graphics.FromImage(destImage))
                {
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.SmoothingMode = SmoothingMode.HighQuality;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                    using (var wrapMode = new ImageAttributes())
                    {
                        wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                        graphics.DrawImage(image, destRect);
                    }
                }

                EncoderParameters parameters = new EncoderParameters(1);
                parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Compression, 8);

                ImageCodecInfo jpgEncoder = GetEncoder(ImageFormat.Jpeg);

                // Create an Encoder object based on the GUID  
                // for the Quality parameter category.  
                using (MemoryStream fileBytes = new MemoryStream())
                {
                    destImage.Save(fileBytes, jpgEncoder, parameters);


                    return fileBytes.ToArray();
                }
            }
        }
        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }


    }
}
