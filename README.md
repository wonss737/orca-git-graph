# Git Graph Lite

A small Windows app that shows only the commit graph of a git folder. It is a single WinForms exe built with the C# compiler that ships with Windows, so it needs no browser engine or extra runtime (about 40 MB of memory on a 170-commit repo).

## Build

```
build.cmd        # builds bin\GitGraphLite.exe
build.cmd test   # also builds and runs the tests (bin\Tests.exe)
```

Requires `git` on `PATH`.

## Use

- `bin\GitGraphLite.exe [folder]` opens the folder; without an argument it opens the worktree Orca is showing, or else the last folder.
- It follows Orca: when Orca switches to another worktree, the graph switches to it too (read from Orca's profile state about once a second). A folder opened by hand stays until Orca switches again.
- Pick a folder with **Open folder…** (Ctrl+O), the recent-folder list, or drag a folder onto the window.
- Uncommitted changes (staged, unstaged, untracked) show as a hollow row on top of HEAD; select it to see `git status --short` and `git diff --stat HEAD`.
- The graph reloads by itself when HEAD or refs change (commit, checkout, fetch, rebase) or files in the worktree change; F5 reloads by hand.
- **All branches** toggles `--all`; **Max commits** limits how many commits are read.
- Select a commit to see `git show --stat` below; arrow keys, PageUp/PageDown, Home/End move; Ctrl+C copies the hash.

## Files

- `src/Git.cs` - runs git, parses `git log` and ref decorations
- `src/GraphLayout.cs` - assigns each commit a lane and the lines between rows
- `src/GraphView.cs` - draws the visible rows (lanes, ref badges, subject, author, date, hash)
- `src/MainForm.cs` - window, toolbar, details pane, folder opening
- `src/RepoWatcher.cs` - watches the git dir for HEAD/ref changes and the worktree for file changes
- `src/Orca.cs` - follows the worktree Orca is showing (via Windows' built-in winsqlite3.dll)
- `src/Recent.cs` - recent folders in `%APPDATA%\GitGraphLite\recent.txt`
