using System;
using Hellcat.Game.Racing;
class RaceRulesTests
{
    static int passed;
    static void Check(bool value, string name)
    {
        if (!value) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name); passed++;
    }
    static void Main()
    {
        Check(RaceRules.CrossedForward(-1, 1), "forward crossing");
        Check(!RaceRules.CrossedForward(1, -1), "backwards crossing rejected");
        Check(!RaceRules.CrossedForward(0, 1), "standing on gate cannot double count");
        Check(!RaceRules.CrossedForward(-1, -0.1f), "approach is not crossing");
        Check(RaceRules.InsideGate(9.9f, 2, 10), "inside gate");
        Check(!RaceRules.InsideGate(11, 0, 10), "outside width rejected");
        Check(!RaceRules.InsideGate(0, 7, 10), "above gate rejected");
        Check(RaceRules.DistinctAvailableDevices(0, 1, 0, true), "two keyboard layouts");
        Check(RaceRules.DistinctAvailableDevices(0, 2, 1, true), "keyboard and gamepad");
        Check(RaceRules.DistinctAvailableDevices(2, 3, 2, false), "two gamepads");
        Check(!RaceRules.DistinctAvailableDevices(2, 2, 2, true), "shared gamepad rejected");
        Check(!RaceRules.DistinctAvailableDevices(0, 0, 0, true), "shared keyboard layout rejected");
        Check(!RaceRules.DistinctAvailableDevices(0, 3, 1, true), "missing gamepad rejected");
        Check(!RaceRules.DistinctAvailableDevices(0, 1, 0, false), "missing keyboard rejected");
        int next = 1, laps = 0;
        for (int lap = 0; lap < 3; lap++)
        {
            for (int i = 0; i < 16; i++)
            {
                laps = RaceRules.CompletedLaps(next, laps);
                next = RaceRules.NextGate(next, 16);
            }
            Check(laps == lap + 1 && next == 1, "complete ordered lap " + (lap + 1));
        }
        Check(RaceRules.CompletedLaps(5, 1) == 1, "intermediate gate does not add lap");
        Console.WriteLine(passed + " checks passed");
    }
}
