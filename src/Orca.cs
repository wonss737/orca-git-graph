using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace GitGraphLite
{
    // Follows the worktree that Orca is showing. Orca keeps it in its profile state
    // (SQLite, %APPDATA%\orca\profiles\<profile>\profile-state.db) as workspaceSession.activeWorktreeId,
    // e.g. "<repo id>::C:/Projects/x". This is Orca's private format, so any failure just means "no answer".
    public sealed class OrcaFollower : IDisposable
    {
        const int PollMs = 1000;
        readonly Timer timer;
        string last;
        bool disposed;

        // Raised on a worker thread with the new local folder Orca switched to.
        public event Action<string> Changed;

        public OrcaFollower()
        {
            timer = new Timer(delegate { Poll(); }, null, Timeout.Infinite, Timeout.Infinite);
        }

        // Returns the folder Orca shows right now (or null) and starts watching for changes to it.
        public string Start()
        {
            last = ActiveFolder();
            timer.Change(PollMs, Timeout.Infinite);
            return last;
        }

        void Poll()
        {
            string now = ActiveFolder();
            if (now != null && !string.Equals(now, last, StringComparison.OrdinalIgnoreCase))
            {
                last = now;
                var handler = Changed;
                if (handler != null) handler(now);
            }
            lock (timer) if (!disposed) timer.Change(PollMs, Timeout.Infinite);
        }

        public static string ActiveFolder()
        {
            try
            {
                string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "orca");
                string index = Path.Combine(root, "orca-profile-index.json");
                string profile = File.Exists(index) ? ParseActiveProfile(File.ReadAllText(index)) : null;
                string db = Path.Combine(root, "profiles", profile ?? "local-default", "profile-state.db");
                if (!File.Exists(db)) return null;
                string path = ParseActiveWorktree(Sqlite.ReadText(db,
                    "select payload from profile_state_documents where domain = 'workspaceSession'"));
                if (path == null) return null;
                path = Path.GetFullPath(path);
                return Directory.Exists(path) ? path : null;
            }
            catch (Exception) { return null; }
        }

        public static string ParseActiveProfile(string json)
        {
            return JsonString(json, "activeProfileId");
        }

        // The worktree id is "<repo id>::<path>"; the path is what we want.
        public static string ParseActiveWorktree(string json)
        {
            string id = JsonString(json, "activeWorktreeId");
            if (id == null) return null;
            int sep = id.IndexOf("::", StringComparison.Ordinal);
            string path = sep >= 0 ? id.Substring(sep + 2) : null;
            return string.IsNullOrEmpty(path) ? null : path;
        }

        static string JsonString(string json, string key)
        {
            if (json == null) return null;
            var m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            return m.Success ? Regex.Unescape(m.Groups[1].Value) : null;
        }

        public void Dispose()
        {
            lock (timer)
            {
                disposed = true;
                timer.Dispose();
            }
        }
    }

    // Read-only access through the SQLite that ships with Windows (winsqlite3.dll).
    static class Sqlite
    {
        const int OpenReadOnly = 1, Row = 100;

        [DllImport("winsqlite3.dll")] static extern int sqlite3_open_v2(byte[] file, out IntPtr db, int flags, IntPtr vfs);
        [DllImport("winsqlite3.dll")] static extern int sqlite3_busy_timeout(IntPtr db, int ms);
        [DllImport("winsqlite3.dll")] static extern int sqlite3_prepare_v2(IntPtr db, byte[] sql, int bytes, out IntPtr stmt, IntPtr tail);
        [DllImport("winsqlite3.dll")] static extern int sqlite3_step(IntPtr stmt);
        [DllImport("winsqlite3.dll")] static extern IntPtr sqlite3_column_text(IntPtr stmt, int col);
        [DllImport("winsqlite3.dll")] static extern int sqlite3_column_bytes(IntPtr stmt, int col);
        [DllImport("winsqlite3.dll")] static extern int sqlite3_finalize(IntPtr stmt);
        [DllImport("winsqlite3.dll")] static extern int sqlite3_close(IntPtr db);

        static byte[] Utf8z(string s) { return Encoding.UTF8.GetBytes(s + "\0"); }

        // First column of the first row as text, or null.
        public static string ReadText(string file, string sql)
        {
            IntPtr db, stmt = IntPtr.Zero;
            if (sqlite3_open_v2(Utf8z(file), out db, OpenReadOnly, IntPtr.Zero) != 0) { sqlite3_close(db); return null; }
            try
            {
                sqlite3_busy_timeout(db, 200);
                if (sqlite3_prepare_v2(db, Utf8z(sql), -1, out stmt, IntPtr.Zero) != 0 || sqlite3_step(stmt) != Row) return null;
                IntPtr text = sqlite3_column_text(stmt, 0);
                if (text == IntPtr.Zero) return null;
                var bytes = new byte[sqlite3_column_bytes(stmt, 0)];
                Marshal.Copy(text, bytes, 0, bytes.Length);
                return Encoding.UTF8.GetString(bytes);
            }
            finally
            {
                if (stmt != IntPtr.Zero) sqlite3_finalize(stmt);
                sqlite3_close(db);
            }
        }
    }
}
