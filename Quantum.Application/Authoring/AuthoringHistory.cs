using System;
using System.Collections.Generic;
using System.Linq;

namespace Quantum.Application.Authoring
{
    public sealed class AuthoringHistoryEntry
    {
        internal AuthoringHistoryEntry(
            string description,
            PreparedTrackGraphState beforeState,
            PreparedTrackGraphState afterState)
        {
            Description = string.IsNullOrWhiteSpace(description)
                ? throw new ArgumentException("A history description is required.", nameof(description))
                : description;
            BeforeState = beforeState ?? throw new ArgumentNullException(nameof(beforeState));
            AfterState = afterState ?? throw new ArgumentNullException(nameof(afterState));
        }

        public string Description { get; }

        public PreparedTrackGraphState BeforeState { get; }

        public PreparedTrackGraphState AfterState { get; }

        public long RetainedPackageByteCount =>
            (long)BeforeState.RetainedPackageByteCount + AfterState.RetainedPackageByteCount;
    }

    public sealed class AuthoringHistory
    {
        private readonly Stack<AuthoringHistoryEntry> undo =
            new Stack<AuthoringHistoryEntry>();
        private readonly Stack<AuthoringHistoryEntry> redo =
            new Stack<AuthoringHistoryEntry>();

        public int UndoCount => undo.Count;

        public int RedoCount => redo.Count;

        public bool CanUndo => undo.Count != 0;

        public bool CanRedo => redo.Count != 0;

        public string? UndoDescription => CanUndo ? undo.Peek().Description : null;

        public string? RedoDescription => CanRedo ? redo.Peek().Description : null;

        /// <summary>
        /// Sum of retained canonical JSON byte sizes. Object-graph memory is not
        /// estimated and shared states may be counted more than once.
        /// </summary>
        public long RetainedPackageByteCount =>
            undo.Sum(entry => entry.RetainedPackageByteCount) +
            redo.Sum(entry => entry.RetainedPackageByteCount);

        internal void Record(AuthoringHistoryEntry entry)
        {
            if (entry is null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            undo.Push(entry);
            redo.Clear();
        }

        internal bool TryUndo(out AuthoringHistoryEntry? entry)
        {
            if (!CanUndo)
            {
                entry = null;
                return false;
            }

            entry = undo.Pop();
            redo.Push(entry);
            return true;
        }

        internal bool TryRedo(out AuthoringHistoryEntry? entry)
        {
            if (!CanRedo)
            {
                entry = null;
                return false;
            }

            entry = redo.Pop();
            undo.Push(entry);
            return true;
        }

        internal void Clear()
        {
            undo.Clear();
            redo.Clear();
        }
    }
}
