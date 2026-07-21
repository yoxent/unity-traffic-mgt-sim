using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityMcpPro
{
    public class UndoCommands : BaseCommand
    {
        private static List<string> _undoHistory = new List<string>();
        private static bool _listenerRegistered = false;
        private const int MaxHistorySize = 50;

        public static void Register(CommandRouter router)
        {
            router.Register("get_undo_history", GetUndoHistory);
            router.Register("perform_undo", PerformUndo);
            router.Register("perform_redo", PerformRedo);

            EnsureListener();
        }

        private static void EnsureListener()
        {
            if (_listenerRegistered) return;
            _listenerRegistered = true;
            Undo.undoRedoEvent += OnUndoRedoPerformed;
            Undo.postprocessModifications += OnPostprocessModifications;
        }

        private static void OnUndoRedoPerformed(in UndoRedoInfo info)
        {
            string entry = info.undoName;
            if (!string.IsNullOrEmpty(entry))
            {
                _undoHistory.Add($"[{(info.isRedo ? "Redo" : "Undo")}] {entry}");
                if (_undoHistory.Count > MaxHistorySize)
                    _undoHistory.RemoveAt(0);
            }
        }

        private static UndoPropertyModification[] OnPostprocessModifications(UndoPropertyModification[] modifications)
        {
            if (modifications.Length > 0)
            {
                string groupName = Undo.GetCurrentGroupName();
                if (!string.IsNullOrEmpty(groupName))
                {
                    string entry = $"[Modify] {groupName}";
                    if (_undoHistory.Count == 0 || _undoHistory[_undoHistory.Count - 1] != entry)
                    {
                        _undoHistory.Add(entry);
                        if (_undoHistory.Count > MaxHistorySize)
                            _undoHistory.RemoveAt(0);
                    }
                }
            }
            return modifications;
        }

        private static object GetUndoHistory(Dictionary<string, object> p)
        {
            string currentGroup = Undo.GetCurrentGroupName();
            int currentGroupId = Undo.GetCurrentGroup();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "current_group_name", currentGroup ?? "" },
                { "current_group_id", currentGroupId },
                { "history_count", _undoHistory.Count },
                { "recent_operations", _undoHistory.ToArray() }
            };
        }

        private static object PerformUndo(Dictionary<string, object> p)
        {
            int count = GetIntParam(p, "count", 1);
            if (count < 1) count = 1;
            if (count > 100) count = 100;

            var performed = new List<string>();
            for (int i = 0; i < count; i++)
            {
                string beforeGroup = Undo.GetCurrentGroupName();
                Undo.PerformUndo();
                string afterGroup = Undo.GetCurrentGroupName();
                performed.Add(beforeGroup ?? "(unknown)");
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "operations_performed", count },
                { "undone", performed.ToArray() },
                { "current_group", Undo.GetCurrentGroupName() ?? "" }
            };
        }

        private static object PerformRedo(Dictionary<string, object> p)
        {
            int count = GetIntParam(p, "count", 1);
            if (count < 1) count = 1;
            if (count > 100) count = 100;

            var performed = new List<string>();
            for (int i = 0; i < count; i++)
            {
                Undo.PerformRedo();
                string group = Undo.GetCurrentGroupName();
                performed.Add(group ?? "(unknown)");
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "operations_performed", count },
                { "redone", performed.ToArray() },
                { "current_group", Undo.GetCurrentGroupName() ?? "" }
            };
        }
    }
}
