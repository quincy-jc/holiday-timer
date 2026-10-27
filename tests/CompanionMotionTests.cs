using System;
using HolidayTimer;

internal static class CompanionMotionTests
{
    private static int checks;
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    private static void Step(CompanionMotion motion, CompanionKind kind, double seconds, double audio)
    { for (int i = 0; i < (int)Math.Round(seconds * 20); i++) motion.Step(.05, kind, .6, audio, 1); }

    private static void Main()
    {
        double strideDistance = CompanionMotion.Stride / CompanionMotion.StanceFraction * CompanionMotion.ArtScale;
        for (int i = 1; i < 50; i++)
        {
            double phase = i / 100.0, movement = .01 * strideDistance;
            double plantedBefore = CompanionMotion.ArtScale * CompanionMotion.FootX(phase);
            double plantedAfter = movement + CompanionMotion.ArtScale * CompanionMotion.FootX(phase + movement / strideDistance);
            Check(Math.Abs(plantedBefore - plantedAfter) < 1e-8, "A planted foot remains fixed as the body advances");
            Check(CompanionMotion.FootLift(phase) == 0, "A stance foot stays on the ground");
        }
        Check(CompanionMotion.FootLift(.8) > 3, "A swinging paw lifts off the ground");
        Check(Math.Abs(CompanionMotion.FootX(0) - CompanionMotion.FootX(1)) < 1e-8, "The stride loops without a position jump");
        CompanionMotion quiet = new CompanionMotion(); quiet.SetForTest(0, 0);
        Step(quiet, CompanionKind.Dog, 1, 0);
        Check(quiet.Distance > 0 && Math.Abs(quiet.Angle * CompanionMotion.Radius - quiet.Distance) < 1e-8,
            "Travel distance and ring position advance together");
        CompanionMotion music = new CompanionMotion(); music.SetForTest(0, 0);
        Step(music, CompanionKind.Dog, 1, .8);
        Check(music.Distance > quiet.Distance * 1.5, "Audio increases travel and gait speed together");

        CompanionMotion animal = new CompanionMotion();
        Step(animal, CompanionKind.Cat, 4.6, 0);
        Check(animal.Action == CompanionAction.Rest, "A cat stops after travelling");
        double parked = animal.Distance; Step(animal, CompanionKind.Cat, 1, 0);
        Check(Math.Abs(animal.Distance - parked) < 1e-8, "No distance or foot cycling accumulates during rest");
        Step(animal, CompanionKind.Cat, .4 + 4.2, 0);
        Check(animal.Action == CompanionAction.Play, "The animal plays between walks");
        parked = animal.Distance; Step(animal, CompanionKind.Cat, 1, 0);
        Check(Math.Abs(animal.Distance - parked) < 1e-8, "Play stays on the rim instead of gliding");
        Step(animal, CompanionKind.Cat, 1.4 + 5.0, 0);
        Check(animal.Action == CompanionAction.Sleep && Math.Abs(animal.Angle) <= .84, "Sleep happens on the outer rim");
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
