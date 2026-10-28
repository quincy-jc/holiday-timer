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
        private static readonly Geometry DogBodyClip = BodyBoundary("M 0,0 L 64,0 L 64,34 L 49,34 C 47,37 45,39 42,39 C 36,37 29,35.5 24,36 C 21,38 18,40 15,38 L 13,35 L 0,35 Z");
        private static readonly Geometry CatBodyClip = BodyBoundary("M 0,0 L 64,0 L 64,37 L 49,37 C 46,40 43,41 40,40 C 34,39 27,38 23,39 C 20,41 18,41 15,39 L 13,37 L 0,37 Z");

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
                if (kind == CompanionKind.Cat || kind == CompanionKind.Dog)
                { DrawWalkingAnimal(dc, kind, distance, colour, CompanionMotion.Smooth(age / .75)); return; }
                Paint(dc, moving[0], colour); return;
            }
            if (action == CompanionAction.Play && (kind == CompanionKind.Dog || kind == CompanionKind.Cat))
            {
                Frame[] poses = kind == CompanionKind.Cat ? CatGroom : DogBark;
                int frame = Math.Min(3, (int)(Math.Max(0, age) * 1.2) % 4);
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
            double framePhase = CompanionMotion.SpritePhase(distance);
            int index = (int)Math.Floor(framePhase * 4) % moving.Length;
            Paint(dc, moving[index], colour);
        }

        private static void DrawWalkingAnimal(DrawingContext dc, CompanionKind kind, double distance, Brush colour, double settle = 0)
        {
            double cycle = CompanionMotion.StepPhase(distance);
            bool cat = kind == CompanionKind.Cat;
            double bodyLift = .22 * (1 - Math.Cos(cycle * Math.PI * 4)) * (1 - settle);
            Leg(dc, QuadrupedGait.Pose(cat, true, true, cycle, bodyLift, settle), true, true, cat, colour);
            Leg(dc, QuadrupedGait.Pose(cat, false, true, cycle, bodyLift, settle), false, true, cat, colour);
            dc.PushTransform(new TranslateTransform(0, -bodyLift));
            dc.PushClip(kind == CompanionKind.Cat ? CatBodyClip : DogBodyClip);
            Paint(dc, kind == CompanionKind.Cat ? CatWalk[0] : DogWalk[0], colour); dc.Pop(); dc.Pop();
            Leg(dc, QuadrupedGait.Pose(cat, true, false, cycle, bodyLift, settle), true, false, cat, colour);
            Leg(dc, QuadrupedGait.Pose(cat, false, false, cycle, bodyLift, settle), false, false, cat, colour);
        }

        private static Point At(QuadrupedGait.Joint joint) { return new Point(joint.X, joint.Y); }

        private static void Leg(DrawingContext dc, QuadrupedGait.LegPose pose, bool hind, bool far, bool cat, Brush colour)
        {
            double scale = (far ? .76 : 1) * (cat ? .8 : 1);
            double rootWidth = (hind ? 4.3 : 3.15) * scale;
            double kneeWidth = (hind ? 1.8 : 1.2) * scale;
            double ankleWidth = .65 * scale;
            Point root = At(pose.Root), knee = At(pose.Knee), ankle = At(pose.Ankle);
            Vector upper = knee - root, lower = ankle - knee;
            Vector rootTangent = UnitVector(upper), kneeTangent = UnitVector(ankle - root), ankleTangent = UnitVector(lower);
            Vector rootNormal = Normal(rootTangent), kneeNormal = Normal(kneeTangent), ankleNormal = Normal(ankleTangent);
            Point outerRoot = root + rootNormal * rootWidth, innerRoot = root - rootNormal * rootWidth;
            Point outerKnee = knee + kneeNormal * kneeWidth, innerKnee = knee - kneeNormal * kneeWidth;
            Point outerAnkle = ankle + ankleNormal * ankleWidth, innerAnkle = ankle - ankleNormal * ankleWidth;
            // One continuous contour grows from a broad thigh/shoulder inside
            // the torso, then tapers through the joints into a small paw.
            // The bones guide the pose; their outlines are never drawn as rods.
            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext g = geometry.Open())
            {
                g.BeginFigure(outerRoot, true, true);
                g.BezierTo(outerRoot + upper * .38 + rootNormal * (.2 * rootWidth),
                    outerKnee - kneeTangent * (upper.Length * .25), outerKnee, true, false);
                g.BezierTo(outerKnee + kneeTangent * (lower.Length * .23),
                    outerAnkle - ankleTangent * (lower.Length * .28), outerAnkle, true, false);
                g.QuadraticBezierTo(new Point(pose.Paw.X - .9 * scale, pose.Paw.Y - 1.1),
                    new Point(pose.Paw.X - .65 * scale, pose.Paw.Y - .25), true, false);
                g.QuadraticBezierTo(new Point(pose.Paw.X + .4 * scale, pose.Paw.Y + .1),
                    new Point(pose.Paw.X + 1.6 * scale, pose.Paw.Y - .05), true, false);
                g.QuadraticBezierTo(new Point(pose.Paw.X + 2.0 * scale, pose.Paw.Y - .7),
                    new Point(pose.Paw.X + .6 * scale, pose.Paw.Y - 1.3), true, false);
                g.QuadraticBezierTo(innerAnkle + ankleTangent, innerAnkle, true, false);
                g.BezierTo(innerAnkle - ankleTangent * (lower.Length * .28),
                    innerKnee + kneeTangent * (lower.Length * .23), innerKnee, true, false);
                g.BezierTo(innerKnee - kneeTangent * (upper.Length * .25),
                    innerRoot + upper * .4, innerRoot, true, false);
                g.QuadraticBezierTo(root - rootTangent * (rootWidth * .65), outerRoot, true, false);
            }
            geometry.Freeze(); dc.DrawGeometry(colour, null, geometry);
        }

        private static Vector UnitVector(Vector value) { value.Normalize(); return value; }
        private static Vector Normal(Vector direction) { return new Vector(-direction.Y, direction.X); }

        private static void Paint(DrawingContext dc, Frame frame, Brush colour)
        {
            dc.PushOpacityMask(frame.Mask);
            dc.DrawRectangle(colour, null, frame.Destination);
            dc.Pop();
        }
    }
}
