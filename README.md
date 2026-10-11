# Git Graph Lite

A small Windows app that shows only the commit graph of a git folder. It is a single WinForms exe built with the C# compiler that ships with Windows, so it needs no browser engine or extra runtime (about 40 MB of memory on a 170-commit repo).

## Build

```
build.cmd        # builds bin\GitGraphLite.exe
build.cmd test   # also builds and runs the tests (bin\Tests.exe)
```

Requires `git` on `PATH`.

## Use

- `bin\GitGraphLite.exe [folder]` opens the folder; without an argument it opens the last folder.
- Pick a folder with **폴더 열기…** (Ctrl+O), the recent-folder list, or drag a folder onto the window.
- The graph reloads by itself when HEAD or refs change (commit, checkout, fetch, rebase); F5 reloads by hand.
- **모든 브랜치** toggles `--all`; **최대 커밋** limits how many commits are read.
- Select a commit to see `git show --stat` below; arrow keys, PageUp/PageDown, Home/End move; Ctrl+C copies the hash.

## Files

- `src/Git.cs` - runs git, parses `git log` and ref decorations
- `src/GraphLayout.cs` - assigns each commit a lane and the lines between rows
- `src/GraphView.cs` - draws the visible rows (lanes, ref badges, subject, author, date, hash)
- `src/MainForm.cs` - window, toolbar, details pane, folder opening
- `src/RepoWatcher.cs` - watches the git dir for HEAD/ref changes
- `src/Recent.cs` - recent folders in `%APPDATA%\GitGraphLite\recent.txt`
