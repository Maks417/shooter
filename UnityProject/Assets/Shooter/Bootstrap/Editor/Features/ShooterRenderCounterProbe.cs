using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

/// <summary>Native descriptor inventory only; Editor availability cannot establish release counter availability.</summary>
public static class ShooterRenderCounterProbe
{
    [Serializable] public sealed class Counter { public string name, category, unit, dataType, flags; }
    [Serializable] public sealed class Proof
    {
        public string result = "Verified", scope = "Locked Editor native profiler identity inventory only; release sample coverage remains a separate check";
        public string renderCategory, memoryCategory, toolchainHash;
        public Counter[] counters;
    }
    public static void Inventory()
    {
        try
        {
            ShooterBuild.CheckToolchain();
            var handles = new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
            var descriptions = handles.Select(ProfilerRecorderHandle.GetDescription).Where(d =>
                d.Name == "Draw Calls Count" || d.Name == "Triangles Count" || d.Name == "Gfx Used Memory");
            ShooterBuild.WriteJson(ShooterBuild.Argument("-shooterReport"), new Proof { renderCategory = ProfilerCategory.Render.Name,
                memoryCategory = ProfilerCategory.Memory.Name, toolchainHash = ShooterBuild.HashFile(ShooterBuild.LockPath),
                counters = descriptions.Select(d => new Counter { name = d.Name, category = d.Category.Name, unit = d.UnitType.ToString(),
                    dataType = d.DataType.ToString(), flags = d.Flags.ToString() }).ToArray() });
        }
        catch (Exception error) { Debug.LogError("SHOOTER_FAILURE: " + error); EditorApplication.Exit(1); }
    }
}
