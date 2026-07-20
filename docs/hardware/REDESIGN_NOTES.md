# 4-Relay Trailer Controller — Redesign Notes

Design record for the rebuild of the 4-relay trailer controller. Written 2026-07-20.

The original build worked but was fragile: a rigid 3D-printed lid pressed down on wiring and
connectors that sat **above** the Pico, stressing solder joints until the unit went partly dead.
Everything below is aimed at not repeating that.

Companion files in this folder:
- `parts-and-build.html` — full parts list
- `frontend-layout.html` — 12 V front-end perfboard layout
- `wiring-reference.html` — wiring / buzz-out reference

---

## 1. Root cause of the original failure

Tall connectors and wiring sat directly above the Pico; the fan protruded down from the lid.
Closing the lid loaded those joints mechanically. **The fix is architectural, not better soldering.**

## 2. Two-deck architecture

| Deck | Contents |
|---|---|
| **Base** | Pico 2W (headerless) + 4 relay modules |
| **Upper** | Power front-end, both bucks, LED driver transistors |

Nothing tall sits above the Pico. Decks connect with keyed plugs so the upper deck lifts off
without desoldering. Lid height is flexible — it is being printed to fit.

Base plate deck space: **37.32 mm** left and right, **7.46 mm** top and bottom.
Perfboard: **30 × 70 mm** (also on hand: 20 × 80 mm, 40 × 60 mm).

## 3. Power tree

12 V in → XT30 (keyed) → 3 A fuse → **IRF9540N** P-channel reverse-polarity protection →
**1.5KE18A** TVS clamp → two independent **MP1584EN** bucks.

- **Buck #1 → 5.00 V** — Pico, button LEDs, 30 mm fan (5 V, 0.12 A, always on)
- **Buck #2 → 3.5 V** — relay coils only

> **Set both bucks to voltage with nothing connected downstream.** These are adjustable and
> ship at arbitrary settings.

### Why these choices

- **Reverse-polarity protection is not optional here.** Two buck converters and a Pico 2 were
  destroyed by plugging the original unit in backwards. XT30 keying prevents the mistake;
  the P-channel MOSFET survives it if the keying is ever defeated.
- **IRF9540N wiring:** Drain = input side, Source = output side, Gate = 100 kΩ to GND with a
  Zener across Gate–Source. (This is backwards from intuition — Drain is the input.)
- **Relay coils are 3 V, not 5 V** — the modules are `JQC3F-03VDC`. Running them at 5 V
  overdrives and heats the coils, hence the dedicated 3.5 V rail.
- **Two bucks instead of a linear regulator.** An AMS1117 was considered and rejected: it burns
  the difference as heat inside a sealed enclosure. Switching regulators run cool.
- **TVS stripe orientation:** stripe → +12 V. Unidirectional.

## 4. Buttons

STARELO 12 mm IP65, **4 wires** — switch and LED are fully independent. LED spec is 12 V/15 mA,
but bench testing showed 12 V is only negligibly brighter than 5 V, so **LEDs run at 5 V**.

LED drive is **low-side 2N2222 NPN**: emitter → GND, collector → LED−, base → 1 kΩ → GPIO.
(TO-18 package, flat side facing you: Collector–Base–Emitter, left to right.)
Long-term heat is a non-issue at 15 mA.

## 5. Relays

Opto-isolated, **high-level trigger**. GPIO drives `IN` directly — the module has its own
onboard resistor and driver transistor. **Leave the jumper in place.**

Relay `IN` lines (GP6–GP9) stay entirely on the base deck and do not cross between decks.

## 6. Inter-deck signals — 12 total

Deliberately split into **two differently-sized keyed connectors so they physically cannot be
cross-plugged**:

| Connector | Pins | Signals |
|---|---|---|
| **Power** | 4 | 5 V, 3.5 V, GND, GND |
| **Data** | 8 | GP0–GP3 (button switches), GP10–GP13 (LED transistor bases) |

## 7. Connectors

All **JST-XH, 2.54 mm** — matches the perfboard grid.

| Side | Part |
|---|---|
| Board headers, right-angle, through-hole | Ssighuyx `SH-XH2.5-DIP-R` (ASIN B0F93YHZYF), 2–12 pin |
| Female housings + pre-crimped 22 AWG | Kidisoii `XH-MM-Kit` (ASIN B0CKZDS8WY), 2–10 pin |

### Connector traps that cost money on this project

- **JST-PH is 2.0 mm and does NOT fit a 2.54 mm perfboard grid.** PH was ordered twice by
  mistake (B0BM492MMF, B0BM82KR17). This is geometry, not preference — PH cannot be used here.
- **Ssighuyx also sells an SMT version.** The correct through-hole part is B0F93YHZYF
  (`SH-XH2.5-DIP-R`); B0F93XSCW6 is surface-mount and unusable on perfboard.
- Part numbering: `B[n]B-XH-A` = vertical/top-entry, `S[n]B-XH-A` = side-entry/right-angle,
  `XHP-n` = wire housing, `SXH-001T` = crimp terminal.

Base-deck power distribution uses a **JANDECCN 2-in-8-out dual-channel lever block**
(ASIN B0BWTF8QZW) — dual-channel keeps +V and GND on separate channels. A Wago 221 is
all-common and will not work for this.

## 8. Firmware — button LED mirroring

The button LEDs mirror relay state. Added in this repo:

- `include/fsconfig.h` — `ledPin` field on `ButtonConfig`
- `src/fsconfig.cpp` — parses optional `"LedPin"`, defaults to `-1` (none)
- `src/button.cpp` — `updateButtonLed()`, called from `initButtons()`,
  `handleLocalToggle()`, and `syncButtonRelayState()`
- `data/config.json` — `LedPin` 10–13 on button1–4

`syncButtonRelayState()` is called from `commandlistener.cpp:327`, so LEDs also track relay
changes commanded remotely by the gateway — not just local button presses.

> **Not yet built or flashed.** Requires `pio run -e pico2w --target buildunified`
> (one-pass flash only — see CLAUDE.md).

## 9. Open items

- Build + flash the LED-mirror firmware
- Physically lay out the 12 V front end on the 30 × 70 perfboard
- Set buck #1 to 5.00 V and buck #2 to 3.5 V before connecting anything downstream
- Decide component placement across both 37 mm shelves

## 10. Side project — RV AC thermostat (paused)

Thermostat is a **Dometic 3316250-712**. It uses a proprietary 3-wire digital protocol
(12 V ±, comms) — **not contact closure**, so it cannot be driven with relays.

**Do not switch the AC compressor with a hobby relay.** The trailer is 30 A shore power with a
single 12 V battery and a converter.

Viable path: **Micro-Air EasyTouch RV** with local **BLE** control. BLE works off-grid; the
WiFi path routes through AWS cloud over TLS and is useless without internet.
