using UnityEngine;

namespace TrafficSim.Core
{
    /// <summary>
    /// Play-mode visibility logs. Filter the Console by <c>[TrafficSim</c>.
    /// Toggle categories from any bootstrap/inspector call site if needed.
    /// </summary>
    public static class SimLog
    {
        public static bool Enabled = true;
        public static bool Bootstrap = true;
        public static bool Map = true;
        public static bool Module = true;
        public static bool Demand = true;
        public static bool Hub = true;
        public static bool Dispatch = true;
        public static bool Economy = true;
        public static bool Rating = true;
        public static bool Eod = true;
        public static bool Overload = true;
        public static bool Phase = true;

        public static void Info(string category, string message)
        {
            if (!Enabled)
                return;

            Debug.Log($"[TrafficSim/{category}] {message}");
        }

        public static void Warn(string category, string message)
        {
            if (!Enabled)
                return;

            Debug.LogWarning($"[TrafficSim/{category}] {message}");
        }

        public static void Error(string category, string message)
        {
            Debug.LogError($"[TrafficSim/{category}] {message}");
        }

        public static void BootstrapInfo(string message)
        {
            if (Bootstrap)
                Info("Bootstrap", message);
        }

        public static void MapInfo(string message)
        {
            if (Map)
                Info("Map", message);
        }

        public static void ModuleInfo(string message)
        {
            if (Module)
                Info("Module", message);
        }

        public static void DemandInfo(string message)
        {
            if (Demand)
                Info("Demand", message);
        }

        public static void HubInfo(string message)
        {
            if (Hub)
                Info("Hub", message);
        }

        public static void DispatchInfo(string message)
        {
            if (Dispatch)
                Info("Dispatch", message);
        }

        public static void EconomyInfo(string message)
        {
            if (Economy)
                Info("Economy", message);
        }

        public static void RatingInfo(string message)
        {
            if (Rating)
                Info("Rating", message);
        }

        public static void EodInfo(string message)
        {
            if (Eod)
                Info("Eod", message);
        }

        public static void OverloadInfo(string message)
        {
            if (Overload)
                Info("Overload", message);
        }

        public static void PhaseInfo(string message)
        {
            if (Phase)
                Info("Phase", message);
        }
    }
}
