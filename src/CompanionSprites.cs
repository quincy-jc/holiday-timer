using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HolidayTimer
{
    // Original silhouette frames are alpha masks, so the selected scheme is
    // always the sole colour. Rectangles include the complete generated pose.
    internal static class CompanionSprites
    {
        private sealed class Frame
        {
            internal ImageBrush Mask;
            internal Rect Destination;
        }

        private static readonly Dictionary<string, BitmapFrame> Atlases = new Dictionary<string, BitmapFrame>();
        private static readonly Frame[] CatWalk = Load("walking-silhouettes.png", 378, .12,
            new int[,] { { 38, 98, 367, 283 }, { 493, 109, 357, 273 }, { 930, 102, 361, 282 }, { 1371, 102, 372, 282 } });
        private static readonly Frame[] DogWalk = Load("walking-silhouettes.png", 778, .12,
            new int[,] { { 30, 514, 402, 269 }, { 463, 504, 405, 279 }, { 896, 516, 419, 267 }, { 1349, 507, 410, 276 } });
        private static readonly Frame[] CatGroom = Load("animal-actions.png", 376, .12,
            new int[,] { { 71, 74, 325, 307 }, { 501, 74, 359, 307 }, { 935, 114, 336, 267 }, { 1376, 74, 336, 307 } });
        private static readonly Frame[] DogBark = Load("animal-actions.png", 786, .12,
            new int[,] { { 43, 493, 394, 298 }, { 487, 496, 369, 295 }, { 924, 480, 372, 311 }, { 1369, 504, 369, 287 } });
        private static readonly Frame[] BunnyHop = Load("wildlife-silhouettes.png", 435, .112,
            new int[,] { { 25, 141, 355, 299 }, { 480, 80, 332, 362 }, { 851, 95, 513, 301 }, { 1426, 151, 330, 288 } });
        private static readonly Frame[] BirdFly = Load("wildlife-silhouettes.png", 700, .112,
            new int[,] { { 32, 606, 337, 229 }, { 504, 483, 337, 297 }, { 951, 452, 345, 339 }, { 1387, 613, 354, 211 } });

        private static Frame[] Load(string resource, double anchorY, double scale, int[,] bounds)
        {
            BitmapFrame atlas;
            if (!Atlases.TryGetValue(resource, out atlas))
            {
                using (Stream stream = typeof(CompanionSprites).Assembly.GetManifestResourceStream("HolidayTimer.Companions." + resource))
                {
                    if (stream == null) throw new InvalidOperationException("A companion silhouette resource is missing: " + resource);
                    atlas = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                }
                atlas.Freeze(); Atlases.Add(resource, atlas);
            }
            Frame[] frames = new Frame[4];
            for (int i = 0; i < frames.Length; i++)
            {
                int x = bounds[i, 0], y = bounds[i, 1], width = bounds[i, 2], height = bounds[i, 3];
                CroppedBitmap crop = new CroppedBitmap(atlas, new Int32Rect(x, y, width, height)); crop.Freeze();
                ImageBrush mask = new ImageBrush(crop) { Stretch = Stretch.Fill }; mask.Freeze();
                double anchorX = atlas.PixelWidth * (i + .5) / 4.0;
                double baseline = resource == "wildlife-silhouettes.png" && anchorY == 700 ? 34 : 51;
                frames[i] = new Frame { Mask = mask, Destination = new Rect(32 + (x - anchorX) * scale,
                    baseline + (y - anchorY) * scale, width * scale, height * scale) };
            }
            return frames;
        }

        internal static void Draw(DrawingContext dc, CompanionKind kind, double time, double reaction, Brush colour)
        {
            Frame[] moving = kind == CompanionKind.Cat ? CatWalk : kind == CompanionKind.Dog ? DogWalk :
                kind == CompanionKind.Bunny ? BunnyHop : BirdFly;
            double speed = kind == CompanionKind.Bird ? 6.5 : kind == CompanionKind.Bunny ? 3.6 : 5.6;
            double activity = CompanionArt.Activity(kind, time);
            double gesture = kind == CompanionKind.Dog ? Math.Max(activity, reaction * .8) : activity;
            // Show one complete pose per frame: cross-fading whole animal
            // silhouettes briefly creates duplicate limbs and heads.
            if (gesture > .22 && (kind == CompanionKind.Dog || kind == CompanionKind.Cat))
            {
                double cycle = Math.Max(0, time) % 16;
                double actionFrame = activity > 0 ? Math.Max(0, Math.Min(3, (cycle - 10) * 3 / 2.4)) : (1 - reaction) * 2;
                Frame[] action = kind == CompanionKind.Cat ? CatGroom : DogBark;
                Paint(dc, action[Math.Min(3, (int)Math.Floor(actionFrame + .5))], colour);
            }
            else Paint(dc, moving[(int)Math.Floor(Math.Max(0, time * speed) + .5) % moving.Length], colour);
        }

        private static void Paint(DrawingContext dc, Frame frame, Brush colour)
        {
            dc.PushOpacityMask(frame.Mask);
            dc.DrawRectangle(colour, null, frame.Destination);
            dc.Pop();
        }
    }
}
