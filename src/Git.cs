using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace GitGraphLite
{
    public enum RefKind { Local, Remote, Tag, Head }

    public sealed class GitRef
    {
        public string Name;
        public RefKind Kind;
        public bool Current; // the branch HEAD points to
    }

    public sealed class Commit
    {
        public string Hash;
        public string[] Parents;
        public List<GitRef> Refs;
        public string Date;
        public string Author;
        public string Subject;
        public bool Uncommitted; // the pseudo-commit for working tree changes
    }

    public sealed class RepoInfo
    {
        public string Root;
        public string CommonDir;
    }

    public static class Git
    {
        const char Sep = '\x1f';
        const string Format = "--format=%H%x1f%P%x1f%D%x1f%ad%x1f%an%x1f%s";

        public static string Run(string dir, params string[] args)
        {
            var psi = new ProcessStartInfo("git", BuildArgs(dir, args));
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;
            using (var p = Process.Start(psi))
            {
                var err = p.StandardError.ReadToEndAsync();
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode != 0) throw new InvalidOperationException(err.Result.Trim());
                return output;
            }
        }

        static string BuildArgs(string dir, string[] args)
        {
            var parts = new List<string> { "-C", dir, "-c", "core.quotepath=false", "-c", "i18n.logOutputEncoding=UTF-8", "--no-pager" };
            parts.AddRange(args);
            var sb = new StringBuilder();
            foreach (string a in parts)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(Quote(a));
            }
            return sb.ToString();
        }

        // Quotes one argument by the MSVCRT command-line rules.
        public static string Quote(string a)
        {
            if (a.Length > 0 && a.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return a;
            var sb = new StringBuilder("\"");
            int slashes = 0;
            foreach (char c in a)
            {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') { sb.Append('\\', slashes * 2 + 1); sb.Append('"'); }
                else { sb.Append('\\', slashes); sb.Append(c); }
                slashes = 0;
            }
            sb.Append('\\', slashes * 2);
            sb.Append('"');
            return sb.ToString();
        }

        public static RepoInfo Resolve(string path)
        {
            string root = Run(path, "rev-parse", "--show-toplevel").Trim();
            string common = Run(path, "rev-parse", "--path-format=absolute", "--git-common-dir").Trim();
            return new RepoInfo { Root = Path.GetFullPath(root), CommonDir = Path.GetFullPath(common) };
        }

        public static List<Commit> Log(string dir, int limit, bool all)
        {
            var args = new List<string> { "log", "--topo-order", "-n" + limit, "--date=format:%Y-%m-%d %H:%M", Format };
            if (all) args.Insert(1, "--all");
            var remotes = Run(dir, "remote").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return ParseLog(Run(dir, args.ToArray()), new HashSet<string>(remotes));
        }

        // A pseudo-commit on top of HEAD for staged, unstaged and untracked changes, or null when the worktree is clean.
        public static Commit Uncommitted(string dir)
        {
            int count = Run(dir, "--no-optional-locks", "status", "--porcelain")
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
            return count == 0 ? null : UncommittedCommit(count, Head(dir));
        }

        public static Commit UncommittedCommit(int files, string head)
        {
            return new Commit
            {
                Hash = "*",
                Parents = head == null ? new string[0] : new[] { head },
                Refs = new List<GitRef>(),
                Date = "",
                Author = "",
                Subject = "Uncommitted changes (" + files + (files == 1 ? " file)" : " files)"),
                Uncommitted = true,
            };
        }

        // The commit HEAD points to, or null on an unborn branch.
        static string Head(string dir)
        {
            try { return Run(dir, "rev-parse", "--verify", "-q", "HEAD").Trim(); }
            catch (InvalidOperationException) { return null; }
        }

        public static string ShowUncommitted(string dir)
        {
            string text = Run(dir, "--no-optional-locks", "status", "--short", "--branch");
            if (Head(dir) != null) text += "\n" + Run(dir, "--no-optional-locks", "diff", "--stat=200", "--no-color", "--no-ext-diff", "HEAD");
            return text;
        }

        public static string CurrentBranch(string dir)
        {
            return Run(dir, "rev-parse", "--abbrev-ref", "HEAD").Trim();
        }

        public static string Show(string dir, string hash)
        {
            return Run(dir, "show", "--stat=200", "--format=fuller", "--no-color", "--no-ext-diff", hash);
        }

        public static List<Commit> ParseLog(string text, ICollection<string> remotes)
        {
            var list = new List<Commit>();
            foreach (string raw in text.Split('\n'))
            {
                string[] f = raw.TrimEnd('\r').Split(Sep);
                if (f.Length < 6) continue;
                list.Add(new Commit
                {
                    Hash = f[0],
                    Parents = f[1].Length == 0 ? new string[0] : f[1].Split(' '),
                    Refs = ParseRefs(f[2], remotes),
                    Date = f[3],
                    Author = f[4],
                    Subject = string.Join(Sep.ToString(), f, 5, f.Length - 5),
                });
            }
            return list;
        }

        public static List<GitRef> ParseRefs(string text, ICollection<string> remotes)
        {
            var list = new List<GitRef>();
            foreach (string raw in text.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries))
            {
                string r = raw.Trim();
                if (r == "HEAD") { list.Add(new GitRef { Name = "HEAD", Kind = RefKind.Head }); continue; }
                bool current = r.StartsWith("HEAD -> ");
                if (current) r = r.Substring(8);
                if (r.StartsWith("tag: ")) list.Add(new GitRef { Name = r.Substring(5), Kind = RefKind.Tag });
                else list.Add(new GitRef { Name = r, Kind = IsRemote(r, remotes) ? RefKind.Remote : RefKind.Local, Current = current });
            }
            return list;
        }

        static bool IsRemote(string name, ICollection<string> remotes)
        {
            int slash = name.IndexOf('/');
            return slash > 0 && remotes.Contains(name.Substring(0, slash));
        }
    }
}
