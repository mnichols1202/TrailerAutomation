# Handoff — Read This First

Context transfer for a fresh Claude session. The prior sessions ran under a different account;
that chat history is gone. This file plus `REDESIGN_NOTES.md` is the complete state.

**Read `docs/hardware/REDESIGN_NOTES.md` before answering anything about the hardware rebuild.**

---

## Where things stand

The 4-relay trailer controller works but is being **rebuilt**, not repaired. The original failed
because a rigid printed lid crushed wiring and connectors sitting above the Pico. Design is
settled; parts are mostly ordered; nothing is assembled yet.

**Done:** design decisions, parts selection, connector sourcing, firmware LED-mirror code
(committed as `faa22c4`, **not yet built or flashed**).

**Next, in order:**
1. Flash the firmware — `pio run -e pico2w --target buildunified` (one-pass only, see CLAUDE.md)
2. Set buck #1 to **5.00 V** and buck #2 to **3.5 V** with nothing connected downstream
3. Lay out the 12 V front end on the 30 × 70 mm perfboard
4. Place components across the two 37 mm shelves

**Paused:** RV AC thermostat control. Don't reopen unless asked.

---

## Settled — do not reopen these

These were worked through at length. Re-proposing them wastes the user's time and money.

| Decision | Why it's closed |
|---|---|
| Perfboard, **not** a custom PCB | Explicitly rejected. Do not pitch PCB fab again. |
| JST-**XH** 2.54 mm connectors | PH is 2.0 mm and does not fit the perfboard grid. PH was ordered twice by mistake already. |
| Two MP1584 bucks | A linear regulator (AMS1117) was considered and rejected — heat in a sealed enclosure. |
| Relay coils on a 3.5 V rail | Modules are `JQC3F-03VDC`. 3 V coils, not 5 V. |
| Button LEDs at 5 V | Bench-tested: 12 V is only negligibly brighter. |
| Two-deck case | This is the fix for the original failure. Not negotiable. |
| 4-pin power + 8-pin data, separately keyed | So they physically cannot be cross-plugged. |

### Dead ends already hit — do not suggest

- **Fritzing** for perfboard layout. It was tried extensively and does not do this. Breadboard
  and schematic views don't sync, and it will not produce a real perfboard layout.
- **DIYLC** — installed, but the walkthrough failed badly (see below).
- The **"carrier board"** concept. It was a fabrication and the user rightly rejected it.

---

## How to work with this user

Read this section carefully. Prior sessions went badly in specific, repeatable ways.

**Plain English. No jargon.** The user is capable and does the physical work, but is not an
electronics engineer. That's the whole reason they're here. Don't name a part or technique and
move on — say what it does.

**Never describe software behavior you haven't verified.** The single biggest failure was
confidently explaining Fritzing and DIYLC workflows that don't exist. The user acted on them,
lost hours, and called it — correctly — unfactual rather than a mistake. If you don't know how a
tool behaves, **say you don't know.** Do not narrate a UI you cannot see.

**No happy-path-then-caveat.** Giving an approach and revealing the blocking limitation
afterward was a repeated pattern and the user identified it as such. If something has a
dealbreaker, it leads. Don't answer "yes" to a capability question that's actually "no, because."

**The layout is the deliverable.** In the user's words: the layout tool *is* the important part.
Handing over diagrams and telling them to go build it does not help. The unsolved problem is
getting a clean physical arrangement on perfboard — not one more schematic.

**Don't offload work back.** Sourcing parts, finding part numbers, checking through-hole vs SMT —
do it. Don't hand them a search to run.

**Real constraints, honestly.** They're afraid of destroying components or the trailer. Two bucks
and a Pico were already killed by a reverse-polarity plug-in. Wrong information here has physical
and financial cost, and mistakes mean re-ordering and waiting.

**Surgical changes only.** S3 and Pico firmware are intentionally different. Don't "fix" one
while touching the other. See CLAUDE.md.

---

## Repo notes

- `docs/hardware/*.html` — parts list, front-end layout, wiring reference. Open in a browser.
  These were once published Claude artifacts; those URLs belonged to the old account and are dead.
  The files here are the same content.
- `TrailerAutomationClientPico/data/config.json` is **tracked in git with the WiFi password in
  plaintext**, already in GitHub history. The user has been told. Don't rotate or untrack it
  without asking — but do flag it if the repo's visibility or team access changes.
