# Slice 4 checkpoint and collaborator branch workflow

The owner's final instruction authorizes creating/committing `Slice-4` and merging it into `main` **locally**. The earlier no-push restriction remains in force. Creating or implementing Slice 5 is not part of this checkpoint operation.

## Inspected starting state

- `Slice-3` at `b996515` has the accepted Slice 4 edits and new files in its working tree.
- `Slice-2` is `d8ed07e`; `main` is `61b5d2a`.
- Slice 2's earlier root-commit message amendment means `main` and `Slice-3` have unrelated roots. `main` and `Slice-2` have **identical file trees**, verified by Git. Consequently the accepted Slice 4 tree can be retained while joining both histories in a real two-parent merge, without rebasing, deleting a branch, or force pushing.
- Local remote-tracking refs have not been refreshed during this audit. Remote branches/protections must be checked before any future publication.

## Reviewed local commands

Run from the repository root. Each validation is a stop gate: if it fails, stop and inspect instead of continuing through the remaining commands. This sequence is specific to the inspected unchanged main baseline; it is not a general recipe for resolving divergent application code.

```powershell
git status --short --branch
git diff --exit-code main Slice-2 --
git switch -c Slice-4
git add --all
git diff --cached --check
git diff --cached --name-only
git commit -m "Slice implemented: Drivers and Intelligent Trip Assignment"

# Join the two histories using the exact accepted tree, without checking out
# old application files or resolving add/add conflicts across unrelated roots.
$mainBefore = git rev-parse main
$checkpoint = git rev-parse Slice-4
$acceptedTree = git rev-parse 'Slice-4^{tree}'
$mergeCommit = git commit-tree $acceptedTree -p $mainBefore -p $checkpoint -m "Merge accepted Slice 4 checkpoint into main"
git update-ref -m "Merge accepted Slice 4 checkpoint into main" refs/heads/main $mergeCommit $mainBefore
git switch main

git diff --exit-code main Slice-4 --
git status --short --branch
git log --all --graph --oneline --decorate
```

This deliberately constructs a real two-parent merge commit using Git's `commit-tree`, with old main as its first parent and the accepted checkpoint as its second parent. It is appropriate here only because main's baseline was verified identical to Slice 2 and has no separate changes. `update-ref` atomically refuses to advance main if its old commit has changed. Neither parent's history is rewritten. The selected tree is exactly Slice 4, which avoids unrelated-root conflict resolution and preserves the accepted working-file bytes, including migration line endings. Check each command's exit code and require identical final trees before treating the merge as complete. If the initial baseline assertion fails, stop and design a normal reviewed merge for those changes instead. Only documentation and gitignore hardening are added to the accepted application work.

Git on this PC may require a **command-scoped** safe-directory setting because the repository was created by the sandbox account:

```powershell
git -c "safe.directory=$((Get-Location).Path)" status
```

That option can be prepended to individual local Git commands. Do not configure `safe.directory=*` globally. Other PCs normally do not need it.

Afterward, leave `main` checked out, retain `Slice-4` at its accepted checkpoint commit, and do not make further feature commits on it. `main` is stable through Slice 4; future work is isolated elsewhere. Merely retaining the branch locally does not technically protect it: later configure GitHub rules to freeze updates/deletion, with an optional protected immutable checkpoint tag if approved.

## Future publication and Slice 5 (not executed)

After explicit owner approval, inspect remote refs and protection rules. If remote main still matches the previously inspected baseline, the merge preserves that baseline as a parent and a normal push can publish it. If it advanced, stop and reconcile/retest; **do not force push**.

```powershell
git fetch origin
git log --left-right --oneline main...origin/main
# Confirm origin/main is an ancestor of local main before publication.
git merge-base --is-ancestor origin/main main
git push --set-upstream origin Slice-4
git push origin main
```

If GitHub requires a pull request, publish the reviewed checkpoint and the local merge result on a separate integration branch instead of directly pushing main:

```powershell
# Alternative publication path, only after explicit approval:
git push --set-upstream origin Slice-4
git push origin main:codex/integrate-slice4
# Open/review a PR from codex/integrate-slice4 to main on GitHub.
```

That integration commit already has old main as a parent, so it provides shared ancestry for review despite the earlier unrelated root. Follow the repository's required reviews/checks and synchronize the local main with the accepted remote result afterward. No PR, permission change or push is part of this task.

Once accepted main is published, the collaborator starts from updated main, using their own clean clone:

```powershell
git fetch origin
git switch main
git pull --ff-only origin main
git switch -c Slice-5-driver-operations
# Only after authorization to begin Slice 5:
git push --set-upstream origin Slice-5-driver-operations
```

If that feature branch already exists remotely, track it rather than creating another branch. Review and acceptance precede its later merge. The collaborator must not develop directly on `main`. No Slice 5 branch or code was created during this audit.
