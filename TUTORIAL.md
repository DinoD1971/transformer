# A Practical Guide to Building a Dual-Agent Software Development Workflow with Claude Cowork and Claude Code

## Table of Contents

- [Phase 0 — Why this workflow](#phase-0--why-this-workflow)
- [Phase 1 — Install the tools](#phase-1--install-the-tools)
- [Phase 2 — Set up the repo](#phase-2--set-up-the-repo)
- [Phase 3 — Generate a GitHub Personal Access Token](#phase-3--generate-a-github-personal-access-token)
- [Phase 4 — Connect Cowork to GitHub via Docker MCP](#phase-4--connect-cowork-to-github-via-docker-mcp)
- [Phase 5 — Optional: connect Claude Code to GitHub MCP](#phase-5--optional-connect-claude-code-to-github-mcp)
- [Phase 6 — Bootstrap CLAUDE.md](#phase-6--bootstrap-claudemd)
- [Phase 7 — Run the workflow on a real story](#phase-7--run-the-workflow-on-a-real-story)
- [Phase 8 — Pro-plan usage discipline](#phase-8--pro-plan-usage-discipline)
- [Phase 9 — The starter prompt library](#phase-9--the-starter-prompt-library)
- [What this taught us](#what-this-taught-us)
- [Appendix A — Sample CLAUDE.md interview answers (Phase 6)](#appendix-a--sample-claudemd-interview-answers-phase-6)
- [Appendix B — Sample v1 backlog: 15 issues filed (Phase 7)](#appendix-b--sample-v1-backlog-15-issues-filed-phase-7)

---

## Phase 0 — Why this workflow

Most "AI coding" demos show a single agent in a single window doing everything: reading the issue, writing the code, opening the PR, merging it, calling itself done. That works fine for a toy example. It falls apart the moment the work is real, because there is no second pair of eyes — the same agent that wrote the code is the one signing off on it. You end up with a fast pipeline that produces code nobody actually reviewed.

This guide is about a slightly different shape: two Claude agents, each in its own role, with a real GitHub repository sitting between them as the source of truth. One agent (Claude Code, running in your terminal) writes the code. The other agent (Claude in Cowork mode, running in the desktop app) plans, files stories, reviews pull requests, and handles the project-management surface. They never talk to each other directly. They communicate through GitHub issues, pull requests, comments, and labels — the same way two human teammates would.

The result is a workflow where one agent's output is always reviewed by another agent before it lands. Both agents are bound by the same conventions document (a `CLAUDE.md` at the repo root) and the same GitHub branch protection rules. Neither one can shortcut the process — not because we trust the agents to behave, but because the rails physically prevent it.

### What problem this actually solves

The naive single-agent workflow has three failure modes that this dual-agent setup directly addresses.

The first is **the rubber-stamp review**. If the same agent that wrote a change is the one approving it, you don't have a review — you have a self-rationalization. Splitting the roles forces an actual second look, and because the reviewer agent has a separate context window, it doesn't carry forward the implementation rationale that would otherwise let mistakes slide.

The second is **convention drift**. When a single agent picks up work, makes ad-hoc decisions, and merges them, the project's conventions slowly bend toward whatever felt convenient that day. Forcing all changes through a PR review against a written `CLAUDE.md` keeps the conventions explicit. When the reviewer flags a deviation, you learn whether the convention is wrong (update the doc) or the code is wrong (request changes).

The third is **lost context between sessions**. A solo agent in a long-running session accumulates context that vanishes the moment the session ends. The dual-agent workflow externalizes that context into GitHub: every decision, clarification, and trade-off lives on an issue or a PR. A new session of either agent can come up to speed by reading the repo. The agents are interchangeable; the project's memory is in the artifacts they produce.

### When to use this pattern

Use it when you want a software project to make consistent progress while you're not staring at it, and you want the agents' outputs to be accountable to something other than your own gut feel. It is particularly well suited to:

- Greenfield projects where the conventions are still being written down. Forcing PR review surfaces drift early, while the doc is still cheap to update.
- Projects where you (the human) are the architect and reviewer-of-last-resort, but you don't have time to write every line yourself.
- Solo developers or small teams who want the discipline of code review without the headcount.
- Projects you'll come back to in three months and want to be able to pick up cleanly.

### When *not* to use it

Skip this pattern when:

- The work is one-shot or throwaway. Setting up the rails is overhead that pays off across many stories, not one.
- You don't have a clear enough picture of the project to write a meaningful `CLAUDE.md`. Without that document, the reviewer agent has nothing to review against and you end up with two agents agreeing with each other on whatever feels right. Spend a session writing the conventions down before you start the workflow.
- The project requires real-time collaboration with humans on the same code. The dual-agent loop assumes the agents are doing the bulk of the work between human checkpoints. If a human teammate is also pushing commits, the labels and PR conventions need to stretch to cover them — workable, but not what this guide describes.
- You're on a free GitHub account with a private repo and aren't willing to either make the repo public or upgrade. Branch protection on private repos requires a paid plan, and without branch protection the rails don't hold. (More on this in Phase 2.)

### What you'll have at the end

After working through this guide you'll have:

- A GitHub repository with workflow labels, issue and PR templates, a CI pipeline, and branch protection.
- A `CLAUDE.md` at the repo root that both agents read at the start of every task.
- Cowork mode in the Claude desktop app wired up to your GitHub repo through the GitHub MCP server (running in Docker).
- Claude Code installed and authenticated, ready to pick up issues from the terminal.
- A small library of reusable prompts (the `prompts.md` in this repo) that drive the typical workflow steps without you having to re-write them each time.
- A worked example you can clone and inspect: [`DinoD1971/transformer`](https://github.com/DinoD1971/transformer) is the public repo this guide was developed against.

The whole setup takes a focused afternoon. Most of that time is the one-off configuration in Phases 1–4. Once it's running, the per-story overhead is trivial — a few minutes of prompt-pasting and a click on the merge button.

### Watch out for

A few framing points before you start:

- **This is not a hands-off setup.** The agents handle the typing; you handle the judgment. You decide what stories to file, you confirm clarifying questions, you click "Merge". Plan to be present at the keyboard during a story cycle, not running it in the background while you do something else.
- **The workflow assumes a real GitHub repo.** Local-only Git won't do. The repo is the shared blackboard. If you can't push to GitHub for some reason (corporate restrictions, etc.), the dual-agent pattern won't survive the translation to a different host without rework.
- **Branch protection is doing the heavy lifting.** Don't disable it because it's annoying. Annoying is the point.

---

## Phase 1 — Install the tools

Five pieces of software, in roughly this order. Install them all before you start configuring anything; the configuration steps in later phases assume they're all present.

### 1.1 Claude desktop app (with Cowork mode)

Cowork mode is a feature of the Claude desktop app, not the web version. You need the desktop app installed and signed in to a paid Claude plan (Pro, Team, or Enterprise). The Free plan does not include Cowork.

On Windows, the obvious-looking download path leads you astray. The "Download for Windows" button on `claude.ai` redirects through the Microsoft Store, and the Store version of the app installs into a virtualized location that makes some MCP configuration steps in Phase 4 harder to find than they need to be. It still works — you'll just be hunting for config files in `%LOCALAPPDATA%\Packages\` instead of `%APPDATA%\Claude\`.

If you want the conventional install path with the config file in `%APPDATA%\Claude\`, you need the standalone MSIX installer from Anthropic's enterprise download page. Both options work; pick the one whose file layout you prefer. The instructions in Phase 4 cover both.

```powershell
# After installation, launch the app once and sign in.
# Verify it's running:
Get-Process Claude -ErrorAction SilentlyContinue
```

### 1.2 Claude Code CLI

Claude Code is the terminal-side agent. It installs via npm and authenticates the first time you run it.

```powershell
# Requires Node.js 18+
node --version

# Install globally
npm install -g @anthropic-ai/claude-code

# First-run authentication (browser-based)
claude
```

When the `claude` command launches the first time, it opens a browser window and walks you through logging into your Anthropic account. After that, the same paid plan that powers Cowork in the desktop app powers Claude Code in the terminal — they share one usage pool.

### 1.3 Docker Desktop

The GitHub MCP server we connect Cowork to in Phase 4 runs in a Docker container. This is the path Anthropic publishes and it is the path of least resistance; running the server natively is possible but unsupported and not worth the time.

Install Docker Desktop from `docker.com`, run the installer, and let it reboot if it asks. After install, open Docker Desktop and let it finish initializing (the whale icon in the system tray turns from "starting" to "running" — wait for that). Verify from PowerShell:

```powershell
docker --version
docker ps
```

`docker ps` should return an empty container list rather than an error. If it errors with "Cannot connect to the Docker daemon", Docker Desktop isn't fully started yet — wait another minute and try again.

### 1.4 GitHub CLI (`gh`)

Claude Code uses the GitHub CLI to interact with issues, branches, and PRs from the terminal. We deliberately do *not* give Claude Code an MCP connection to GitHub (see Phase 5); `gh` is sufficient and keeps the agent's tooling simple.

```powershell
winget install --id GitHub.cli
# or download from https://cli.github.com

gh --version
gh auth login
```

`gh auth login` walks you through an OAuth flow in the browser. Pick HTTPS as the protocol when prompted. After it finishes, run `gh auth status` to confirm you're authenticated.

### 1.5 Project-specific build tools

These are only needed if you're going to actually build the example project as you read along. Skip if you're just here for the workflow.

```powershell
# .NET 8 SDK
winget install Microsoft.DotNet.SDK.8

# Azure Functions Core Tools v4
npm install -g azure-functions-core-tools@4 --unsafe-perm true

dotnet --version       # should print 8.x
func --version         # should print 4.x
```

### Watch out for

- **Microsoft Store redirect on Windows.** The "Download for Windows" link on `claude.ai` redirects to the Microsoft Store. The Store version is functional but installs the app into a per-user virtualized AppData path that makes the Phase 4 config-file hunt mildly annoying. If you'd rather avoid that, grab the MSIX installer from Anthropic's enterprise download page. (Don't bother re-installing if you've already got the Store version working — you can find the config file either way, you just need to know where to look.)
- **Docker Desktop must be *running*, not just installed.** A fresh install often leaves Docker in a "starting" state for a minute or two. MCP calls from Cowork that depend on the GitHub MCP container will silently fail or hang if Docker isn't ready when the app launches. Make a habit of confirming `docker ps` succeeds before opening Cowork.
- **Don't use `sudo`/admin elevation for `npm install -g` unless you have to.** On Windows it's usually fine; if you hit permission errors, fix npm's prefix path (`npm config set prefix`) instead of running PowerShell as admin every time.

---

## Phase 2 — Set up the repo

The repo isn't just where the code lives; it's the substrate the whole workflow runs on. Labels are how the agents track state. Templates are how stories get filed consistently. CI is the gate that prevents broken merges. Branch protection is the rule that keeps the dual-agent loop honest.

We'll set all of that up before either agent touches a line of code.

### 2.1 Create the repo (and make a deliberate public/private choice)

```powershell
gh repo create transformer --public --description "Azure Function for JSON transformation" --clone
cd transformer
```

The `--public` flag matters. On GitHub Free, branch protection rules are not available on private repos. Without branch protection, anyone — including either agent — can push directly to `main`, and the entire workflow's "all changes go through a PR" assumption disappears.

You have three options:

1. **Public repo on Free.** This is what the example uses. Branch protection works. Trade-off: anyone on the internet can read your code. Fine for greenfield, learning projects, and anything you'd be okay open-sourcing eventually.
2. **Private repo on Pro.** Branch protection works. Trade-off: a paid GitHub plan.
3. **Private repo on Free, no branch protection.** Workable in theory if you trust yourself and the agents not to push to main. In practice, the rails are doing real work; skipping them weakens the whole pattern. Don't do this if you have any other option.

Pick option 1 or 2. This guide assumes branch protection is in play.

### 2.2 Add the workflow labels

The labels are how stories signal where they are in the cycle. Add them once at the start; from then on the agents (and you) only ever change which one is applied.

```powershell
# Status labels (one is applied to every issue at all times)
gh label create "status:ready" --color "0e8a16" --description "Specified, ready for Claude Code"
gh label create "status:in-progress" --color "fbca04" --description "Claude Code is working it"
gh label create "status:needs-clarification" --color "d93f0b" --description "Blocked on a question for the human"
gh label create "status:in-review" --color "1d76db" --description "PR open, awaiting review"
gh label create "status:qa" --color "5319e7" --description "Merged, awaiting smoke test"
gh label create "status:done" --color "ededed" --description "Accepted and closed"
gh label create "status:blocked" --color "b60205" --description "Blocked by external dependency"

# Type labels (one per issue)
gh label create "type:feature" --color "a2eeef" --description "New capability"
gh label create "type:bug" --color "ee0701" --description "Defect fix"
gh label create "type:chore" --color "cfd3d7" --description "Refactor, tooling, docs"
```

There is nothing magic about these specific labels. What matters is that you have *exactly one* status label class and exactly one type label class, and that the agents know what every state means. The list above is the one used in `CLAUDE.md` and the prompt library — keep it consistent across all three.

### 2.3 Add the issue and PR templates

Both templates live under `.github/`. They are markdown files with light frontmatter; GitHub picks them up automatically.

`.github/ISSUE_TEMPLATE/story.md`:

```markdown
---
name: Story
about: A unit of work for the agent workflow
title: ''
labels: 'status:ready'
---

## Context
<!-- Why are we doing this? Link to CLAUDE.md sections if relevant. -->

## Acceptance Criteria
- [ ]
- [ ]
- [ ]

## Technical Notes
<!-- Files to touch, libraries to use, patterns to follow. -->

## Out of Scope
<!-- Things NOT to do in this story. -->

## Open Questions
<!-- Cowork leaves blank when filing. Claude Code adds here if it gets stuck. -->
```

`.github/pull_request_template.md`:

```markdown
## Summary
<!-- What changed and why. One paragraph. -->

Closes #

## What was tested
- [ ] `dotnet build` passes
- [ ] `dotnet test` passes
- [ ] Manual smoke test
- [ ] No new console errors or warnings

## Acceptance Criteria
<!-- Copy from the linked issue. Check off as completed. -->
- [ ]
- [ ]
```

The Acceptance Criteria checklist on the PR forces Claude Code to copy the issue's criteria over and tick them as it implements. That makes Cowork's review job in Phase 7 trivial — it can compare the PR's checked items against the issue's original criteria and immediately spot anything that was skipped.

### 2.4 Add the CI workflow

`.github/workflows/ci.yml`:

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --configuration Release --no-restore

      - name: Test
        run: dotnet test --configuration Release --no-build --verbosity normal
```

Three steps: restore, build, test. CI gates the merge, so if any of them fails, the PR can't be merged until it's fixed. This is the loop that catches a Claude Code session that finished but didn't actually get the tests passing.

### 2.5 Initial commit, push, and the expected CI failure

```powershell
git add .github/
git commit -m "chore: add labels, templates, and CI workflow"
git push origin main
```

When this hits GitHub, Actions will fire and the build will fail. **This is expected.** There's no .NET project yet — `dotnet restore` has nothing to restore against. The CI failure isn't a configuration problem; it's the file structure being incomplete.

Don't panic, don't disable CI, don't add a stub project just to make the green checkmark appear. The first real story will create the project and the first CI run after that PR will go green. Until then, leave the failure visible. It's an accurate signal that there's no working build yet.

### 2.6 Configure branch protection

Branch protection has one constraint that catches people: **you can't protect a branch that doesn't exist**. The `main` branch is created by your first push. Set up the protection rule after that push, not before.

Via the GitHub UI: Settings → Branches → Add branch ruleset (or "Add rule" under Branch protection rules, depending on which UI version you see) → target `main`.

Settings to enable:

- **Require a pull request before merging.** Enable. Set "Required approvals" to **1**.
- **Require status checks to pass before merging.** Enable. Add the `build-and-test` check from the CI workflow once it's run at least once (status checks only show up after they've fired once).
- **Require branches to be up to date before merging.** Enable.
- **Do not allow bypassing the above settings (`enforce_admins`).** Leave **off**. We want admins to be able to bypass deliberately for paper-trail-only situations (see Phase 7's discussion of the docs-only direct push). Enabling this would mean *no one* can push to main, even you.

Or via the CLI, if you'd rather:

```powershell
gh api repos/DinoD1971/transformer/branches/main/protection `
  --method PUT `
  --field required_pull_request_reviews='{"required_approving_review_count":1}' `
  --field required_status_checks='{"strict":true,"contexts":["build-and-test"]}' `
  --field enforce_admins=false `
  --field restrictions=null
```

### Watch out for

- **Branch protection requires the branch to exist.** If you try to add a rule before pushing anything, GitHub will tell you the branch doesn't exist. Push first, protect after.
- **CI failure on the very first push is normal.** The repo doesn't have a buildable project yet. Don't waste time debugging — the next story creates the project and CI will go green when that PR merges.
- **Free + private = no branch protection.** GitHub Free disallows branch protection on private repos. If you're on Free, the choice is: make the repo public, upgrade, or skip branch protection. The first two preserve the workflow; the third weakens it materially.
- **`enforce_admins=false` is a deliberate choice.** It keeps the admin bypass available for situations where you (the human) need to push directly to main — for example, this very tutorial, which we're about to commit straight to main with a paper-trail issue. Be deliberate about using the bypass; never let an agent use it.
- **The status check name has to match exactly.** "Required status checks" lets you list checks by name. The name comes from the job name in your workflow YAML. If you rename the job, update the protection rule.

---

## Phase 3 — Generate a GitHub Personal Access Token

Cowork talks to GitHub through a Personal Access Token (PAT) that you generate once and paste into the MCP config. The token is the agent's identity inside GitHub — every action Cowork performs (filing issues, opening PRs, commenting) is logged as you, authenticated by this token.

### 3.1 Choose fine-grained vs. classic

GitHub offers two PAT types:

- **Fine-grained PATs.** Scoped to specific repositories, expire by default, narrower permission model. The newer option and the one GitHub recommends.
- **Classic PATs.** Global scope (all your repos), broader permission model, easier to misconfigure to the agent's advantage and your detriment.

Use fine-grained. The setup takes thirty seconds longer; the blast radius if the token leaks is dramatically smaller.

### 3.2 Generate the token

GitHub UI: Settings → Developer settings → Personal access tokens → Fine-grained tokens → Generate new token.

Settings:

- **Token name.** Something memorable like `cowork-mcp-transformer`.
- **Expiration.** 90 days is reasonable. Shorter is better; you'll get used to rotating.
- **Repository access.** Select "Only select repositories" and pick `DinoD1971/transformer` (or whatever your repo is). Don't grant access to all repos.
- **Permissions.** Under "Repository permissions":
  - Contents: **Read and write**
  - Issues: **Read and write**
  - Pull requests: **Read and write**
  - Metadata: **Read-only** (this is auto-selected and required)
  - Workflows: **Read and write** (needed if the agent is going to update CI configuration)

Leave everything else at "No access". The agent does not need org permissions, account permissions, or anything outside the repo.

Click "Generate token". Copy the token immediately — GitHub shows it once. If you lose it, you have to regenerate.

### 3.3 Store it locally

Don't paste the token directly into a config file you might commit. Two reasonable storage approaches:

1. **Environment variable.** Set it once in your user environment so the MCP config can reference it by name rather than by literal value.

   ```powershell
   # Permanent (persists across reboots)
   [Environment]::SetEnvironmentVariable("GITHUB_PAT", "ghp_yourTokenHere", "User")

   # Verify (open a fresh PowerShell window first)
   $env:GITHUB_PAT
   ```

2. **Windows Credential Manager.** More secure but more work to wire up. Skip unless you have a specific reason.

The MCP config in Phase 4 will read the token from the environment variable. If you ever rotate the PAT, you only have to update one place.

### Watch out for

- **Don't commit the token.** Even to a private repo. PATs in commit history are a recurring source of incidents. If you ever paste one into a file by accident, regenerate it immediately — assume it's compromised once it's been on disk in plain text in a Git-tracked location.
- **Don't use a classic PAT.** The convenience of "all repos, all permissions" is exactly the convenience an attacker wants if the token leaks.
- **Set an expiration.** It feels like friction, but it forces you to rotate, which is the behavior you want.
- **Workflows scope is needed if the agent will edit CI.** If you skip it and the first story touches `.github/workflows/`, the agent will get a permissions error pushing the change. Easier to grant once than to debug later.

---

## Phase 4 — Connect Cowork to GitHub via Docker MCP

This is the configuration step where the desktop app gets its first set of "tools" beyond plain chat. We're going to point Cowork at the GitHub MCP server image, give it the PAT from Phase 3, and verify the connection.

### 4.1 Anatomy of `claude_desktop_config.json`

The Claude desktop app reads MCP server configuration from a JSON file at startup. Each top-level key under `mcpServers` is the name of a server; the value tells the app how to launch it. For Docker-hosted MCP servers, the launch command runs the Docker image and pipes stdio between the container and the app.

A minimal config with the GitHub MCP server looks like this:

```json
{
  "mcpServers": {
    "github": {
      "command": "docker",
      "args": [
        "run",
        "-i",
        "--rm",
        "-e",
        "GITHUB_PERSONAL_ACCESS_TOKEN",
        "ghcr.io/github/github-mcp-server"
      ],
      "env": {
        "GITHUB_PERSONAL_ACCESS_TOKEN": "${GITHUB_PAT}"
      }
    }
  }
}
```

A few things to notice:

- The `command` is `docker`, not the MCP server itself. Docker runs the container, the container runs the server, the app talks to the container via stdio.
- `--rm` tells Docker to delete the container when it exits. The MCP server is launched fresh on every Claude desktop startup; we don't want stale containers piling up.
- `-i` keeps stdin open so the app can talk to the server.
- The `-e GITHUB_PERSONAL_ACCESS_TOKEN` flag passes the named env var into the container; the value comes from `env.GITHUB_PERSONAL_ACCESS_TOKEN`, which interpolates `${GITHUB_PAT}` from your shell environment.

If you'd rather paste the token literally into the config (don't), replace `"${GITHUB_PAT}"` with the literal token string. You'll regret it the first time you back up your home directory to a place you forgot about.

### 4.2 Where the config file actually lives

This is the gotcha that ate the most time on initial setup.

The Anthropic docs tell you the config file is at:

```
%APPDATA%\Claude\claude_desktop_config.json
```

If you installed the desktop app from the **standalone MSIX installer** (Anthropic's enterprise download page), this is correct. The file is at `C:\Users\<you>\AppData\Roaming\Claude\claude_desktop_config.json`. If the file doesn't exist, create it.

If you installed the desktop app from the **Microsoft Store**, the path is different. The Store install runs the app inside a UWP package container, which virtualizes its AppData. The actual file path is:

```
%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Claude\claude_desktop_config.json
```

(The package family name `Claude_pzs8sxrjxfjjc` is stable for the Store-published version. If you're unsure, list your `%LOCALAPPDATA%\Packages\` directory and look for the Claude entry.)

To find which one applies to you:

```powershell
# Check standalone install location
Test-Path "$env:APPDATA\Claude\claude_desktop_config.json"

# Check Store install location
Get-ChildItem "$env:LOCALAPPDATA\Packages\" -Filter "Claude_*" -Directory | ForEach-Object {
    "$($_.FullName)\LocalCache\Roaming\Claude\claude_desktop_config.json"
}
```

Whichever one returns `True` (or shows an existing file) is your real config path. Use that path for the steps below.

### 4.3 Pull the GitHub MCP server image

```powershell
docker pull ghcr.io/github/github-mcp-server
```

This downloads the image once. Subsequent Cowork startups just spin up a container from the cached image — fast. If Docker Desktop isn't running, this command will hang or fail; make sure the whale icon is steady before running.

### 4.4 Write the config and restart Cowork

Open the config file at the path you identified in 4.2. If it doesn't exist, create it. Paste in the JSON from 4.1.

If you already have other MCP servers configured (filesystem, Slack, etc.), merge the `github` entry into your existing `mcpServers` object — don't replace the whole file.

Save the file. **Fully quit and reopen the Claude desktop app.** "Fully quit" means close the window *and* exit from the system tray icon. The app re-reads the config only on cold start.

### 4.5 Verify the connection

In a fresh Cowork conversation, ask Cowork to list the GitHub repos you have access to, or to look up your repo by name. Something concrete that requires hitting the GitHub API.

```
List the open issues on DinoD1971/transformer.
```

If the connection is working, Cowork will call the appropriate GitHub MCP tool and return real data from the repo. If it isn't, you'll see one of a few characteristic failure modes:

- "I don't have access to GitHub tools" — the config didn't load. Check the path you used in 4.2 and confirm the JSON is valid.
- "Authentication failed" / 401 — the PAT isn't being passed correctly. Make sure `GITHUB_PAT` is set in your user environment (run `$env:GITHUB_PAT` in a fresh PowerShell to confirm) and that you actually restarted the Claude desktop app *after* setting it.
- The call hangs or times out — Docker Desktop probably isn't fully ready. Check `docker ps` from a terminal.

### Watch out for

- **MSIX vs. Store install changes the config path.** The MSIX install puts everything under `%APPDATA%\Claude\`; the Store install virtualizes that to `%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Claude\`. Use whichever matches your install. If you ever migrate from one to the other, you'll need to copy the config file across.
- **Restarting Cowork means *fully quitting*.** Closing the window does not reload the config. Right-click the system tray icon and explicitly Quit, then relaunch. Skip this and you'll think your config edits aren't taking effect.
- **The PAT lives in your environment, not the config.** That means a fresh PowerShell session is needed to pick up changes to `$env:GITHUB_PAT`, and the desktop app reads from the user environment, not from a single shell. Setting it via `[Environment]::SetEnvironmentVariable(..., "User")` rather than `$env:GITHUB_PAT = ...` (which only sets it for the current shell) is what makes it visible to the app.
- **Docker Desktop has to be running before Cowork launches.** If Docker isn't ready when the app tries to spin up the MCP container, the GitHub tools just won't show up. The fix is to start Docker first, wait for it to settle, then start Cowork.
- **PAT leaks via screenshots.** If you record a setup walkthrough and the PAT is anywhere on screen — config file, env var output, browser tab from when you first generated it — regenerate before publishing.

---

## Phase 5 — Optional: connect Claude Code to GitHub MCP

Claude Code can also be wired to the GitHub MCP server. We deliberately didn't do this for the `transformer` project, and unless you have a specific reason, you probably shouldn't either.

### Why we skipped it

Claude Code already has a perfectly capable GitHub interface: the `gh` CLI, which you installed in Phase 1.4. Every operation we'd want the agent to perform — listing issues, viewing an issue body, creating a branch, opening a PR, reading PR comments, replying to a review — is a one-liner with `gh`. The agent learns the CLI syntax once and applies it naturally as part of its shell workflow.

Adding a second path to GitHub via MCP gets you:

- A second authentication mechanism to keep in sync (the MCP PAT and `gh`'s OAuth token).
- Two ways to do the same thing, which means more decisions for the agent to make about which to use, which means more inconsistency.
- A bigger surface area of "things that could be misconfigured" when something doesn't work.

What it doesn't get you: any new capability. Anything the GitHub MCP can do, `gh` can do, often more directly.

### When it would be worth doing

The case for hooking Claude Code up to the GitHub MCP server is real but narrow. Consider it if:

- You want both agents to use *exactly* the same tooling, for symmetry. (This is mostly aesthetic; in practice the agents do different jobs.)
- You're operating without `gh` for some reason — corporate machine where you can't install it, restricted network. The MCP container approach can work where the CLI install path can't.
- You're doing GitHub operations that the `gh` CLI doesn't cover well and the MCP tool does (rare; `gh` is comprehensive).

For a single-project workflow like the one in this guide, the answer is: don't bother. Skip Phase 5, use `gh` from Claude Code, keep the moving parts to a minimum.

### Watch out for

- **Two paths to GitHub means two PATs to rotate.** If you do wire up the MCP server for Claude Code, you now have two tokens to rotate, expire, and remember about. Worth the cost only if you're getting something out of it.
- **Tool overlap can confuse the agent.** Given two equivalent tools, agents sometimes pick the worse one for the situation. Removing the choice removes the failure mode.

---

## Phase 6 — Bootstrap CLAUDE.md

`CLAUDE.md` at the repo root is the single most important artifact in this entire workflow. Both agents read it at the start of every task. It is the contract that turns "two Claude instances flailing at a problem" into "two Claude instances collaborating on a project with shared conventions."

### What CLAUDE.md is for

`CLAUDE.md` answers the questions an agent needs to answer before writing a single line of code: What is this project? What stack? What patterns do we use? What does "done" look like? What's our test discipline? How are issues and PRs structured?

It is not API documentation. It is not a tutorial. It is not a "what we plan to build someday" wishlist. It is a description of the project as it exists right now, written for an agent that is about to start working in it.

### Sections that earn their place

The `CLAUDE.md` in the `transformer` repo has nine sections. They are not all required for every project — adapt to your situation — but each one exists because we hit a moment where having it written down would have saved a clarification round.

1. **Project Overview.** One paragraph. What the system does and where it sits in a larger pipeline. An agent that doesn't have this loses time reasoning about scope.
2. **Tech Stack.** A concrete table: runtime, framework version, JSON library, logging library, HTTP extensions. Settles the "should I add Polly?" question before it gets asked.
3. **Project Structure.** The folder tree, the naming conventions, the file-naming rules. Stops the agent from inventing a parallel structure on the side.
4. **Configuration Format.** If the project has a domain-specific config format (in `transformer`'s case, the JSON transformation specs), spell out the schema and the supported features. Saves the agent from making up a format that almost works.
5. **API Contract.** Routes, request shapes, response shapes, status codes. Without this, every endpoint becomes a clarification question.
6. **Coding Conventions.** Async rules, DI rules, exception handling, logging. The conventions that a human reviewer would notice if violated, but that an agent might not infer from the existing code if there's not yet much existing code.
7. **Testing.** Framework, mocking library, coverage target, integration vs. unit policy. Without an explicit coverage target, the agent will write either zero tests or fifty.
8. **Deployment.** Resource naming, environments, secret-management policy. Even if you're not deploying yet, having the names committed early prevents drift.
9. **GitHub Workflow.** Labels, story flow, branch naming, PR template, CI requirements, merge rules. This is the section that ties the agents back to the rails set up in Phase 2. Without this, the agents will reinvent the workflow on every new task.

The full `CLAUDE.md` from the `transformer` project is committed at the repo root and is the example for this phase: see [`CLAUDE.md`](https://github.com/DinoD1971/transformer/blob/main/CLAUDE.md). Read it in full to see how the sections connect to each other, then write your own version with project-appropriate content. Don't just copy verbatim — sections that are wrong for your project are worse than no section at all.

### Why both agents need to read it

A reviewer can only catch a deviation from a convention if the convention is written down somewhere the reviewer can point to. If `CLAUDE.md` says "all I/O-bound operations use `async/await` end-to-end" and Claude Code submits a PR with a `.Result` call, Cowork can flag it with a citation. Without the doc, the same review degenerates into Cowork's general taste vs. Claude Code's general taste, which is exactly the convention drift we set out to prevent.

In the prompt library (Phase 9), every Cowork review prompt and every Claude Code pickup prompt explicitly instructs the agent to read `CLAUDE.md` first. That isn't ceremony; it's the mechanism that gives both agents the same starting point.

### Keeping it alive

`CLAUDE.md` will go stale. Frameworks update, your understanding of the project deepens, and at some point an agent will say "the modern pattern for X is Y, but the doc says Z". When that happens:

- Don't have the agent silently follow the modern pattern. That's drift.
- Don't reflexively force the doc's pattern. Maybe the doc is wrong now.
- Decide deliberately: file an issue, decide which one to keep, update the doc if needed.

A useful retrospective prompt is in `prompts.md` ("Cowork — Mid-project retrospective"). Run it every five to ten stories; ask which conventions are getting questioned, which clarifications keep recurring, and what should change in `CLAUDE.md` to head them off.

The doc is a living surface. Every clarification round is feedback that something in it is missing, ambiguous, or out of date.

### Watch out for

- **Don't write CLAUDE.md by asking an agent to "summarize the project".** It will produce something that *sounds* like a project description but is full of plausible-sounding fictions. Write it yourself, or have the agent draft it from explicit material you point at, then edit hard.
- **Resist the temptation to put aspirational content in.** "We will eventually use Polly for retries" is not a convention; it's a wish. If it isn't true today, it doesn't belong in `CLAUDE.md` today.
- **The doc gets stale fastest in fast-moving sections.** Tech Stack, dependencies, and deployment pipelines drift quickly. API contract and coding conventions drift slowly. When updating, prioritize the sections that the agents cite most.
- **Update when a deviation is flagged, not before.** If an agent flags "the modern pattern for X is Y", that's the moment to decide. Pre-emptive rewrites tend to introduce new bugs in the doc.

> **See [Appendix A](#appendix-a--sample-claudemd-interview-answers-phase-6)** for a complete worked example: the actual answers given during the interview that produced this project's `CLAUDE.md`. Useful as a reference for the level of specificity each section needs.

---

## Phase 7 — Run the workflow on a real story

Configuration is done. Now we run the loop. This phase walks through the full cycle on a single story: file, pick up, implement, review, merge, unblock the next one. The pattern is the same every time after this.

### 7.1 File the backlog up front

The first instinct is to file one story at a time, work it, then file the next. Resist that. File the whole backlog in one Cowork session before you start any implementation.

There are three reasons:

1. **It surfaces dependencies you'd otherwise miss.** When you write story 8 and realize it depends on story 3, that's information you only get by writing them both.
2. **It catches scope problems early.** Stories that don't fit cleanly into the template (no clear acceptance criteria, no obvious "done" state) signal that you don't understand the work yet. Better to discover that on a Sunday afternoon than mid-implementation on Wednesday night.
3. **It's faster.** Filing twelve stories in a single Cowork session is dramatically less expensive (in usage and in your time) than twelve separate sessions of "now file the next one."

Use the *Cowork — File a follow-up story* prompt from `prompts.md` for each one, or write a single bulk prompt that walks through your whole backlog. Apply `status:ready` to the first one (or first few, if they're parallelizable) and `status:blocked` to anything with a dependency. Cowork should add a "Blocked by #N" line to each blocked issue's body so the chain is explicit.

> **See [Appendix B](#appendix-b--sample-v1-backlog-15-issues-filed-phase-7)** for the actual 15 issues filed for the `transformer` project. Useful as a reference for how stories should be sized, how acceptance criteria should be written, and how dependencies should be expressed via the `Blocked by` chain.

### 7.2 Cowork: status summary at the start of a session

When you sit down to work on the project, the first prompt of the session is the status summary from the prompt library. This grounds you in where things stand without you having to scroll through the issue list.

```
Give me a status summary of DinoD1971/transformer:
1. Current open issues by label (counts)
2. The single issue currently in status:ready (if any)
3. Any open PRs and their state
4. Anything in status:needs-clarification I owe answers on
5. Recommended next action
```

Cowork hits the GitHub MCP, pulls the data, and gives you the rundown in about ten seconds.

### 7.3 Claude Code: pick up the story

Open a terminal, `cd` into the repo, and run `claude`. Once the prompt is up, paste the *Claude Code — Pick up a story* prompt from `prompts.md`. It instructs Claude Code to:

1. Read `CLAUDE.md` in full.
2. List `status:ready` issues via `gh`.
3. Read the highest-priority one.
4. Confirm the title and acceptance criteria back to you as a checklist.
5. Outline its implementation plan.
6. Identify ambiguities — if any, post a clarification question and switch the label.
7. Wait for your approval before starting.

The "wait for your approval" step is non-negotiable. Even when the plan looks fine, take twenty seconds to look at it. Catching a misunderstanding here costs nothing; catching it after a hundred lines of code costs a lot.

If Claude Code raises clarification questions, switch over to Cowork and run the *Cowork — Answer clarification questions* prompt. Cowork pulls the question, drafts an answer grounded in `CLAUDE.md` and the existing acceptance criteria, posts the answer, and flips the label back to `status:in-progress`. Then return to Claude Code and tell it to re-read the issue.

### 7.4 Claude Code: implement, commit, push, open PR

After approval, Claude Code:

1. Switches the label from `status:ready` to `status:in-progress`.
2. Creates a feature branch named `<type>/<issue-number>-<slug>` (e.g., `feature/3-add-dotnet-project`).
3. Implements per `CLAUDE.md`, running `dotnet build` and `dotnet test` after meaningful changes.
4. Commits with a conventional commit message that includes `Closes #N` on its own line in the body.
5. Pushes the branch.
6. Opens a PR via `gh pr create`, filling out the PR template (summary, `Closes #N`, what was tested, acceptance criteria checklist).
7. Switches the issue label to `status:in-review`.
8. Reports back with the PR URL.

The branch name format isn't decoration. It encodes the story type and issue number, which makes the PR list scannable and makes the post-merge cleanup unambiguous about which issue closed.

### 7.5 Cowork: review the PR

Switch back to Cowork. Run the *Cowork — Review a pull request* prompt with the PR number. Cowork:

1. Reads the full diff.
2. Reads the linked issue's acceptance criteria.
3. Re-reads relevant sections of `CLAUDE.md`.
4. Checks: does every acceptance criterion appear in the diff? Does the code follow the conventions? Are tests present and meaningful (not just trivially passing)? Are there code smells or architectural concerns?

If the PR passes, Cowork submits a formal approving review with a brief summary comment. If it fails, Cowork posts specific change requests at the relevant lines, flips the issue label from `status:in-review` back to `status:in-progress`, and comments on the issue with what needs to change. The ball is back in Claude Code's court.

When changes are requested, switch back to Claude Code and run the *Claude Code — Address PR review feedback* prompt. Claude Code reads the comments, makes the changes, runs build and tests, replies to each comment, pushes a follow-up commit, and flips the label back to `status:in-review`.

This back-and-forth can take more than one round. That's fine. It's working as designed.

### 7.6 Who actually approves the PR — and why the human merge is the gate
GitHub branch protection, configured the way Phase 2.6 sets it up, requires one approving review from someone other than the PR author before a merge can complete. That rule is what prevents an agent from writing code and approving its own work — the failure mode this entire workflow is designed around.

In this setup, both Claude Code and Cowork operate against your GitHub identity via your PAT. That means GitHub sees both agents as "you" for authorship purposes. Claude Code authors the PR; Cowork can submit a review of the PR, but because GitHub sees it as authored by the same identity that opened the PR, the review doesn't satisfy the 'one approving review required' rule. So how does the merge actually happen?

The honest answer: the merge is the human's deliberate action, executed with admin bypass.

This is why Phase 2.6 leaves enforce_admins=false in the branch protection configuration. The rails are configured strictly — required PR, required approving review, required passing CI, required up-to-date branch — and the agents cannot bypass any of them on their own. But you, as the repository admin, can. And every merge in this workflow is you, deliberately, choosing to bypass the "1 required approving review" rule after Cowork's review has done the substantive work of checking the diff against the acceptance criteria and the conventions.

That is not a workaround. That is the design.

The mental model is: the rails train the agents, and the human signs off. The agents are constrained from shortcutting because they cannot self-approve and cannot bypass admin settings. The human retains the irreversible decision because the merge action is, by intent, the moment of human judgment. Cowork's review is advisory — extremely thorough advisory, grounded in the acceptance criteria and CLAUDE.md, but advisory. Your click on the Merge button is the decision.

*In a team setting, the cleaner architecture is a separate GitHub identity for the reviewer agent (a bot account with its own PAT, or a human collaborator) so the approving review satisfies branch protection without bypass. For a solo-developer setup running both agents through your own identity, admin bypass is the simplest model that holds together. The principle below is the same in both cases — the rails train the agents, the human signs off.*

This shape matters for several reasons:

- It puts the irreversible action in human hands. The agents do the generation and the analysis; the human does the merge. If you ever found yourself wanting to script the merge step too, you'd have slipped back into the single-agent failure mode this whole pattern is built to avoid.
- It makes Cowork's review honest. Because Cowork knows its review is advisory rather than dispositive, it has no incentive to soften feedback to get a merge through. It can flag what it flags. You decide what to do with the flags.
- It keeps the audit trail intact. Every merge in the repo's history shows your username and the admin-bypass annotation. There's no ambiguity later about who decided what. If you ever need to defend a merge — to yourself in retrospect, to a future teammate, to a client — the record is clean.

### 7.7 Merge, then post-merge cleanup

You click Merge in the GitHub UI. The PR closes. The issue auto-closes because of the `Closes #N` in the commit body.

Switch back to Cowork and run the *Cowork — After-merge cleanup* prompt. Cowork:

1. Verifies the issue auto-closed (closes it manually if not).
2. Updates the closed issue's labels: removes `status:in-review`, adds `status:done`.
3. Finds any `status:blocked` issues whose dependencies are now satisfied.
4. For each, removes `status:blocked` and adds `status:ready`, with a comment confirming the unblock.
5. Reports back: which issue closed, which were unblocked, which remain blocked and why.

That last step is the loop closer. The newly-`status:ready` issue is now the next one Claude Code picks up. Run the pickup prompt again and start the cycle over.

### 7.8 The admin-bypass paper trail

There are situations where pushing directly to main is the right call. The two recurring ones are:

- **Docs-only changes.** Adding or updating a tutorial, a README, a clarification in `CLAUDE.md`. The PR-and-review ceremony isn't doing protective work for a docs file.
- **Recovery from a broken main.** A merge slipped through with an issue that needs immediate fixing and going through a full PR cycle would leave main broken longer than necessary.

When you bypass branch protection (admin push to main), the workflow is:

1. Make the change locally on main.
2. Push.
3. **Immediately file a paper-trail issue** describing what was bypassed and why. Apply `type:chore` and `status:done` (since the work is already on main). The issue is the audit log entry. It is not a request to redo the work; it's the record that the work happened outside the normal flow.

This tutorial is a docs-only change, so we'll commit it directly to main and file a chore issue right after. That's the pattern.

The reason to bother filing the issue, even for a self-evident change, is that "I bypassed the rails this once for a good reason" has a way of becoming "I bypass the rails whenever I want". The paper trail is a small enough ritual that you can keep doing it; once the ritual stops, the rails stop being rails.

### 7.9 The merged-branch trap

After a PR is merged, the feature branch on GitHub has served its purpose. **Don't add new commits to it.** Specifically, don't think "I'll just amend the merged work with one more thing" and push to the merged branch.

Two reasons:

- The branch may already be deleted on the remote (if you have "automatically delete head branches" enabled).
- Even if it's still there, GitHub considers the PR closed; your new commit doesn't become part of any open review.

If you need to add follow-up work, file (or pick up) a new story and create a fresh branch from main. This sounds obvious in writing and is surprisingly easy to forget when you're in the flow of "I just need to tweak one thing".

### Watch out for

- **Self-approval is blocked.** GitHub will not let the PR author single-handedly approve their own PR. The dual-agent setup works around this *because* the human (you) is the one merging — the agents do the work and the review, you sign off.
- **`enforce_admins=false` is a double-edged tool.** Admin bypass is necessary for the tutorial-style direct push, but it also means you *can* bypass when you shouldn't. File the paper-trail issue every single time. No exceptions, even when the change is "obviously fine".
- **Don't push to a merged feature branch.** Open a new branch from main. The PR is closed; new commits are stranded.
- **Clarification rounds aren't failures.** If Claude Code flags ambiguity rather than guessing, that's the workflow doing what it's supposed to do. The cost of the clarification round is much lower than the cost of unwinding a wrong-direction PR.
- **Don't merge with red CI.** Ever. If CI is failing, the PR isn't ready, period. Branch protection should make this physically impossible, but if you've turned that off, hold yourself to the rule.
- **Cowork's review is advisory, not dispositive.**  The merge action is yours, and it's an admin bypass by design. If you ever scale to a team, replace the bypass with a real second identity (a bot account or a human collaborator) so the rails enforce themselves."

---

## Phase 8 — Pro-plan usage discipline

The Claude Pro plan has usage limits. Cowork and Claude Code share one usage pool. The dual-agent workflow is more usage-efficient than naive single-agent looping, but you can still burn through a session faster than you'd like if you're not deliberate.

A few habits that keep the loop sustainable.

### 8.1 Cowork burns usage faster than Chat

Cowork mode does more per turn than chat mode. Every Cowork response can include tool calls (GitHub MCP, file system, web fetch), each of which consumes more context and more compute than a plain chat reply. Don't be surprised when a one-hour Cowork session uses more of your daily quota than three hours of chat.

In practice this means:

- Don't leave Cowork open and idle. Tool calls don't fire on idle, but loaded MCP servers and lingering context can still draw down the usage allocation when the next turn fires with everything still attached.
- Be deliberate about long-running plans. If you ask Cowork to "do the next five stories end to end without stopping", you'll spend a session's budget on a single prompt's reasoning chain.

### 8.2 Sonnet by default, Opus when it matters

The Claude desktop app lets you switch models per conversation. The two relevant ones for this workflow are Sonnet (faster, cheaper, plenty smart for routine work) and Opus (slower, more expensive, deeper reasoning).

Routine work — status summaries, filing stories, post-merge cleanup, after-merge unblocks, simple PR reviews — runs perfectly well on Sonnet. Reserve Opus for moments where you actually need the extra reasoning depth:

- Architectural decisions that affect the whole codebase.
- Debugging a weird issue that's resisted ordinary investigation.
- Mid-project retrospectives where you're asking the agent to reason across many artifacts at once.
- Reviewing a particularly large or subtle PR.

The reflex of "always Opus to be safe" is expensive and rarely warranted. Default to Sonnet; switch to Opus when the situation specifically asks for it.

### 8.3 Don't run both agents at once

Cowork and Claude Code share usage. Running them in parallel doesn't get you twice the throughput; it gets you the same throughput at twice the burn rate. Worse, if one agent's actions depend on the other's (Claude Code waiting on a Cowork-answered clarification), parallel running creates race conditions in the issue state.

Run one at a time. Finish the current step, switch to the other tool, run its step. The serial pattern matches the workflow's actual data dependencies and keeps usage at one-thread cost.

### 8.4 Batch reviews and cleanup

If you've got two PRs in flight, don't do "open PR #1, switch to Cowork, review #1, switch back, open PR #2, switch to Cowork, review #2". Two context-switches into Cowork is two MCP-loaded session starts.

Better:

- Have Claude Code finish PRs #1 and #2 in one session.
- Switch to Cowork once and review both back-to-back in one prompt.

Same for after-merge cleanup. If you merge two PRs in a row, run a single after-merge cleanup prompt that handles both.

### Watch out for

- **The "let's just run another review for safety" trap.** If Cowork has already reviewed a PR, re-running the review prompt costs another full pass through the diff and `CLAUDE.md`. If you genuinely need a second look, ask a focused follow-up question instead of re-running the whole review.
- **Image-heavy prompts cost more.** Pasting screenshots into Cowork to ask "what's wrong with this UI" is fine, but expensive. Crop tightly and don't paste five when one would do.
- **Long-running plans amplify any planning error.** If you ask Cowork to do something in five steps and step two is wrong, you've paid for steps three through five before discovering it. Shorter prompts with checkpoints are usually cheaper than one long prompt.
- **Watch your hit pattern across days.** The Pro plan resets on a schedule. If you're consistently hitting the cap, adjust *what* you're asking for, not just *how often* — usually the answer is "do more of the routine on Sonnet".

---

## Phase 9 — The starter prompt library

After running through a few story cycles you'll notice you're typing the same shape of prompt over and over: "give me a status summary", "review PR #N", "answer the clarification on issue #M". Writing them fresh each time is a waste; saving them as templates is one of the highest-leverage moves in the whole workflow.

The starter library for this project is committed at the repo root: see [`prompts.md`](https://github.com/DinoD1971/transformer/blob/main/prompts.md).

### How the library is organized

`prompts.md` is split into two halves:

- **Cowork prompts.** Status summary, review PR, answer clarification, after-merge cleanup, unblock, file a follow-up story, retrospective, reorder backlog.
- **Claude Code prompts.** Pick up a story, address PR review feedback, resume after interruption, debug a CI failure, quick fix without an issue.

Each prompt is a copy-paste template with `[brackets]` marking the parts you customize per use. Re-read the file once when you set up the project, then refer back to it as needed.

### The two prompts that carry most of the weight

If you only memorize the shape of two, make them:

1. **Claude Code — Pick up a story.** This is the workhorse. It runs at the start of every implementation session. It encodes the entire pickup discipline: read `CLAUDE.md`, find the right issue, confirm understanding, raise ambiguities, wait for approval, implement only after the green light.

2. **Cowork — Review a pull request.** This is the gate. It runs every time Claude Code opens a PR. It encodes the review discipline: check every acceptance criterion, check every convention, evaluate test quality, formally approve or formally request changes.

Those two prompts together cover the majority of any active story cycle. The others are situational.

### When to add new prompts

Add a new prompt to the library when you've typed the same paragraph more than twice. Add it as a new section rather than rewriting an existing one. The categorization (Cowork vs. Claude Code) is by which agent the prompt is for; keep the file organized that way.

A useful trigger: every five to ten stories, run the *Cowork — Mid-project retrospective* prompt and ask whether any new prompts have emerged from the recurring patterns. Codify them while they're fresh.

### Watch out for

- **Don't put project-specific implementation details in the generic prompts.** "Pick up a story" is generic — it works for any project with the workflow. "Implement a new mapping feature in the transformer engine" is project-specific — it goes in the project-specific section at the bottom of `prompts.md`. Keeping the generic prompts portable means you can copy them to the next project's `prompts.md` with light edits.
- **Don't over-bake the prompts.** A prompt that's been edited fifty times to handle every edge case becomes a brittle procedure that the agent follows mechanically. Prompts work best when they describe the goal and the discipline, not every step. Trust the agent on the steps.
- **The prompts decay too.** Like `CLAUDE.md`, your prompt library will get stale. The fix is the same: when an agent's behavior surprises you, ask whether a prompt needs an update.

---

## What this taught us

Setting up this workflow on a real project (the `transformer` Azure Function in `DinoD1971/transformer`) and running stories through it produced a few observations that turned out to be more general than the specific tooling.

**1. The dual-agent pattern works because each agent stays in its lane.**
Claude Code writes code. Cowork plans and reviews. They do not chat with each other. They communicate exclusively through GitHub artifacts. That separation is what makes the review meaningful — Cowork's context comes from the diff and the issue, not from the implementation rationale that lived in Claude Code's session. The same pattern holds in human teams: a reviewer who was in the planning meeting is a worse reviewer than one who shows up cold to the diff.

**2. `CLAUDE.md` is the single most important artifact in the workflow.**
Every other piece of infrastructure — branch protection, CI, the prompt library, even the dual-agent split itself — is replaceable. `CLAUDE.md` isn't. It is the shared contract that lets two independent agent sessions converge on the same project. When `CLAUDE.md` is wrong, the workflow degrades quietly and the symptoms look like agent failures rather than process failures. When it's right, the rest of the system runs almost on its own.

**3. Branch protection is a forcing function, not a bureaucratic obstacle.**
Every workflow rule that enforces something has a moment where you wish you could just bypass it. That's the moment the rule is most valuable. The friction of "I have to open a PR for a one-line change" is exactly the friction that prevents thirty one-line changes from accumulating into an unreviewed merge. When you find yourself wanting to disable a rail, the right question isn't "is this rail too restrictive" but "what is the rail catching that I'm trying to ship around".

**4. Filing the whole backlog up front beats filing one story at a time.**
The temptation is to file story 1, work it, file story 2, work it. The cost of that pattern compounds. Filing twelve stories in one session takes ninety minutes; filing them one-at-a-time across twelve sessions takes eight hours and surfaces dependencies in the wrong order. The bulk-filing pattern also forces you to think about the project as a whole, which is when you catch the scope problems that would otherwise show up in week three.

**5. The validator pattern generalizes beyond this specific use.**
"One agent does the work, a different agent (or human) checks the work against an explicit standard" is the shape of the dual-agent workflow here, but it's also the shape of code review, of contract inspection, of audit. The reason it works isn't that two agents are smarter than one; it's that having a separate context for the check forces the check to actually happen. If you find yourself building any agent-driven process where one component generates and another component evaluates, the lessons here transfer: write the standard down, route generation and evaluation through different sessions, gate the irreversible action behind an explicit decision.

**6. The friction is the feature.**
Every "watch out for" in this guide is a moment where the system pushed back against doing something the easy way. The Microsoft Store install path is annoying. Branch protection on a fresh branch is annoying. Filing a paper-trail issue after an admin bypass is annoying. CI failing on the first push is annoying. None of those frictions are bugs to be smoothed away; they are the system telling you the thing you almost did was a mistake. Tutorials that hide the friction produce demos; tutorials that document it produce working systems.

**7. The agents don't get smarter; the project gets clearer.**
At the start, the agents need a lot of clarification. By story ten, they're picking up issues and producing PRs that pass review on the first round. The agents haven't changed — `CLAUDE.md` has, the issue templates have, your prompts have, your sense of what a "well-specified story" looks like has. The leverage in this workflow is in the artifacts, not in the model. Invest accordingly: every minute spent improving `CLAUDE.md` and `prompts.md` saves multiples downstream.

---

## Appendix A — Sample CLAUDE.md interview answers (Phase 6)

This appendix documents the actual answers given during the interview that produced the `CLAUDE.md` for the `transformer` project. The intent is to show, concretely, the level of specificity each section needs. A good `CLAUDE.md` is opinionated and committal — vague answers produce vague conventions, which produce drift.

The interview was driven by Cowork in Ask mode, one question at a time. The answers below are the human's responses, lightly cleaned up. Each section corresponds to a heading that ended up in `CLAUDE.md`.

### 1. Project Overview

> A general-purpose data normalization layer sitting between upstream sources (operational systems, APIs, ingestion pipelines) and downstream consumers (data warehouse, APIs, apps). Called by ingestion workflows or event triggers; transforms semi-structured or heterogeneous payloads into structured, schema-aligned data.

### 2. Tech Stack

- **Runtime:** .NET 8
- **Functions:** Azure Functions v4, Isolated Worker model
- **Hosting:** Flex Consumption hosting plan
- **JSON:** `System.Text.Json` for all JSON handling
- **Logging:** Built-in `ILogger` + Application Insights for telemetry
- **No external service calls, no secrets management, no extra NuGet dependencies for v1**

### 3. Project Structure

```
transformer/
├── src/
│   └── Transformer/
│       ├── Functions/
│       ├── Services/
│       ├── Models/
│       ├── Configs/
│       │   └── {Domain}/
│       │       └── {Operation}/
│       │           └── {configName}.json
│       └── Program.cs
├── tests/
│   └── Transformer.Tests/
├── transformer.sln
└── CLAUDE.md
```

### 4. Configuration Format

- Embedded JSON files, versioned with code
- Selected via URL route: `POST /api/transform/{domain}/{operation}/{configName}`
- Config path mirrors route: `Configs/{Domain}/{Operation}/{configName}.json`
- Full feature surface (see the example transformation config below): direct mapping, nested objects, type conversion, defaults, validation, transform functions (trim, round, contains, now), conditional logic, inline expressions, lookups/enum mapping, array transforms with item-level mapping, static value injection, post-processing steps, error handling strategies

#### Example transformation config (the "feature surface" reference)

This single example was used during the interview to communicate the full feature surface the engine needed to support. Every individual capability in this file became one or more stories in the backlog (see [Appendix B](#appendix-b--sample-v1-backlog-15-issues-filed-phase-7)).

```json
{
  "version": "1.0",
  "description": "Transform inbound order payload into normalized order model",

  "source": {
    "type": "json",
    "rootPath": "$"
  },

  "target": {
    "type": "json",
    "rootObject": "order"
  },

  "settings": {
    "ignoreNulls": true,
    "dateFormat": "yyyy-MM-ddTHH:mm:ssZ",
    "culture": "en-US"
  },

  "mappings": [
    {
      "target": "orderId",
      "source": "$.id",
      "type": "string"
    },
    {
      "target": "customer.name",
      "source": "$.customer.full_name",
      "type": "string",
      "transform": "trim"
    },
    {
      "target": "customer.email",
      "source": "$.customer.email",
      "type": "string",
      "validate": {
        "regex": "^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$",
        "onFail": "null"
      }
    },
    {
      "target": "customer.isVip",
      "source": "$.customer.tags",
      "type": "boolean",
      "transform": "contains",
      "parameters": {
        "value": "VIP"
      }
    },
    {
      "target": "orderDate",
      "source": "$.created_at",
      "type": "datetime",
      "format": "yyyy-MM-ddTHH:mm:ssZ"
    },
    {
      "target": "totalAmount",
      "source": "$.total",
      "type": "decimal",
      "transform": "round",
      "parameters": {
        "precision": 2
      }
    },
    {
      "target": "currency",
      "source": "$.currency",
      "type": "string",
      "default": "USD"
    },
    {
      "target": "status",
      "source": "$.status",
      "type": "string",
      "lookup": {
        "pending": "Pending",
        "paid": "Completed",
        "failed": "Cancelled"
      }
    },
    {
      "target": "shipping.address.line1",
      "source": "$.shipping.address1"
    },
    {
      "target": "shipping.address.city",
      "source": "$.shipping.city"
    },
    {
      "target": "shipping.address.postalCode",
      "source": "$.shipping.zip"
    },
    {
      "target": "items",
      "source": "$.line_items",
      "type": "array",
      "itemMapping": {
        "sku": "$.sku",
        "name": "$.name",
        "quantity": "$.qty",
        "unitPrice": "$.price",
        "lineTotal": {
          "expression": "$.qty * $.price"
        }
      }
    },
    {
      "target": "discountAmount",
      "type": "decimal",
      "condition": {
        "if": "$.discount != null",
        "then": "$.discount.amount",
        "else": 0
      }
    },
    {
      "target": "isHighValue",
      "type": "boolean",
      "expression": "$.total > 1000"
    },
    {
      "target": "metadata.sourceSystem",
      "value": "Shopify"
    },
    {
      "target": "metadata.processedAt",
      "type": "datetime",
      "transform": "now"
    }
  ],

  "postProcessing": [
    {
      "type": "removeEmptyObjects"
    },
    {
      "type": "sortArray",
      "target": "items",
      "by": "name"
    }
  ],

  "errorHandling": {
    "onMissingField": "ignore",
    "onTypeMismatch": "coerce",
    "onError": "log"
  }
}
```

### 5. API Contract

- **Route:** `POST /api/transform/{domain}/{operation}/{configName}`
- **Request envelope:** `{ "correlationId": "...", "payload": { } }`
- **Response envelope:** `{ "correlationId": "...", "domain": "...", "operation": "...", "configName": "...", "processedAt": "...", "payload": { } }`
- **Errors:** RFC 7807 ProblemDetails
- **Status codes:** 200, 400, 404, 415, 500
- **No versioning for v1**

### 6. Coding Conventions

- PascalCase for classes, methods, properties, file names
- `_camelCase` for private fields
- camelCase for JSON serialization
- I-prefixed interfaces (`IConfigLoader`, `ITransformationEngine`)
- kebab-case for config file names
- async/await end-to-end, `CancellationToken` threaded through, no `async void`
- DI via `IServiceCollection` in `Program.cs`, interface→implementation, Singleton lifetime for stateless services
- Global middleware exception handler
- Typed exceptions: `ConfigNotFoundException`, `TransformationException`
- No swallowed exceptions

### 7. Testing

- xUnit + Moq
- Unit tests only for v1; integration tests deferred
- 80% coverage target enforced

### 8. Deployment

- **Resource naming pattern:** `rg-transformer-{env}`, `func-transformer-{env}`, `sttransformer{env}`, `appi-transformer-{env}`
- **`dev` environment only for now**
- **Configuration:** environment variables / Azure App Settings only — no Key Vault for v1

### 9. Workflow

- **Labels:** `status:ready`, `status:in-progress`, `status:needs-clarification`, `status:in-review`, `status:qa`, `status:done`, `status:blocked` + `type:feature`, `type:bug`, `type:chore`
- **Pull-based:** Claude Code scans for `status:ready` issues, picks the highest priority, moves it to `status:in-progress`
- **Fully autonomous:** Claude Code implements → opens PR → CI runs → Cowork reviews → human approves → merges → closes issue → moves to `status:done`

### What this list demonstrates

A few patterns worth lifting into your own interview:

- **Every section gets a concrete answer.** No "we'll figure that out later" entries. If something is genuinely undecided, the entry is "Decided: not in scope for v1" — explicit deferral, not vague absence.
- **Naming patterns are spelled out, not described.** "PascalCase for classes" is enforceable; "follow standard C# naming" is not.
- **The deployment section is short on purpose.** v1 is dev-only with App Settings. The terseness is honest; padding it with hypotheticals about prod / staging / Key Vault would create a doc that lies about the project's actual state.
- **The example config in section 4 does most of the work for the engine spec.** Pointing at a single richly-annotated example is more efficient than writing prose about each feature.

When you do this interview for your own project, a useful test: after you finish, hand `CLAUDE.md` to someone who wasn't in the room and ask them to predict three architectural choices the agents will make. If they can predict accurately, the doc is doing its job. If they can't, the doc has gaps.

---

## Appendix B — Sample v1 backlog: 15 issues filed (Phase 7)

This appendix lists the 15 issues filed for the `transformer` v1 backlog. Each was created by Cowork during a single Phase 7 session, in the order shown, so issue numbers match the dependency graph (issue #1 has no dependencies; issue #15 depends transitively on most prior issues).

The structure of each issue follows the Story template (`.github/ISSUE_TEMPLATE/story.md`): Title, Labels, Blocked by (where applicable), Context, Acceptance Criteria, Technical Notes, Out of Scope. Use these as a reference for how stories should be sized and specified for this workflow.

The full conversation that produced this backlog: Cowork was given the example transformation config (Appendix A, section 4) and asked to propose a 15-story breakdown that would deliver the full feature surface in dependency order. The result was three foundation stories, three core engine stories, seven independent feature stories, and two polish stories.

### Issue 1

- **Title:** `chore: scaffold .NET solution and projects`
- **Labels:** `status:ready`, `type:chore`
- **Context:** First implementation task. Per `CLAUDE.md` "Project Structure," create the foundational solution before any features.
- **Acceptance Criteria:**
  - `transformer.sln` at repo root
  - `src/Transformer/Transformer.csproj` targeting .NET 8, Azure Functions v4 isolated worker
  - `tests/Transformer.Tests/Transformer.Tests.csproj` using xUnit + Moq, references main project
  - Solution builds with `dotnet build` (no errors)
  - Empty smoke test in tests project passes
  - Folders `Functions/`, `Services/`, `Models/`, `Configs/` exist under `src/Transformer/` (with `.gitkeep` files)
  - `Program.cs` with isolated worker host setup, ApplicationInsights, empty DI placeholder
  - `host.json` and `local.settings.json` exist (`local.settings.json` gitignored)
  - `.gitignore` for .NET / Azure Functions at repo root
  - CI passes
- **Technical Notes:** Use `dotnet new` and `func` templates. No transformation logic yet.
- **Out of Scope:** Any transformation logic, HTTP functions, config files, deployment.

### Issue 2

- **Title:** `feat: implement HTTP function with route and request/response envelopes`
- **Labels:** `status:blocked`, `type:feature`
- **Blocked by:** #1
- **Context:** Per `CLAUDE.md` "API Contract," implement the HTTP entry point with route binding and request/response envelopes. No transformation logic yet — pass payload through unchanged.
- **Acceptance Criteria:**
  - HTTP-triggered function at route `POST /api/transform/{domain}/{operation}/{configName}`
  - Request envelope model: `{ correlationId, payload }`
  - Response envelope model: `{ correlationId, domain, operation, configName, processedAt, payload }`
  - Returns 200 with response envelope (passthrough payload) for valid requests
  - Returns 400 ProblemDetails if request envelope malformed
  - Returns 415 ProblemDetails if Content-Type not application/json
  - camelCase JSON serialization
  - `CancellationToken` threaded through
  - `ILogger` logs `correlationId` on request entry/exit
  - Unit tests cover happy path + 400 + 415
- **Out of Scope:** Config loading, actual transformation, full error middleware.

### Issue 3

- **Title:** `feat: implement config loader with embedded JSON files`
- **Labels:** `status:blocked`, `type:feature`
- **Blocked by:** #2
- **Context:** Per `CLAUDE.md` "Configuration Format," configs are embedded JSON files at `src/Transformer/Configs/{Domain}/{Operation}/{configName}.json`. Implement loading, caching, and parsing.
- **Acceptance Criteria:**
  - `IConfigLoader` interface and `ConfigLoader` implementation
  - Loads config by `(domain, operation, configName)` from embedded resources or filesystem
  - In-memory cache (singleton lifetime) keyed by full path
  - Returns parsed config model on success
  - Throws `ConfigNotFoundException` if file missing
  - Throws `ConfigParseException` if JSON invalid
  - Function returns 404 ProblemDetails on `ConfigNotFoundException`, 500 on parse errors
  - Sample config at `Configs/Sample/Echo/passthrough.json` (empty mappings array — passes payload through)
  - Unit tests cover: found, not found, invalid JSON, cache hit
- **Out of Scope:** Validating config schema beyond JSON parse, transformation execution.

### Issue 4

- **Title:** `feat: implement direct field mapping with JSONPath`
- **Labels:** `status:blocked`, `type:feature`
- **Blocked by:** #3
- **Context:** Core engine. Implement `mappings[]` with `source` (JSONPath) → `target` (dotted path) for string values. Foundation for all subsequent transformation features.
- **Acceptance Criteria:**
  - `ITransformationEngine` interface, `TransformationEngine` implementation
  - Reads `source` JSONPath from input payload
  - Writes to `target` (supports nested dotted paths like `customer.name` and `shipping.address.city`)
  - Returns transformed payload as JSON object
  - HTTP function calls engine when config has mappings
  - Sample config at `Configs/Sample/Order/normalize.json` demonstrating direct mappings
  - Unit tests: simple mapping, nested target, missing source field, deeply nested target
- **Technical Notes:** Use `System.Text.Json` `JsonNode` for traversal/mutation. JSONPath via `JsonPath.Net` NuGet or equivalent — confirm choice in implementation plan.
- **Out of Scope:** Type conversion, defaults, transforms, validation, arrays, conditionals, expressions.

### Issue 5

- **Title:** `feat: add type conversion (string, decimal, integer, boolean, datetime)`
- **Labels:** `status:blocked`, `type:feature`
- **Blocked by:** #4
- **Context:** Per the config spec, mappings have a `type` field. Implement coercion plus the `errorHandling.onTypeMismatch` modes (`coerce`, `error`, `null`).
- **Acceptance Criteria:**
  - Supports `type` values: `string`, `decimal`, `integer`, `boolean`, `datetime`
  - Datetime parsing respects `settings.dateFormat`
  - `onTypeMismatch: coerce` attempts best-effort conversion
  - `onTypeMismatch: error` throws `TransformationException`
  - `onTypeMismatch: null` writes null on failure
  - Unit tests cover each type, each onTypeMismatch mode, edge cases (empty string to decimal, etc.)
- **Out of Scope:** Defaults, transforms, validation.

### Issue 6

- **Title:** `feat: add default values and ignoreNulls setting`
- **Labels:** `status:blocked`, `type:feature`
- **Blocked by:** #5
- **Context:** Implement the `default` field on mappings and the `settings.ignoreNulls` behavior.
- **Acceptance Criteria:**
  - If source is null/missing and `default` is set, use the default value (with type conversion)
  - If `settings.ignoreNulls: true`, omit null target fields entirely from output
  - If `settings.ignoreNulls: false`, write nulls explicitly
  - Unit tests: default applied, default with type conversion, ignoreNulls true vs false
- **Out of Scope:** Transforms, validation.

### Issue 7

- **Title:** `feat: add transform functions (trim, round, contains, now)`
- **Labels:** `status:blocked`, `type:feature`
- **Blocked by:** #6
- **Context:** Implement the `transform` field with `parameters` for built-in functions.
- **Acceptance Criteria:**
  - `trim` — string trim, no parameters
  - `round` — decimal round, `parameters.precision` (int)
  - `contains` — boolean check, `parameters.value` (any) returns true if source array contains value
  - `now` — datetime current UTC, no parameters (ignores source)
  - Extensible registry pattern (`ITransformFunction` interface) so new transforms can be added without modifying core engine
  - Unit tests for each function plus an unknown-transform case (should throw `TransformationException`)
- **Out of Scope:** Validation, conditionals, expressions.

### Issue 8

- **Title:** `feat: add validation with regex and onFail behavior`
- **Labels:** `status:blocked`, `type:feature`
- **Blocked by:** #7
- **Context:** Implement the `validate` block with `regex` and `onFail` modes (`null`, `error`, `default`).
- **Acceptance Criteria:**
  - Regex validation runs after type conversion, before transforms
  - `onFail: null` writes null on validation failure
  - `onFail: error` throws `TransformationException`
  - `onFail: default` writes the mapping's default value
  - Unit tests cover each onFail mode and a regex-matched success case
- **Out of Scope:** Conditionals, lookups.

### Issue 9

- **Title:** `feat: add lookup / enum mapping`
- **Labels:** `status:blocked`, `type:feature`
- **Blocked by:** #8
- **Context:** Implement the `lookup` field — a key/value object that maps source values to target values.
- **Acceptance Criteria:**
  - Lookup applied after type conversion
  - If source value is a key in lookup, write the mapped value
  - If source value is not a key, behavior depends on `errorHandling.onMissingField` (`ignore` writes source as-is, `error` throws, `null` writes null)
  - Unit tests: hit, miss with each `onMissingField` mode
- **Out of Scope:** Conditionals.

### Issue 10

- **Title:** `feat: add conditional logic (if/then/else)`
- **Labels:** `status:blocked`, `type:feature`
- **Blocked by:** #9
- **Context:** Implement the `condition` block on mappings with `if`, `then`, `else`. The `if` is a JSONPath expression evaluating to a boolean.
- **Acceptance Criteria:**
  - `if` supports basic comparison expressions: `==`, `!=`, `>`, `<`, `>=`, `<=`, `!= null`, `== null`
  - `then` and `else` can be either JSONPath references or literal values
  - Result of selected branch undergoes normal type conversion / defaults
  - Unit tests: each comparison, true branch, false branch, nested condition
- **Technical Notes:** Confirm expression parser approach in implementation plan — likely a small custom parser or a vetted library. Document choice.
- **Out of Scope:** Inline arithmetic expressions (#11).

### Issue 11

- **Title:** `feat: add inline expressions for math and comparisons`
- **Labels:** `status:blocked`, `type:feature`
- **Blocked by:** #10
- **Context:** Implement the `expression` field on mappings — supports arithmetic and comparison over JSONPath references.
- **Acceptance Criteria:**
  - Supports `+`, `-`, `*`, `/`, `%` arithmetic
  - Supports `>`, `<`, `>=`, `<=`, `==`, `!=` comparisons returning boolean
  - Operands can be JSONPath references or numeric literals
  - Result type honors mapping's `type` field
  - Unit tests cover each operator, mixed operations, JSONPath operands, missing operand handling
- **Technical Notes:** Reuse / extend the parser from #10 if practical.

### Issue 12

- **Title:** `feat: add array transformation with item-level mapping`
- **Labels:** `status:blocked`, `type:feature`
- **Blocked by:** #11
- **Context:** Implement `type: array` with `itemMapping`. Maps each element of a source array using a per-item mapping spec.
- **Acceptance Criteria:**
  - Source must resolve to a JSON array (else error per `onTypeMismatch`)
  - `itemMapping` defines target field shapes for each item
  - Per-item mapping can use all previously implemented features (direct mapping, type conversion, transforms, conditionals, expressions)
  - Output is a JSON array of mapped items
  - Empty source array produces empty target array
  - Unit tests: simple item mapping, item with expression (e.g., `lineTotal = qty * price`), nested objects within items, empty array
- **Out of Scope:** Sorting (covered in #14).

### Issue 13

- **Title:** `feat: add static value injection`
- **Labels:** `status:blocked`, `type:feature`
- **Blocked by:** #12
- **Context:** Implement the `value` field on mappings — writes a static literal to target with no source lookup.
- **Acceptance Criteria:**
  - `value` accepts string, number, boolean, null
  - Static value undergoes type conversion per `type` field
  - Works at any target path depth
  - Unit tests: each literal type, with type conversion, at nested target
- **Out of Scope:** Templated values (not in scope for v1).

### Issue 14

- **Title:** `feat: add post-processing steps (removeEmptyObjects, sortArray)`
- **Labels:** `status:blocked`, `type:feature`
- **Blocked by:** #13
- **Context:** Implement the `postProcessing[]` array. Steps run sequentially after all mappings complete.
- **Acceptance Criteria:**
  - `removeEmptyObjects` — recursively removes any object with zero non-null/non-empty fields
  - `sortArray` — sorts a target array by a `by` field (string comparison, ascending)
  - Extensible registry pattern (`IPostProcessingStep` interface) for future additions
  - Unit tests: each step, multiple steps in sequence, target path resolution for `sortArray`
- **Out of Scope:** Other post-processing types.

### Issue 15

- **Title:** `feat: implement global error handling and ProblemDetails responses`
- **Labels:** `status:blocked`, `type:feature`
- **Blocked by:** #14
- **Context:** Per `CLAUDE.md` "Coding Conventions," implement the global middleware exception handler and ensure all error paths return RFC 7807 ProblemDetails.
- **Acceptance Criteria:**
  - Middleware catches all unhandled exceptions
  - Maps known exception types to status codes:
    - `ConfigNotFoundException` → 404
    - `ConfigParseException` → 500
    - `TransformationException` → 422
    - `ArgumentException` / model binding failures → 400
    - Unhandled → 500
  - Response body is RFC 7807 ProblemDetails with `type`, `title`, `status`, `detail`, plus a `correlationId` extension
  - Logs full exception with `correlationId` at appropriate level (warn for 4xx, error for 5xx)
  - No exception details leak into 500 responses (just `correlationId` for support reference)
  - Unit tests cover each mapping
  - Existing functions and tests still pass
- **Out of Scope:** Retry policies, circuit breakers.

### What this backlog demonstrates

A few patterns worth lifting into your own backlog:

- **Foundation stories first, in strict order.** Issues #1–#3 (scaffold, HTTP shell, config loader) establish everything subsequent stories depend on. They're small, mechanical, and unblock the rest of the project.
- **The earliest possible "it transforms something" milestone.** Issue #4 is the smallest possible story that produces a working transformation. After it merges, you have a usable v0 — every subsequent story is additive.
- **Independent feature stories use parallel structure.** Issues #5–#13 are mostly independent of each other (only the dependency on the engine in #4 binds them). Their structure is deliberately uniform: each adds one mapping-level feature with the same "context, criteria, tests" shape. Uniformity makes them faster to file, faster to pick up, and easier to review.
- **Polish stories last, on purpose.** Issues #14 and #15 are post-processing and error handling — work that touches the whole engine and benefits from being done after the engine is feature-complete.
- **Every story has explicit "Out of Scope."** This is the section that prevents scope creep mid-implementation. Without it, an agent picking up issue #4 might be tempted to add type conversion "while it's in there." With it, the agent stays bounded.
- **Acceptance criteria are testable.** Every bullet under Acceptance Criteria can be turned into a checkbox or a test assertion. "Unit tests cover X, Y, Z" is a checkbox; "be well-tested" isn't.
- **Technical Notes are short and pointed.** They flag the few decisions worth surfacing in the implementation plan (parser library choice, JSONPath library choice). Everything else is left to the agent and reviewed in the PR.

The total time to file all 15 in one Cowork session: about 15 minutes. The total time to file them one-at-a-time across 15 sessions would have been an order of magnitude more, with worse dependency hygiene. This is the bulk-filing pattern from Phase 7.1 in concrete form.
