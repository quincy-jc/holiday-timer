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
        private static readonly Frame[] Resting = LoadRest();
        private static readonly Geometry DogBodyClip = BodyBoundary("M 0,0 L 64,0 L 64,36 L 50,36 C 47,39 44,41 40,40 C 36,37 33,37 29,37 C 25,37 22,40 17,40 L 14,36 L 0,36 Z");
        private static readonly Geometry CatBodyClip = BodyBoundary("M 0,0 L 64,0 L 64,38 L 50,38 C 47,41 44,43 40,41 C 36,40 33,40 29,40 C 25,40 22,42 18,41 L 15,39 L 0,39 Z");

        private static Geometry BodyBoundary(string path)
        { Geometry geometry = Geometry.Parse(path); geometry.Freeze(); return geometry; }

        private static Frame[] LoadRest()
        {
            BitmapFrame atlas;
            using (Stream stream = typeof(CompanionSprites).Assembly.GetManifestResourceStream("HolidayTimer.Companions.animal-resting.png"))
            {
                if (stream == null) throw new InvalidOperationException("The resting companion artwork is missing.");
                atlas = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            }
            atlas.Freeze();
            int[,] bounds = { { 62, 100, 716, 346 }, { 947, 152, 536, 282 }, { 108, 603, 617, 331 }, { 1057, 583, 400, 354 } };
            double[] scales = { .067, .075, .071, .084 };
            Frame[] frames = new Frame[4];
            for (int i = 0; i < frames.Length; i++)
            {
                int x = bounds[i, 0], y = bounds[i, 1], width = bounds[i, 2], height = bounds[i, 3];
                CroppedBitmap crop = new CroppedBitmap(atlas, new Int32Rect(x, y, width, height)); crop.Freeze();
                ImageBrush mask = new ImageBrush(crop) { Stretch = Stretch.Fill }; mask.Freeze();
                frames[i] = new Frame { Mask = mask, Destination = new Rect(32 - width * scales[i] / 2,
                    51 - height * scales[i], width * scales[i], height * scales[i]) };
            }
            return frames;
        }

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

        internal static void Draw(DrawingContext dc, CompanionKind kind, double time, double reaction, Brush colour,
            CompanionAction action, double distance, double age)
        {
            Frame[] moving = kind == CompanionKind.Cat ? CatWalk : kind == CompanionKind.Dog ? DogWalk :
                kind == CompanionKind.Bunny ? BunnyHop : BirdFly;
            if (action == CompanionAction.Sleep || (action == CompanionAction.Rest && kind == CompanionKind.Bird))
            {
                dc.PushTransform(new ScaleTransform(1, 1 + .015 * Math.Sin(age * 2), 32, 51));
                Paint(dc, Resting[(int)kind - 1], colour); dc.Pop(); return;
            }
            if (action == CompanionAction.Rest)
            {
                Paint(dc, kind == CompanionKind.Cat ? CatGroom[0] : kind == CompanionKind.Dog ? DogBark[0] : moving[0], colour); return;
            }
            if (action == CompanionAction.Play && (kind == CompanionKind.Dog || kind == CompanionKind.Cat))
            {
                Frame[] poses = kind == CompanionKind.Cat ? CatGroom : DogBark;
                int frame = Math.Min(3, (int)(Math.Max(0, age) * 2.0) % 4);
                Paint(dc, poses[frame], colour); return;
            }
            if (action == CompanionAction.Play && (kind == CompanionKind.Bunny || kind == CompanionKind.Bird))
            {
                int frame = (int)(Math.Max(0, age) * (kind == CompanionKind.Bird ? 6 : 4)) % moving.Length;
                Paint(dc, moving[frame], colour); return;
            }
            if (kind == CompanionKind.Dog || kind == CompanionKind.Cat)
            { DrawWalkingAnimal(dc, kind, distance, colour); return; }
            // The rabbit and bird use separate four-frame artwork; preserve
            // their animation timing when tuning the quadrupeds' stride.
            double framePhase = Math.Max(0, distance) / (10 / CompanionMotion.StanceFraction * CompanionMotion.ArtScale);
            int index = (int)Math.Floor(framePhase * 4) % moving.Length;
            Paint(dc, moving[index], colour);
        }

        private static void DrawWalkingAnimal(DrawingContext dc, CompanionKind kind, double distance, Brush colour)
        {
            double cycle = CompanionMotion.StepPhase(distance);
            // Four alternating contacts. The contact half of each gait is
            // driven directly by distance, so planted paws do not slide.
            Leg(dc, 19, 35, cycle + .50, true, colour);
            Leg(dc, 40, 35, cycle + .25, false, colour);
            Leg(dc, 20, 35, cycle, true, colour);
            Leg(dc, 41, 35, cycle + .75, false, colour);
            dc.PushClip(kind == CompanionKind.Cat ? CatBodyClip : DogBodyClip);
            Paint(dc, kind == CompanionKind.Cat ? CatWalk[0] : DogWalk[0], colour); dc.Pop();
        }

        private static void Leg(DrawingContext dc, double x, double y, double phase, bool hind, Brush colour)
        {
            double stride = CompanionMotion.FootX(phase), lift = CompanionMotion.FootLift(phase);
            double footX = x + stride, footY = 51 - lift;
            // A dog's or cat's hind leg folds forward at the stifle and back
            // at the hock; the foreleg has a gentler, nearly vertical bend.
            // Keep the lower joints close to the foot instead of drawing a
            // second exaggerated zig-zag as the paw passes under the body.
            double kneeX = x + (hind ? 2.0 : -.4) + stride * .18;
            double kneeY = y + 7;
            double ankleX = x + (hind ? -.7 : 0) + stride * .72;
            double ankleY = footY - 3.0;
            double width = hind ? 2.5 : 1.8;
            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext g = geometry.Open())
            {
                g.BeginFigure(new Point(x - width, y), true, true);
                g.QuadraticBezierTo(new Point(x - width, y + 3), new Point(kneeX - 1.5, kneeY), true, false);
                g.QuadraticBezierTo(new Point(ankleX - 1.0, ankleY - 1), new Point(footX - .9, footY - .6), true, false);
                g.QuadraticBezierTo(new Point(footX - .9, footY + .6), new Point(footX + 1.5, footY + .6), true, false);
                g.QuadraticBezierTo(new Point(footX + 2.0, footY + .2), new Point(footX + .8, footY - .8), true, false);
                g.QuadraticBezierTo(new Point(ankleX + .9, ankleY - .5), new Point(kneeX + 1.3, kneeY), true, false);
                g.QuadraticBezierTo(new Point(x + width, y + 3), new Point(x + width, y), true, false);
            }
            geometry.Freeze(); dc.DrawGeometry(colour, null, geometry);
        }

        private static void Paint(DrawingContext dc, Frame frame, Brush colour)
        {
            dc.PushOpacityMask(frame.Mask);
            dc.DrawRectangle(colour, null, frame.Destination);
            dc.Pop();
        }
    }
}
