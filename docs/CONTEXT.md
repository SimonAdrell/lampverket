# Context — what Lampverket is and why it exists

## The idea

Lampverket turns a home into a satirical Swedish government authority — *Myndigheten för Hemautomation*. You don't flip a switch; you file an application. A case-officer agent processes it like a real public authority would: it assigns a *diarienummer*, reviews the matter, issues a formal *beslut* (decision) with a written *motivering*, and then carries it out through Home Assistant.

The one-line pitch:

> *"I was tired of flipping switches like an ordinary citizen, so I turned my apartment into a public authority. Now I submit an application to turn on a lamp, and a case officer issues a formal decision before the light comes on."*

The whole joke lands on screen in seconds: a request goes in, the bureaucracy whirrs, a stamped decision appears — and a real light actually turns on.

## What it demonstrates

Lampverket is a comedy wrapper around a serious pattern. Building it exercises, end to end:

- **Full-stack agentic AI in C#/.NET** — a Blazor e-tjänst front end, a C# *handläggare* agent calling Claude, and live device control, all in one .NET solution. A rarer, more differentiating signal than the usual Python demo.
- **Agentic case handling** — intake, review, decision, and execution modelled as discrete states, the same shape as a real triage/ticketing pipeline.
- **Live tool execution via MCP** — a C# MCP client calls Home Assistant's MCP Server to act on a *real* system, not a mock. Decisions have physical consequences.
- **Structured, auditable output** — every action produces a logged decision with a reference number. The audit trail (*diariet*) is a first-class feature.
- **Persona and prompt engineering** — a consistent, in-character civil-servant voice with rules it actually follows (refusals, rate limits, "lagom" constraints).
- **Responsible-AI instincts, played for laughs** — explicit refusals, an immutable log, and a narrow action scope, all dressed up as bureaucracy.

## Scope

This is a deliberately small, fun project — buildable in a weekend. It is a **reference architecture and portfolio piece**, demonstrable end to end but not a product. The design intentionally mirrors how real-world case-handling systems are structured, which is what makes a joke about light switches a legitimate engineering showcase.

## Non-goals

- Not a production home-automation controller. It governs a short, explicit allow-list of devices (lights, speakers). No locks, alarms, or anything safety-critical.
- Not a general assistant. It only handles "ärenden" (cases) about the devices it governs.
- Not a multi-tenant or multi-user system. One household, one applicant.

## Author and license

Built by **Code by Simon** as an open portfolio project. Licensed under **Apache 2.0**.

## Reading order

1. This file — the why.
2. [`ARCHITECTURE.md`](ARCHITECTURE.md) — how it's built (the .NET solution).
3. [`BUREAUCRACY.md`](BUREAUCRACY.md) — the domain rules and the in-character behaviour.
4. [`WEBAPP.md`](WEBAPP.md) — the Blazor e-tjänst portal and its design system.
5. [`DEVICES.md`](DEVICES.md) — the Home Assistant integration.
6. [`ROADMAP.md`](ROADMAP.md) — the build plan and demo.
