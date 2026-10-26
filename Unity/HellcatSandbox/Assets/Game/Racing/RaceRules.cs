using System;

namespace Hellcat.Game.Racing
{
    /// <summary>Pure rules shared by the Unity race and command-line regression tests.</summary>
    public static class RaceRules
    {
        public static bool CrossedForward(float previousDistance, float currentDistance) =>
            previousDistance < 0f && currentDistance >= 0f;

        public static bool InsideGate(float lateral, float height, float halfWidth) =>
            Math.Abs(lateral) <= halfWidth && Math.Abs(height) < 6f;

        public static bool DistinctAvailableDevices(int first, int second, int gamepadCount, bool keyboardAvailable) =>
            first != second && Available(first, gamepadCount, keyboardAvailable) && Available(second, gamepadCount, keyboardAvailable);

        static bool Available(int device, int gamepadCount, bool keyboardAvailable) =>
            device >= 0 && (device < 2 ? keyboardAvailable : device - 2 < gamepadCount);

        public static int NextGate(int gate, int count) => (gate + 1) % count;
        public static int CompletedLaps(int gate, int currentLaps) => currentLaps + (gate == 0 ? 1 : 0);
    }
}
