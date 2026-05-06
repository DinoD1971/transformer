# Prompt Library

Reusable prompts for the Cowork + Claude Code workflow on the `transformer` project.

Each prompt is a template. The `[brackets]` are the parts you customize per use. Copy, swap the brackets, paste.

**Most-used prompts** (memorize the shape of these two):
- [Claude Code — Pick up a story](#claude-code--pick-up-a-story)
- [Cowork — Review a pull request](#cowork--review-a-pull-request)

---

## Cowork prompts

These are pasted into Cowork (Agent mode, GitHub MCP enabled, ideally on Sonnet to conserve usage).

### Cowork — Status summary

Quick standup-style read at the beginning of a work session.

```
Give me a status summary of DinoD1971/transformer:
1. Current open issues by label (counts)
2. The single issue currently in status:ready (if any)
3. Any open PRs and their state (in-review, approved, CI failing, etc.)
4. Anything in status:needs-clarification I owe answers on
5. Recommended next action
```

### Cowork — Review a pull request

Run this every time Claude Code opens a PR.

```
Review pull request #[N] in DinoD1971/transformer. Read:
- The full diff
- The linked issue's acceptance criteria
- Relevant sections of CLAUDE.md

Evaluate:
- Does the PR satisfy every acceptance criterion?
- Does the code follow the conventions in CLAUDE.md (naming, async patterns, DI, logging, exception handling)?
- Are tests present, meaningful (not just trivially passing), and aligned with our 80% coverage target?
- Are there any acceptance criteria CLAUDE.md says we need that aren't covered?
- Are there any code smells or architectural concerns I should flag before merging?

If it passes: submit a formal approving review on the PR with a brief summary comment listing what passed.

If it fails: post specific change requests as review comments at the relevant lines. Flip the issue label from status:in-review back to status:in-progress. Comment on the issue explaining what needs to change.
```

### Cowork — Answer clarification questions

Run when Claude Code has flagged a `status:needs-clarification` and posted questions.

```
Find issues labeled status:needs-clarification in DinoD1971/transformer. For each one:
1. Read the latest comment containing Claude Code's questions
2. Draft answers grounded in CLAUDE.md and the issue's existing acceptance criteria
3. If the answer requires a CLAUDE.md update, flag that to me before posting
4. Post answers as a reply comment
5. Flip the label back to status:in-progress
```

### Cowork — After-merge cleanup

Run once per merge to handle issue closure, label tidying, and unblocking the next story.

```
PR #[N] in DinoD1971/transformer was just merged. Please do the standard post-merge cleanup:
1. Verify issue #[M] auto-closed (close manually if not)
2. Update the closed issue's labels: remove status:in-review, add status:done
3. Find any status:blocked issues whose dependencies are now satisfied
4. For each: remove status:blocked, add status:ready, post a comment confirming the unblock
5. Report back with: which issue closed, which issues unblocked, which (if any) remain blocked and why
```

### Cowork — Unblock a specific story

Use this if you want fine-grained control over which story unblocks next.

```
Issue #[N] in DinoD1971/transformer is closed and merged. Please:
1. Find the next blocked issue whose dependency on #[N] is now satisfied
2. Verify all its "Blocked by" references are now closed
3. Remove the status:blocked label, add status:ready
4. Post a comment confirming the unblock and noting which predecessor was just completed
5. If the now-ready story has multiple predecessors and not all are done yet, leave it blocked and tell me which other issues still need to close
```

### Cowork — File a follow-up story

Use this when something comes up mid-flow that needs its own story.

```
File a new story in DinoD1971/transformer using the Story issue template.

Title: [type]: [short description]

Context: [paragraph explaining why this work matters]

Acceptance Criteria:
- [ ] [criterion]
- [ ] [criterion]

Technical Notes:
[any specifics on files, libraries, patterns]

Out of Scope:
[things this story doesn't cover]

Apply labels status:[ready|blocked] and type:[feature|bug|chore]. If blocked, list the blockers.
```

### Cowork — Mid-project retrospective

Run every 5–7 stories or when something feels off.

```
We've completed [N] stories so far. Look at:
1. Closed issues in the last 2 weeks
2. CLAUDE.md
3. The structure of the codebase

Tell me:
- What's working well in the workflow
- What's not (e.g., recurring clarification questions, repeated reviews finding the same issues, conventions drifting)
- Any CLAUDE.md updates that would prevent the recurring issues
- Any process changes worth considering
```

### Cowork — Reorder the backlog

Use when you decide to change story priority.

```
Currently issue #[A] is status:ready and issue #[B] is status:blocked. I want to reverse this — work on #[B] first.

Please:
1. Verify #[B]'s blockers (if any) are actually satisfiable now
2. Move #[A] back to status:blocked (or pause it if it's already in progress — ask me first)
3. Move #[B] to status:ready
4. Post comments on both explaining the reorder
```

---

## Claude Code prompts

These are pasted into Claude Code in your terminal, typically with the project repo as the working directory.

### Claude Code — Pick up a story

The workhorse prompt. Use at the start of every implementation session.

```
Read CLAUDE.md at the project root in full.

Then list issues with `gh issue list --label "status:ready"`. Pick up the highest-priority status:ready issue. Read its full body with `gh issue view <number>`.

Before writing any code:
1. Confirm the issue title and acceptance criteria back to me as a checklist
2. Outline your implementation plan step by step
3. Identify any ambiguities. If you find any, post them as a comment on the issue using `gh issue comment`, switch the label from status:ready to status:needs-clarification using `gh issue edit`, and stop. Do NOT proceed on assumptions.
4. If everything is clear, wait for my approval before starting implementation.

Once I approve:
1. Switch the label from status:ready to status:in-progress
2. Create a branch named [type]/[issue-number]-[short-slug]
3. Implement following CLAUDE.md exactly. Run `dotnet build` and `dotnet test` after each significant change
4. Commit with conventional commit format. The body must include `Closes #[N]` on its own line
5. Push the branch
6. Open a PR via `gh pr create` filling out the PR template
7. Switch the label to status:in-review
8. Report back with the PR URL

Rules:
- No direct pushes to main
- No skipping the tests required by acceptance criteria
- No global tool installs without telling me first
- If a build/test fails and you can't figure out why after one or two attempts, stop and ask
```

### Claude Code — Address PR review feedback

Use when Cowork has requested changes on a PR.

```
PR #[N] has review comments requesting changes. Run `gh pr view [N] --comments` to read all of them.

For each requested change:
1. Make the change in code
2. Run `dotnet build` and `dotnet test` to verify
3. Reply to the specific review comment confirming what you changed and why

When all review comments are addressed:
1. Commit with a message like `fix: address review feedback for #[M]`
2. Push to the same branch
3. The PR will update automatically
4. Switch the issue label from status:in-progress back to status:in-review
5. Comment on the PR summarizing the changes made
6. Report back
```

### Claude Code — Resume after interruption

Use if a session got cut off (token limits, you walked away, etc.).

```
I was previously working on issue #[N] on branch [branch-name]. Pick up where we left off:
1. Run `git status` and `git log --oneline -5` to see current state
2. Run `gh issue view [N]` to re-read the issue
3. Re-read any relevant sections of CLAUDE.md
4. Run `dotnet build` and `dotnet test` to confirm current state
5. Tell me where we are and what's left to do per the acceptance criteria
6. Wait for me to confirm before proceeding
```

### Claude Code — Debug a CI failure

Use when CI fails on a PR.

```
PR #[N] has failing CI. Run `gh pr checks [N]` and `gh run view --log-failed` for the most recent failed run.

1. Identify the root cause from the logs
2. Reproduce locally if possible (`dotnet build`, `dotnet test`)
3. Tell me what you found and your proposed fix before making changes
4. After I approve, fix it, push, and confirm CI passes
```

### Claude Code — Quick fix without an issue

Use for a small change that doesn't need a new issue (e.g., docs typo, dependency bump).

```
[Describe the change]

Make the change directly. Run build and tests. Commit with a conventional commit message. Open a PR if branch protection requires it; otherwise a direct commit to main is okay if the change is trivially safe.

If you're unsure whether this needs an issue first, stop and ask.
```

---

## Usage notes

**Most prompts have a Cowork half and a Claude Code half.** A typical story cycle looks like this:

1. *Cowork — Status summary* (start of session)
2. *Claude Code — Pick up a story* (in terminal)
3. Claude Code asks clarifying questions → *Cowork — Answer clarification questions*
4. Claude Code opens PR → *Cowork — Review a pull request*
5. Cowork requests changes → *Claude Code — Address PR review feedback*
6. PR approved, you click merge on GitHub
7. *Cowork — After-merge cleanup*
8. Repeat.

**Default to Sonnet.** All Cowork prompts above run fine on Sonnet. Reserve Opus for moments where deeper reasoning is genuinely needed (architectural decisions, debugging weird issues, retrospectives).

**Don't run both tools at once.** Cowork and Claude Code share usage limits. Let one finish before starting the other.

**Update this file as you learn.** Once you've run 5–10 stories you'll discover patterns worth codifying. Add them as new sections rather than rewriting the existing ones.

**Project-specific prompts go below.** As the transformer engine grows, you'll want prompts like "implement a new mapping feature" that capture project conventions. Reserve a section for those at the end of this file.

---

## Project-specific prompts

*(Empty for now. Add transformer-specific prompts here as you discover them.)*
