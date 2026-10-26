using System;
using Hellcat.Game.Core;
using Hellcat.Game.Views;
using Hellcat.Sim.Vehicle;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hellcat.Game.Racing
{
    /// <summary>Two drivers in ONE solver world, with independent devices and vertical split cameras.</summary>
    [DefaultExecutionOrder(-150)]
    public class SplitScreenRace : MonoBehaviour
    {
        public SimRunner runner;
        public SoftBodyCarView firstView, secondView;
        public Camera[] cameras = new Camera[2];
        public Vector3[] checkpoints;
        public int lapsToWin = 3;
        public float gateHalfWidth = 10f;
        public int SecondBody { get; private set; }
        readonly int[] devices = { 0, 1 }; // 0 WASD, 1 arrows, 2+ explicit connected gamepad
        readonly int[] assignedPadId = { -1, -1 };
        readonly int[] next = { 1, 1 }, laps = { 0, 0 };
        readonly float[] finish = { -1f, -1f }, reverseTimer = { 0f, 0f };
        readonly bool[] reverse = { false, false };
        readonly Vector3[] previous = new Vector3[2];
        bool setup = true, paused;
        float countdown = 3f, elapsed;
        int winner = -1;
        GUIStyle text;

        public void AddSecondCar(SimRunner source, Vector3 position, Quaternion rotation)
        {
            runner = source;
            SecondBody = source.World.AddBody("hellcat-player-2", source.VehicleDefinition,
                source.VehicleParams, position + rotation * new Vector3(5f, 0f, 0f), rotation, float3.zero, true);
            source.World.bodies[SecondBody].spawnClearance = source.spawnClearance;
            if (secondView != null) secondView.bodyOverride = SecondBody;
        }

        void Start()
        {
            runner.paused = true;
            if (checkpoints == null || checkpoints.Length < 4)
                throw new InvalidOperationException("Race needs at least four ordered checkpoint gates.");
            runner.World.SetInput(runner.World.bodies[SecondBody].vehicleIndex,
                new DriverInput { gear = 0, brake = 1f, handbrake = 1f });
            runner.World.Settle(SecondBody);
            for (int i = 0; i < 2; i++)
            {
                cameras[i].rect = new Rect(i * 0.5f, 0f, 0.5f, 1f);
                previous[i] = Position(i);
            }
        }

        int Body(int player) => player == 0 ? runner.VehicleBody : SecondBody;
        int Vehicle(int player) => runner.World.bodies[Body(player)].vehicleIndex;
        Vector3 Position(int player)
        {
            return (Vector3)(float3)runner.World.Metrics(Body(player), false).centerOfMass;
        }

        bool DevicesValid()
        {
            if (!RaceRules.DistinctAvailableDevices(devices[0], devices[1], Gamepad.all.Count, Keyboard.current != null)) return false;
            for (int i = 0; i < 2; i++)
                if (devices[i] >= 2 && (devices[i] - 2 >= Gamepad.all.Count ||
                    (!setup && assignedPadId[i] != Gamepad.all[devices[i] - 2].deviceId))) return false;
            return true;
        }

        void Update()
        {
            if (runner == null || runner.World == null) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && !setup)
                paused = !paused;
            if (!setup && !DevicesValid()) paused = true;
            runner.paused = setup || paused || winner >= 0;
            if (runner.paused) return;
            for (int i = 0; i < 2; i++)
            {
                var kb = Keyboard.current;
                bool repair = devices[i] == 0 && kb != null && kb.rKey.wasPressedThisFrame ||
                    devices[i] == 1 && kb != null && kb.numpad0Key.wasPressedThisFrame;
                if (devices[i] >= 2 && devices[i] - 2 < Gamepad.all.Count)
                    repair |= Gamepad.all[devices[i] - 2].selectButton.wasPressedThisFrame;
                if (repair && countdown <= 0f) Recover(i);
            }
            // Use simulated time: race timers stay fair when the solver slows down.
            float now = (float)runner.SimTime;
            float dt = Mathf.Max(0f, now - lastSimTime);
            lastSimTime = now;
            if (countdown > 0f) countdown = Mathf.Max(0f, countdown - dt);
            else
            {
                elapsed += dt;
                for (int i = 0; i < 2; i++) CheckGate(i);
            }
        }
        float lastSimTime;

        void CheckGate(int player)
        {
            Vector3 p = Position(player);
            int n = next[player];
            Vector3 centre = checkpoints[n];
            Vector3 direction = (centre - checkpoints[(n + checkpoints.Length - 1) % checkpoints.Length]).normalized;
            float a = Vector3.Dot(previous[player] - centre, direction);
            float b = Vector3.Dot(p - centre, direction);
            if (RaceRules.CrossedForward(a, b))
            {
                Vector3 hit = Vector3.Lerp(previous[player], p, -a / (b - a));
                Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
                if (RaceRules.InsideGate(Vector3.Dot(hit - centre, side), hit.y - centre.y, gateHalfWidth))
                {
                    laps[player] = RaceRules.CompletedLaps(n, laps[player]);
                    if (n == 0 && laps[player] >= lapsToWin)
                    {
                        finish[player] = elapsed;
                        if (winner < 0) winner = player;
                    }
                    next[player] = RaceRules.NextGate(n, checkpoints.Length);
                }
            }
            previous[player] = p;
        }

        public void WriteInputs(int substeps)
        {
            for (int i = 0; i < 2; i++)
            {
                var input = setup || paused || countdown > 0f || winner >= 0
                    ? new DriverInput { brake = 1f, handbrake = 1f, gear = 0 }
                    : ReadDriver(i, substeps);
                for (int step = 0; step < substeps; step++) runner.World.SetInput(Vehicle(i), step, input);
            }
        }

        DriverInput ReadDriver(int player, int substeps)
        {
            float throttle = 0f, brake = 0f, steer = 0f, handbrake = 0f;
            var kb = Keyboard.current;
            int device = devices[player];
            if (device < 2 && kb != null)
            {
                bool first = device == 0;
                throttle = (first ? kb.wKey : kb.upArrowKey).isPressed ? 1f : 0f;
                brake = (first ? kb.sKey : kb.downArrowKey).isPressed ? 1f : 0f;
                steer = ((first ? kb.dKey : kb.rightArrowKey).isPressed ? 1f : 0f)
                    - ((first ? kb.aKey : kb.leftArrowKey).isPressed ? 1f : 0f);
                handbrake = (first ? kb.spaceKey : kb.rightCtrlKey).isPressed ? 1f : 0f;
            }
            else if (device >= 2 && device - 2 < Gamepad.all.Count)
            {
                var pad = Gamepad.all[device - 2];
                throttle = pad.rightTrigger.ReadValue(); brake = pad.leftTrigger.ReadValue();
                steer = pad.leftStick.x.ReadValue(); handbrake = pad.buttonEast.isPressed ? 1f : 0f;
            }
            float speed = runner.World.vehicleState[Vehicle(player)].speed;
            bool change = Mathf.Abs(speed) < 0.8f && (reverse[player] ? throttle > 0.5f && brake < 0.1f : brake > 0.5f && throttle < 0.1f);
            reverseTimer[player] = change ? reverseTimer[player] + substeps * runner.World.dt : 0f;
            if (reverseTimer[player] >= 0.3f) { reverse[player] = !reverse[player]; reverseTimer[player] = 0f; }
            return new DriverInput { throttle = reverse[player] ? brake : throttle,
                brake = reverse[player] ? throttle : brake, steer = steer, handbrake = handbrake,
                gear = reverse[player] ? -1 : DriverInput.Drive, flags = DriverInput.FlagAbs | DriverInput.FlagTc };
        }

        void LateUpdate()
        {
            if (runner == null || runner.World == null) return;
            for (int i = 0; i < 2; i++)
            {
                Vector3 centre = Position(i);
                Vector3 forward = (Vector3)runner.World.vehicleState[Vehicle(i)].forward;
                forward.y = 0f; forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
                Vector3 target = centre - forward * 8f + Vector3.up * 3.5f;
                cameras[i].transform.position = Vector3.Lerp(cameras[i].transform.position, target, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
                cameras[i].transform.LookAt(centre + forward * 4f + Vector3.up);
            }
        }

        string DeviceName(int index) => index == 0 ? "Keyboard WASD + Space" : index == 1 ? "Keyboard arrows + Right Ctrl"
            : index - 2 < Gamepad.all.Count ? "Gamepad " + (index - 1) + ": " + Gamepad.all[index - 2].displayName : "Disconnected gamepad";

        void Recover(int player)
        {
            int gate = (next[player] + checkpoints.Length - 1) % checkpoints.Length;
            Vector3 heading = (checkpoints[next[player]] - checkpoints[gate]).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, heading);
            Vector3 position = checkpoints[gate] + heading * 3f + right * (player == 0 ? -3f : 3f) + Vector3.up;
            runner.World.Respawn(Body(player), position, Quaternion.LookRotation(heading), float3.zero);
            runner.World.SetInput(Vehicle(player), new DriverInput { brake = 1, handbrake = 1, gear = 0 });
            runner.World.Settle(Body(player));
            previous[player] = Position(player);
            reverse[player] = false; reverseTimer[player] = 0;
            (player == 0 ? firstView : secondView)?.ResetVisualState();
        }

        void Restart()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        void OnGUI()
        {
            Hellcat.Game.UI.EmbeddedUIFont.Apply();
            text ??= new GUIStyle(GUI.skin.label) { fontSize = 20, normal = { textColor = Color.white } };
            GUI.DrawTexture(new Rect(Screen.width / 2f - 2f, 0f, 4f, Screen.height), Texture2D.whiteTexture);
            if (setup || paused || winner >= 0)
            {
                GUILayout.BeginArea(new Rect(Screen.width / 2f - 260, 35, 520, Screen.height - 70), GUI.skin.box);
                GUILayout.Label(winner >= 0 ? $"PLAYER {winner + 1} WINS!  {finish[winner]:F2} s" : "CRASH CIRCUIT - LOCAL TWO PLAYER", text);
                if (setup || paused)
                {
                    // Paused device changes re-enter setup so gamepad identities can be reassigned.
                    for (int i = 0; i < 2; i++)
                    {
                        GUILayout.Label($"Player {i + 1}: {DeviceName(devices[i])}", text);
                        if (GUILayout.Button("Change player " + (i + 1) + " device")) { devices[i] = (devices[i] + 1) % (2 + Gamepad.all.Count); setup = true; }
                    }
                    GUILayout.Label("3 laps. Pass every gate in order. Ramps and slalom.\nRepair: WASD R / arrows Num0 / gamepad Back.");
                    GUILayout.Label("Esc: pause. Duplicate devices are not allowed.");
                    GUI.enabled = DevicesValid();
                    if (GUILayout.Button(setup ? "START RACE" : "RESUME")) {
                        for (int i = 0; i < 2; i++) assignedPadId[i] = devices[i] >= 2 ? Gamepad.all[devices[i] - 2].deviceId : -1;
                        setup = false; paused = false; lastSimTime = (float)runner.SimTime;
                    }
                    GUI.enabled = true;
                }
                if (GUILayout.Button("Restart / choose devices")) Restart();
                if (Application.CanStreamedLevelBeLoaded("TestMap") && GUILayout.Button("Back to sandbox"))
                    UnityEngine.SceneManagement.SceneManager.LoadScene("TestMap");
                if (GUILayout.Button("Quit")) Application.Quit();
                GUILayout.EndArea();
                return;
            }
            for (int i = 0; i < 2; i++)
            {
                float x = i * Screen.width / 2f + 16f;
                GUI.Label(new Rect(x, 15, Screen.width / 2f - 25, 80),
                    $"PLAYER {i + 1}  |  LAP {Mathf.Min(laps[i] + 1, lapsToWin)}/{lapsToWin}\n" +
                    $"{Mathf.Abs(runner.World.vehicleState[Vehicle(i)].speed) * 3.6f:F0} km/h   {elapsed:F1} s   Gate {next[i] + 1}", text);
                if (countdown > 0f) GUI.Label(new Rect(x + 100, 130, 220, 60), $"START IN {Mathf.CeilToInt(countdown)}", text);
            }
        }
    }
}
