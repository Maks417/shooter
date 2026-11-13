using System;
using System.Security.Cryptography;
using System.Text;

namespace Shooter.Bootstrap
{
    /// <summary>First executable B01-materials route: seven six-second fixed-tick camera cuts.</summary>
    public static class MaterialBenchmarkRecipe
    {
        public const string Workload = "B01-materials-v1";
        public const int ViewTicks = 360, CycleTicks = 2520;
        // Sorted canonical ASCII JSON; hash identifies the complete repeated camera recipe, not its last position.
        public const string RouteJson = "{\"cameraHeightMm\":1650,\"cameraZMm\":-6000,\"cycleTicks\":2520,\"firstXMm\":-6000,\"interpolation\":\"hold-cut\",\"pitchMilliDegrees\":0,\"stepXMm\":2000,\"viewTicks\":360,\"views\":7,\"yawMilliDegrees\":0}";
        public static int ViewIndex(uint completedTicks) => (int)(completedTicks % CycleTicks) / ViewTicks;
        public static float CameraX(uint completedTicks) => -6 + 2 * ViewIndex(completedTicks);
        public static string RouteHash
        {
            get
            {
                using (var sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(Encoding.ASCII.GetBytes(RouteJson));
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }
    }
}
