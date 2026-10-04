# ForteMove repository audit — 4 October 2026

Scope: the local repository, its tracked/untracked/ignored files, available Git history and objects, and the accepted working Slice 4 implementation. No Slice 5 implementation, demo seed, database initialization, application-code edits, or migration edits were performed. The owner's final instruction authorizes a local Slice 4 commit and merge into main; **publication remains prohibited**.

## Security and privacy findings

**No actual account credential or private key was identified in the inspected repository or available Git history. No credential rotation or history purge is currently indicated by these findings.** This is a bounded inspection, not a guarantee that every possible secret encoding or external service artifact is absent.

The pre-change inventory contained 147 tracked files, 49 untracked project files, 119 ignored local files, and 33 Git metadata files (excluding raw object-storage files, which were separately inspected via Git). The audit scanned all 203 available Git blobs, three commit objects, refs/reflogs and 29 unreachable objects reported by `git fsck`. Reachable history and reflog object names were inspected for sensitive filenames; blob contents included otherwise unreachable old staging objects. Commit metadata/messages were checked too. No custom active Git hooks or credential-bearing remote URL were found. Remote-only branches, GitHub secrets/settings, the Windows credential store, external SQL databases, and files outside this repository were not audited or changed; no fetch or push was performed.

Five ignored files under `.vs/ForteMove/FileContentIndex/*.vsidx` could be inventoried but not read: Visual Studio holds them locked, including on a retry with shared file access. They are not tracked and have no matching path in available history. Their contents are **unverified**; close Visual Studio and repeat the local scan if full cache inspection is required. This does not block sharing a reviewed Git clone, but do not share a raw workspace archive containing `.vs`.

| Finding | Current tracking / history | Assessment and action |
| --- | --- | --- |
| Application connection string in `src/ForteMove.Web/Web.config` | Tracked and in history | Uses Windows Integrated Security, local instance/database, no username/password. Safe development configuration; collaborator must target their own instance. |
| `AuthenticationService.DummySalt` and `DummyHash` | Tracked and in history | Deliberate timing-resistance material used by `PerformDummyVerification`, not an account's stored hash/salt. No rotation/removal required; values are not reproduced here. |
| Password/temporary-password/hash properties and SQL parameter names | Tracked and/or current Slice 4 source | Runtime input/model/persistence code, not stored credentials. Driver creation hashes the supplied temporary password; the initializer prompts securely; password audits contain metadata, not credential values. No plaintext credential logging was identified. |
| `.vs/`, `*.suo`, `ForteMove.Web.csproj.user`, `bin/`, `obj/`, PDBs and caches | Ignored, not tracked; no corresponding history paths found | Contain developer state and personal Windows paths. Keep local; no files were deleted or untracked. |
| Commit author/committer identity | Present in existing Git commit metadata/history | A personal author email and name are exposed when these commits are shared. This is personal information, not a login secret. Decide whether that is acceptable before making the repository public. Future commits can use the account's exact GitHub noreply address; this does not remove identity from existing commits. No identity/history rewrite was performed. |
| Brand PNG provenance | Tracked and in history | Both PNGs have C2PA/OpenAI provenance chunks and public signature/certificate material. No embedded private-key marker or personal Windows path was found. Binary email-like fragments were not treated as verified contact data. No images were modified. |
| Key-shaped pattern in ignored binary VS index | Ignored; not in Git history | Manual inspection found a false positive in a UTF-16 interpretation of the index's ordered character sequences, not a standalone AWS credential. No key value is reproduced. |

No `.env`, credential-bearing connection string, API/GitHub/access token, actual account password hash/salt export, private certificate/key file, SQL Server `.mdf/.ldf/.ndf`, `.bak`, `.bacpac`, publish-credential file, or database export was identified in the current inventory or sensitive history paths. Visual Studio's ignored `.db` files are source/search indexes, not the SQL application database. Migration INSERT statements seed reference roles, bus categories and propulsion types, not demonstration identities or operational data.

If a real credential is later found, first identify its type and owner without reproducing it, revoke/rotate it with the issuing service, then remove it from current source/index. `.gitignore` does not remove prior commits. Coordinate any history remediation and other clones/forks/caches with the owner before rewriting or force pushing; neither is authorized by this audit. For a merely generated tracked file, `git rm --cached -- <reviewed-path>` keeps the local file while removing it from the next commit; no such action is currently necessary.

## Gitignore findings and applied hardening

Existing exclusions already covered `.vs/`, build outputs, `*.user`, `*.suo`, user caches, solution documents state, restored packages, PDBs, `.publishsettings`, `.pubxml.user`, logs/temp/backups and basic OS files.

The updated `.gitignore` adds repository-wide SQL data files, `.bacpac` and SQLite exports, dedicated local/export/backup database directories, local `.pubxml` publish profiles, `.env` variants with sanitized example/sample exceptions, narrowly named local/secret configs, private PFX/P12/key files and common SSH private-key names, dumps/traces/conflict leftovers and local editor workspaces. No matching legitimate tracked source file was excluded. Public `.cer/.crt` files, migrations, `.csproj`, `.sln`, Web.config/transforms, shared editor settings and source assets remain eligible. PEM files can contain either public or private material and are not blanket-ignored; review any future PEM before adding it.

These exclusions prevent accidental addition; they do not enforce confidentiality if someone uses `git add -f`. Environment/local-config exclusions also do not implement application config loading. No already-tracked generated file needs untracking.

## Portability findings

| Issue | Impact / mitigation |
| --- | --- |
| SQL version was not stated in the original prerequisite list | `SqlSchedulingRepository` uses ordered `STRING_AGG`; document SQL Server 2017+ and database compatibility 110+ in the setup guide. [Microsoft reference](https://learn.microsoft.com/en-us/sql/t-sql/functions/string-agg-transact-sql?view=sql-server-2017). |
| Default `.\SQLEXPRESS` / `ForteMove` | Convenient default, not the owner's PC hostname. Use the collaborator's instance and a new local database, matching the initializer and Web.config. |
| Integrated Security and database-creation permission | Collaborator needs their own Windows SQL permissions. IIS Express usually runs as their user; full IIS service identity provisioning is separate. No owner password should be shared. |
| Classic Framework web build tools | Requires VS 2022 ASP.NET/web tooling and Framework 4.8 SDK/targeting support. A modern dotnet SDK alone is insufficient. Imports use MSBuild properties, not personal Visual Studio installation paths. |
| Hard-coded development port | `ForteMove.Web.csproj` defaults to HTTPS localhost:44300 and legacy development-server port 55358. There is no application callback/service dependency on these ports. Resolve collisions through local VS settings and its own HTTPS binding/certificate. |
| Windows timezone | `SystemClock` explicitly uses `South Africa Standard Time`, appropriate to the intended Windows deployment. `en-ZA` culture is not a timezone setting. Assignment Queue's initial date uses host `DateTime.Today`, so that screen may open on the wrong service date on PCs outside South African time. Documented, not changed. |
| Dependencies | Framework assemblies, relative project references and vendored Bootstrap 5.2.3; no unpublished binary reference, NuGet/npm restore requirement, or developer-specific HintPath found. Retain vendor notices; a comprehensive package-vulnerability assessment was not performed. |
| Local certificate and paths | No personal absolute path is required by tracked source/config. Absolute paths in ignored IIS state, indexes, PDBs and build file lists are developer-local output. No private development certificate is committed. |

Development security settings are not production deployment approval: Web.config has debugging enabled, the Release transform disables it during appropriate publishing, and a Release **build alone** is not proof that a transform was applied. The initializer requests SQL encryption but trusts the server certificate; the application's connection string does not explicitly request encryption. Review TLS/certificates, runtime permissions and production configuration before remote deployment; no deployment change is needed just to share source for local development.

One authentication hardening finding also remains: password changes do not revoke previously issued Forms Authentication tickets. Tickets contain only UserAccountId and expiry, and `Global.asax.cs` reloads active account/role flags without checking a password/security version. Someone already holding a valid ticket could remain authenticated after a password change until that ticket expires (30 minutes), unless the account is deactivated. This is not a credential found in Git. Plan explicit session/ticket invalidation when rotating passwords or replacing temporary credentials; no authentication code was changed in this task. It is a deployment/account-security follow-up, not a reason to share the owner's credentials with a collaborator.

## Validation and Git readiness

Debug and Release solution **rebuilds passed**. Whitespace validation passed. Existing manual acceptance through Slice 4 was supplied by the owner; this audit did not repeat interactive acceptance or initialize/change any database. All 193 accepted application/source/tool files, including migrations `0000–0005`, matched their pre-audit byte hashes after documentation changes. Twenty-six representative gitignore inclusion/exclusion cases passed; no tracked source matches an ignore rule. Document links and all explicit project source/content/reference paths resolved. The same source-hash and exact-tree checks are repeated when finishing the local merge. Both brand images were also visually inspected and contain the expected logos, not credential screenshots.

The earlier Slice 2 message amendment created a separate root from `main`. `main` and `Slice-2` have identical baseline trees (`a42ff252ca472e1720248396f2df145e6a454242`), but no merge base; do not use a normal fast-forward or overwrite remote history. [BRANCH_WORKFLOW.md](BRANCH_WORKFLOW.md) records the reviewed local merge that preserves both histories and verifies the final tree against Slice 4.

## Before giving GitHub Write access

1. Publish the reviewed checkpoint/main only after separate owner approval. Until then, another developer cannot clone the new local Slice 4 through GitHub.
2. Agree that the collaborator works on `Slice-5-driver-operations` and requests review before merging. Protect `main` against direct development/force pushes/deletion; freeze `Slice-4` against updates/deletion. Configure available GitHub protections/rulesets and required review according to the repository's plan/visibility. These remote settings were not inspected or changed. [GitHub branch protection](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches).
3. Decide whether existing author email exposure is acceptable for the intended audience; use an exact account-provided noreply address for future commits if desired. [GitHub email guidance](https://docs.github.com/en/account-and-profile/how-tos/email-preferences/setting-your-commit-email-address).
4. Share a Git clone/reviewed source, not a raw `.vs`/build/database-containing folder. Give the collaborator their own GitHub account access and local administrator, not the owner's passwords, tokens or SQL backup.
5. Confirm their clean local setup and Slice 1–4 walkthrough, then approve the unresolved Slice 5 decisions in the plan before coding.

No confirmed secret-remediation blocker was found. Locked ignored caches, unknown remote configuration, and untested fresh-PC setup remain explicit limits.
