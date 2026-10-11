using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace GitGraphLite
{
    // Watches a git dir and its worktree and raises Changed (debounced) when HEAD or refs move or worktree files change.
    public sealed class RepoWatcher : IDisposable
    {
        readonly List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();
        readonly Timer timer;
        public event EventHandler Changed;

        public RepoWatcher()
        {
            timer = new Timer(delegate
            {
                var handler = Changed;
                if (handler != null) handler(this, EventArgs.Empty);
            }, null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Watch(string gitDir, string worktree)
        {
            Stop();
            Add(gitDir, e => { if (IsRefPath(e.Name)) timer.Change(400, Timeout.Infinite); });
            // Files edited in the worktree change the uncommitted row; wait longer since editors and builds write in bursts.
            Add(worktree, e => { if (IsWorktreePath(e.Name)) timer.Change(1000, Timeout.Infinite); });
        }

        void Add(string dir, Action<FileSystemEventArgs> onEvent)
        {
            if (!Directory.Exists(dir)) return;
            var w = new FileSystemWatcher(dir);
            w.IncludeSubdirectories = true;
            w.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite;
            FileSystemEventHandler handler = (s, e) => onEvent(e);
            w.Changed += handler;
            w.Created += handler;
            w.Deleted += handler;
            w.Renamed += (s, e) => onEvent(e);
            w.EnableRaisingEvents = true;
            watchers.Add(w);
        }

        // The git dir is watched separately; its index and object writes (including git status's own) must not loop back.
        public static bool IsWorktreePath(string name)
        {
            if (name == null) return false;
            string n = name.Replace('\\', '/');
            return n != ".git" && !n.StartsWith(".git/");
        }

        public static bool IsRefPath(string name)
        {
            if (name == null) return false;
            string n = name.Replace('\\', '/');
            if (n.EndsWith(".lock") || n.StartsWith("objects/") || n.StartsWith("logs/")) return false;
            return n == "HEAD" || n.EndsWith("/HEAD") || n == "packed-refs" || n.StartsWith("refs/");
        }

        void Stop()
        {
            foreach (var w in watchers) w.Dispose();
            watchers.Clear();
        }

        public void Dispose()
        {
            Stop();
            timer.Dispose();
        }
    }
}
