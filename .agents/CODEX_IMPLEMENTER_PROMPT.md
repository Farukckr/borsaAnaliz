# Codex implementer prompt

Read `.agents/PLAN.md` and implement it.

## Preflight

- Check the plan's Status field. Implement only if it is `ready` or `in-progress`. If it is `placeholder`, `done`, or `blocked`, stop and tell the user.
- If Status is `in-progress`, a previous run was interrupted: skip steps already checked off and continue from the first unchecked step.
- If the repo is a git repository with uncommitted changes, recommend committing before large changes.
- Read the files named in the plan before editing.
- Identify existing patterns to preserve.
- If the plan is contradictory, unsafe, or missing a blocking decision, ask before editing.
- Set Status to `in-progress` when you start.

## Implementation rules

- Follow the plan closely.
- Do not expand the scope.
- Keep edits small and complete.
- Use existing dependencies and style unless the plan says otherwise.
- Avoid unrelated refactors.
- Preserve user changes.
- Add comments only when they clarify non-obvious logic.
- Check off each step in Proposed Changes as you complete it.
- If the plan has phases, implement only the current phase unless the user says otherwise.
- If you hit a real blocker, stop, set Status to `blocked`, and describe the blocker in the Implementation Report so the planner can replan.

## Verification

- Run the verification commands listed in the plan when possible.
- If no commands exist, do the smallest useful manual/static check.
- If verification cannot run, explain why.

## Wrap-up

- Fill in the Implementation Report section of `.agents/PLAN.md`.
- Set Status to `done` (all acceptance criteria met), `in-progress` (phase complete, more phases remain), or `blocked`.

## Final response

Include:

- What changed.
- Files changed.
- Verification performed.
- Any remaining risk or follow-up.

For tiny direct user edits, a separate plan is not required.
