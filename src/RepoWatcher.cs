using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace GitGraphLite
{
    // Watches a git dir and raises Changed (debounced) when HEAD or refs move.
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

        public void Watch(string gitDir)
        {
            Stop();
            if (!Directory.Exists(gitDir)) return;
            var w = new FileSystemWatcher(gitDir);
            w.IncludeSubdirectories = true;
            w.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite;
            w.Changed += OnEvent;
            w.Created += OnEvent;
            w.Deleted += OnEvent;
            w.Renamed += OnEvent;
            w.EnableRaisingEvents = true;
            watchers.Add(w);
        }

        void OnEvent(object sender, FileSystemEventArgs e)
        {
            if (IsRefPath(e.Name)) timer.Change(400, Timeout.Infinite);
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
