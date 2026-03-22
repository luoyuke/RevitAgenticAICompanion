# Revit Agentic AI Companion

Revit Agentic AI Companion is a hobby Revit 2026 add-in that lets you talk to your model like it’s a coworker… except this coworker can write code, run transactions, and occasionally make questionable life choices.It is a dockable chat pane with bounded Revit execution. It lets Codex inspect the live model, propose generated C# actions, and execute approved edits through host-owned Revit requests.

It’s past proof-of-concept and already does real work — but it’s still very much an experiment. It now supports multi-step inspection-first planning, read-only BIM queries, bounded write proposals, preview and approval before writes, and local audit/artifact review after each run.

If your model gets nuked, tell me so I can also laugh about it.

## What it can do

- answer conversational prompts inside Revit
- inspect document, view, selection, linked-model, and parameter context
- run up to 3 read-only probes before proposing a write
- generate and compile C# against real Revit references
- preview bounded edits before approval
- execute approved writes inside host-owned Revit transactions
- analyze failed write executions and failed read-only probes automatically

## Current design

The host owns Revit access and execution. Codex owns planning.

Host responsibilities:
- capture context
- run `ExternalEvent` requests
- compile, validate, preview, and execute
- keep approval and confirm gates
- persist artifacts, audit, and user memory

Codex responsibilities:
- interpret prompts
- request more evidence when needed
- decide between reply, read-only query, inspection probe, or action proposal
- generate corrected follow-up plans after failures

## Memory and audit

The current memory model is intentionally small:

- Codex thread continuity for short conversational context
- `memory.md` for cross-project user preferences only
- `audit.db` as a ledger, not retrieval memory

## Build

## Install

## Repo layout

## Notes

- project-specific memory is intentionally disabled for now
- failures are expected during exploration; the runtime is built to keep them reviewable
- artifact folders are the best place to inspect a single run in detail

Why this exists
Originally this started as:
- “How badly can an agentic AI mess up a Revit model?”
Turns out…  
it can do some genuinely useful BIM work and might refuse your task due to your bad engineering practices.  
So now it’s both:
a chaos experiment and a glimpse of what future BIM workflows might look like
