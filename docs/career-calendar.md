# Career calendar

Open the calendar from launch with **K**. Select a commitment (Tab cycles current commitments), then choose Attend [1], Skip [2], Wait [3], Extra practice [4], Rest [5] or Party [6]. These choices only create a preview. **Enter** confirms it; Escape closes the calendar without spending time. Mouse buttons expose the same actions. Upcoming and completed commitments remain inspectable, but cannot be attended ahead of time or repeated.

The preview shows total minutes/slots, the resulting day/slot, known delay and every affected commitment, including whether attendance is required. A replacement or wait may skip an expired commitment or make a still-open commitment late. Late warnings become attendance facts only when the player actually attends or misses that window.

## Time model

The `calendar` section of `PrototypeFixture.json` configures day names, slot names, minutes per slot, activities and commitments. Defaults are seven days with four six-hour slots (Morning, Afternoon, Evening, Night). These coarse slots are tuning defaults. Travel and match-day attendance use two slots. Commitments may deliberately overlap; the preview exposes the conflict and attending one can skip or delay another.

`CareerCalendar` is career-owned and copies its configuration. It has no frame update or wall-clock callback. Reading the schedule, navigating, inspecting players or advancing a separate match clock never advances it. Only a confirmed `CalendarChoice` moves its minute cursor forward. The week boundary rejects activities whose duration plus known delay cannot fit.

- **Attend:** participate from now until the selected current commitment ends. Arrival after its start is Late; the record includes actual participation minutes and lateness.
- **Skip:** advance to that commitment's end and publish a Skipped fact with zero participation.
- **Wait:** advance to the next slot boundary without an activity reward fact. Commitments that expire are recorded as skipped.
- **Replace:** spend the selected activity's full duration plus its known delay, explicitly displacing scheduled commitments as shown in the preview.
- **Extra practice:** requires a completed training choice immediately beforehand, an exact slot boundary and a subsequent free interval. It consumes a full additional slot; it cannot silently overwrite even optional commitments. Chaining practice is possible only while subsequent slots are free. Rest, wait, skip or another activity breaks the immediate continuation.

The prototype Party choice has a known 60-minute oversleep delay. Choosing it on Monday night ends 60 minutes into Tuesday training. Attending then records Late, 60 minutes of lateness and 300 minutes of participation. The delay is an authored decision effect, not elapsed walking time or a hidden random roll.

## Exactly-once facts and integration

A preview is bound to one calendar revision. Confirming that same preview again returns its original decision without spending time or publishing attendance twice. A different stale preview or another calendar's preview is rejected. Sequential time intervals prevent overlapping activity participation. Attendance facts have stable event IDs, player/activity/slot references, commitment ID, required status, lateness and participation duration. Skipped records are consequences, not reward eligibility.

The calendar retains its history when closed and reopened, and newly created characters receive their own fresh week. The readiness system consumes confirmed decisions now; later training XP and relationship systems consume the attendance facts; they must use event IDs for idempotent effect application and respect actual participation duration. This ticket does not award training XP, apply coach penalties, launch the calendar's fixture, create exploration venues or save to disk. Existing launch-screen match demos remain separate from the schedule until the integrated-week ticket.

## Verification

Tests cover preview/navigation inertia, independent match time, required and optional attendance, explicit replacement conflicts, multi-slot durations, late partial attendance, extra-practice availability/consumption, stale/foreign/duplicate confirmations, week boundaries, configuration validation and copied JSON configuration. Character creation tests verify a fresh calendar for all fourteen positions. Packaged smoke uses the real launch-screen preview/confirm adapter for training, extra practice, skip, party delay and late attendance.

Readiness now changes on confirmation: see [fatigue, confidence and recovery](readiness.md). The calendar displays current condition, readiness-adjusted training yield and recent reasons.
