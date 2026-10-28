using System;
using HolidayTimer;

internal static class CompanionMotionTests
{
    private static int checks;
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    private static void Step(CompanionMotion motion, CompanionKind kind, double seconds, double audio)
    { for (int i = 0; i < (int)Math.Round(seconds * 20); i++) motion.Step(.05, kind, .6, audio, 1); }
    private static bool Reach(CompanionMotion motion, CompanionKind kind, CompanionAction target)
    { for (int i = 0; i < 1200; i++) { motion.Step(.05, kind, .6, 0, 1); if (motion.Action == target) return true; } return false; }
    private static double Length(QuadrupedGait.Joint a, QuadrupedGait.Joint b)
    { return Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y)); }
    private static QuadrupedGait.Joint WorldPaw(QuadrupedGait.Joint paw, double cycle)
    {
        double angle = cycle * CompanionMotion.Stride / CompanionMotion.StanceFraction * CompanionMotion.ArtScale / CompanionMotion.Radius;
        double x = (paw.X - 32) * CompanionMotion.ArtScale, y = (paw.Y - 32) * CompanionMotion.ArtScale;
        double radius = CompanionMotion.Radius + 19 * CompanionMotion.ArtScale;
        return new QuadrupedGait.Joint(radius * Math.Sin(angle) + x * Math.Cos(angle) - y * Math.Sin(angle),
            -radius * Math.Cos(angle) + x * Math.Sin(angle) + y * Math.Cos(angle));
    }

    private static int Main()
    {
        try { Run(); return 0; }
        catch (Exception error) { Console.Error.WriteLine("FAIL: " + error.Message); return 1; }
    }
    private static void Run()
    {
        foreach (bool cat in new[] { false, true })
        foreach (bool hind in new[] { false, true })
        foreach (bool far in new[] { false, true })
        {
            for (int i = 0; i <= 128; i++)
            {
                double cycle = i / 128.0, bodyLift = .22 * (1 - Math.Cos(cycle * Math.PI * 4));
                QuadrupedGait.LegPose pose = QuadrupedGait.Pose(cat, hind, far, cycle, bodyLift);
                Check(Math.Abs(Length(pose.Root, pose.Knee) - pose.UpperLength) < 1e-7, "Upper leg keeps its anatomical length");
                Check(Math.Abs(Length(pose.Knee, pose.Ankle) - pose.LowerLength) < 1e-7,
                    "Lower leg keeps its anatomical length: cat=" + cat + " hind=" + hind + " far=" + far + " cycle=" + cycle);
            }
            double offset = hind ? (far ? .5 : 0) : (far ? .25 : .75);
            QuadrupedGait.Joint first = WorldPaw(QuadrupedGait.Pose(cat, hind, far, .05 - offset, 0).Paw, .05 - offset);
            for (int i = 1; i < 100; i++)
            {
                double cycle = .05 + i * .005 - offset;
                QuadrupedGait.Joint paw = WorldPaw(QuadrupedGait.Pose(cat, hind, far, cycle, 0).Paw, cycle);
                Check(Length(first, paw) < 1e-7, "Every planted paw stays fixed on the circular rim as the body moves");
            }
            QuadrupedGait.LegPose end = QuadrupedGait.Pose(cat, hind, far, 0, 0), loop = QuadrupedGait.Pose(cat, hind, far, 1, 0);
            Check(Length(end.Paw, loop.Paw) < 1e-8 && Length(end.Knee, loop.Knee) < 1e-8, "Each leg loops continuously");
            QuadrupedGait.LegPose standing = QuadrupedGait.Pose(cat, hind, far, .8, 0, 1);
            Check(standing.Paw.Y >= 51, "Rest finishes with every paw on the rim");
        }
        CompanionMotion quiet = new CompanionMotion(); quiet.SetForTest(0, 0);
        Step(quiet, CompanionKind.Dog, 1, 0);
        Check(quiet.Distance > 0 && Math.Abs(quiet.Angle * CompanionMotion.Radius - quiet.Distance) < 1e-8,
            "Travel distance and ring position advance together");
        CompanionMotion music = new CompanionMotion(); music.SetForTest(0, 0);
        Step(music, CompanionKind.Dog, 1, .8);
        Check(music.Distance > quiet.Distance * 1.15 && music.Distance < quiet.Distance * 1.4, "Audio gently increases travel and gait speed together");

        CompanionMotion animal = new CompanionMotion();
        Check(Reach(animal, CompanionKind.Cat, CompanionAction.Rest), "A cat stops after travelling");
        double parked = animal.Distance; Step(animal, CompanionKind.Cat, 2, 0);
        Check(Math.Abs(animal.Distance - parked) < 1e-8, "No distance or foot cycling accumulates during rest");
        Check(Reach(animal, CompanionKind.Cat, CompanionAction.Play), "The animal plays between walks");
        parked = animal.Distance; Step(animal, CompanionKind.Cat, 1, 0);
        Check(Math.Abs(animal.Distance - parked) < 1e-8, "Play stays on the rim instead of gliding");
        Check(Reach(animal, CompanionKind.Cat, CompanionAction.Sleep) && Math.Abs(animal.Angle) <= .84, "Sleep happens on the outer rim");
        parked = animal.Distance; Step(animal, CompanionKind.Cat, 2, 0);
        Check(Math.Abs(animal.Distance - parked) < 1e-8, "A sleeping animal remains in place");
        animal.Interact(); Check(animal.Action == CompanionAction.Play, "A click wakes a sleeping animal into play");
        Step(animal, CompanionKind.Cat, 1.8, 0);
        Check(animal.Action == CompanionAction.Travel, "A click reaction returns to travel");

        CompanionMotion car = new CompanionMotion(); bool sawPark = false, sawPlay = false, sawReverse = false;
        int lastDirection = car.Direction;
        for (int i = 0; i < 1200; i++)
        {
            car.Step(.05, CompanionKind.Car, .6, 0, 1);
            Check(car.Action != CompanionAction.Sleep, "Vehicles park rather than sleep");
            Check(Math.Abs(car.Angle) <= .840001, "Vehicles stay on the upper rim");
            sawPark |= car.Action == CompanionAction.Rest; sawPlay |= car.Action == CompanionAction.Play;
            sawReverse |= car.Direction != lastDirection; lastDirection = car.Direction;
        }
        Check(sawPark && sawPlay && sawReverse, "Vehicles park, play and reverse direction");
        Console.WriteLine("PASS: " + checks + " companion motion checks.");
    }
}
