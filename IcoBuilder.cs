using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IconvApp
{
    public class IconBuilder
    {
        public static readonly int[] DefaultResolutions = { 16, 24, 32, 48, 64, 128, 256 };

        /// <summary>
        /// Builds a multi-size ICO file from a list of frames containing Size and ImageSource.
        /// </summary>
        public static void BuildIcon(IEnumerable<(int Size, BitmapSource Image)> framesData, string outputPath)
        {
            var frames = new List<byte[]>();
            var sortedFrames = framesData.OrderBy(f => f.Size).ToList();

            foreach (var item in sortedFrames)
            {
                var resized = PrepareImage(item.Image, item.Size);
                frames.Add(EncodeToPng(resized));
            }

            using var fs = new FileStream(outputPath, FileMode.Create);
            using var writer = new BinaryWriter(fs);

            // 1. ICONDIR Header (6 bytes)
            writer.Write((short)0);      // Reserved
            writer.Write((short)1);      // Type (1 = Icon)
            writer.Write((short)frames.Count); // Count

            // 2. ICONDIRENTRY Array (16 bytes per entry)
            int currentOffset = 6 + (16 * frames.Count);

            for (int i = 0; i < frames.Count; i++)
            {
                int size = sortedFrames[i].Size;
                byte bSize = (byte)(size >= 256 ? 0 : size);
                
                writer.Write(bSize);    // Width
                writer.Write(bSize);    // Height
                writer.Write((byte)0);  // Color count (0 for 32bpp)
                writer.Write((byte)0);  // Reserved
                writer.Write((short)1); // Planes
                writer.Write((short)32);// BitCount
                writer.Write(frames[i].Length); // Size of data
                writer.Write(currentOffset);    // Offset to data

                currentOffset += frames[i].Length;
            }

            // 3. Image Data
            foreach (var frameData in frames)
            {
                writer.Write(frameData);
            }
        }

        public static BitmapSource PrepareImage(BitmapSource source, int targetSize)
        {
            double sourceW = source.PixelWidth;
            double sourceH = source.PixelHeight;
            double ratioDiff = Math.Abs(sourceW - sourceH) / Math.Max(sourceW, sourceH);

            bool useCrop = ratioDiff >= 0.20; // 20% or more difference -> Crop

            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                var targetRect = new Rect(0, 0, targetSize, targetSize);
                context.DrawRectangle(Brushes.Transparent, null, targetRect);

                if (useCrop)
                {
                    // Center Crop logic
                    double scale = Math.Max(targetSize / sourceW, targetSize / sourceH);
                    double drawW = sourceW * scale;
                    double drawH = sourceH * scale;
                    double x = (targetSize - drawW) / 2;
                    double y = (targetSize - drawH) / 2;
                    
                    context.PushGuidelineSet(new GuidelineSet(new[] { 0.0, targetSize }, new[] { 0.0, targetSize }));
                    context.DrawImage(source, new Rect(x, y, drawW, drawH));
                    context.Pop();
                }
                else
                {
                    // Fit with Padding logic
                    double scale = Math.Min(targetSize / sourceW, targetSize / sourceH);
                    double drawW = sourceW * scale;
                    double drawH = sourceH * scale;
                    double x = (targetSize - drawW) / 2;
                    double y = (targetSize - drawH) / 2;

                    context.DrawImage(source, new Rect(x, y, drawW, drawH));
                }
            }

            var rtb = new RenderTargetBitmap(targetSize, targetSize, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            return rtb;
        }

        private static byte[] EncodeToPng(BitmapSource source)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }
    }
}
