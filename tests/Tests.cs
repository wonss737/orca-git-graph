using System;
using System.Collections.Generic;
using System.Linq;
using GitGraphLite;

static class Tests
{
    static int failures;

    static void Check(bool ok, string name)
    {
        Console.WriteLine((ok ? "PASS " : "FAIL ") + name);
        if (!ok) failures++;
    }

    static Commit C(string hash, params string[] parents)
    {
        return new Commit { Hash = hash, Parents = parents, Refs = new List<GitRef>(), Subject = hash };
    }

    static void LinearHistoryStaysInOneLane()
    {
        var layout = GraphLayout.Build(new[] { C("c", "b"), C("b", "a"), C("a") });
        Check(layout.Width == 1 && layout.Rows.All(r => r.Lane == 0), "linear history stays in lane 0");
    }

    static void MergeOpensAndClosesASecondLane()
    {
        var layout = GraphLayout.Build(new[] { C("m", "a", "b"), C("b", "a"), C("a") });
        var rows = layout.Rows;
        Check(layout.Width == 2, "merge graph is two lanes wide");
        Check(rows[0].Bottom.Count == 2 && rows[0].Bottom[1].To == 1, "merge sends second parent to lane 1");
        Check(rows[1].Lane == 1 && rows[1].Bottom[0].To == 0, "branch commit joins lane 0 below");
        Check(rows[1].Through.SequenceEqual(new[] { 0 }), "lane 0 passes by the branch commit");
        Check(rows[2].Lane == 0 && rows[2].Top.Count == 1, "root commit closes the lanes");
    }

    static void ParsesRefs()
    {
        var refs = Git.ParseRefs("HEAD -> main, origin/main, tag: v1, feature/x", new HashSet<string> { "origin" });
        Check(refs.Count == 4, "four refs parsed");
        Check(refs[0].Name == "main" && refs[0].Kind == RefKind.Local && refs[0].Current, "HEAD -> main is current local");
        Check(refs[1].Kind == RefKind.Remote, "origin/main is remote");
        Check(refs[2].Name == "v1" && refs[2].Kind == RefKind.Tag, "tag parsed");
        Check(refs[3].Kind == RefKind.Local, "feature/x without a remote is local");
        var detached = Git.ParseRefs("HEAD", new string[0]);
        Check(detached.Count == 1 && detached[0].Kind == RefKind.Head, "detached HEAD parsed");
    }

    static void ParsesLog()
    {
        string text = string.Join("\u001f", "h2", "h1", "HEAD -> main", "2026-10-11 12:00", "작성자", "한글 제목") + "\n" +
            string.Join("\u001f", "h1", "", "", "2026-10-10 09:00", "me", "init") + "\n";
        var commits = Git.ParseLog(text, new string[0]);
        Check(commits.Count == 2, "two commits parsed");
        Check(commits[0].Parents.SequenceEqual(new[] { "h1" }) && commits[0].Subject == "한글 제목", "fields parsed");
        Check(commits[1].Parents.Length == 0 && commits[1].Refs.Count == 0, "root commit has no parents or refs");
    }

    static void QuotesArguments()
    {
        Check(Git.Quote("log") == "log", "plain arg unquoted");
        Check(Git.Quote("a b") == "\"a b\"", "space quoted");
        Check(Git.Quote("x\"y") == "\"x\\\"y\"", "quote escaped");
        Check(Git.Quote("C:\\my dir\\") == "\"C:\\my dir\\\\\"", "trailing backslash doubled");
        Check(Git.Quote("") == "\"\"", "empty arg quoted");
    }

    static void FiltersRefPaths()
    {
        Check(RepoWatcher.IsRefPath("HEAD") && RepoWatcher.IsRefPath("refs\\heads\\main"), "HEAD and refs watched");
        Check(RepoWatcher.IsRefPath("packed-refs") && RepoWatcher.IsRefPath("worktrees\\x\\HEAD"), "packed-refs and worktree HEAD watched");
        Check(!RepoWatcher.IsRefPath("refs\\heads\\main.lock") && !RepoWatcher.IsRefPath("objects\\ab\\cd"), "locks and objects ignored");
        Check(!RepoWatcher.IsRefPath("index") && !RepoWatcher.IsRefPath("logs\\HEAD"), "index and reflog ignored");
    }

    static void ParsesOrcaState()
    {
        string session = "{\"activeRepoId\":\"r1\",\"activeWorktreeId\":\"r1::C:/Users/me/문서/repo\",\"other\":1}";
        Check(OrcaFollower.ParseActiveWorktree(session) == "C:/Users/me/문서/repo", "worktree path taken after ::");
        Check(OrcaFollower.ParseActiveWorktree("{\"activeWorktreeId\":null}") == null, "no active worktree");
        Check(OrcaFollower.ParseActiveWorktree("{\"activeWorktreeId\":\"r1\"}") == null, "id without a path ignored");
        Check(OrcaFollower.ParseActiveWorktree("{\"activeWorktreeId\":\"r1::C:/a\\\"b\"}") == "C:/a\"b", "escaped quote unescaped");
        Check(OrcaFollower.ParseActiveProfile("{\"schemaVersion\":1,\"activeProfileId\":\"local-default\"}") == "local-default", "active profile parsed");
    }

    static int Main()
    {
        LinearHistoryStaysInOneLane();
        MergeOpensAndClosesASecondLane();
        ParsesRefs();
        ParsesLog();
        QuotesArguments();
        FiltersRefPaths();
        ParsesOrcaState();
        Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
        return failures == 0 ? 0 : 1;
    }
}
