# Club exploration (SME-129)

Press **O / Explore club** on the launch screen to enter the small placeholder training base. The player starts beside the parked car. The six marked destinations are arrival/parking, coach office, teammate area, agent meeting space, media room and training entrance.

## Movement and interaction

Click open ground or a destination marker, or select a named destination using the right-hand buttons / **1–6**. The footballer walks along the green route. Walls, furniture and the car are blocked cells; the route passes through door gaps. Clicking a blocked, out-of-bounds or disconnected point stops movement and shows an unreachable explanation. A new valid destination replaces the route without teleporting the player.

A destination's context appears when selected. Its action becomes usable only after arrival. **Enter / the interaction button** opens the existing calendar preview or a free squad inspector. Walking, redirection, inspection, advice and opening/closing the venue never spend career time, change fatigue, heal injuries or award XP.

The calendar displays the activity's cost and consequences. Confirm separately with **Enter**, or back out with **Escape**. A one-frame input guard prevents the key used to open the calendar from also confirming the activity. **Back to club** returns to the same position. Opening guided practice uses the existing practice flow and returns through the calendar. Creating a new footballer starts a fresh club exploration state.

## Availability

- Arrival: review the calendar for free, or preview team travel when a travel commitment is current.
- Coach: discuss training direction during free daytime slots. The office is unavailable at night or while the squad has training, travel or a match commitment.
- Teammates: inspect names, positions and passing ability when the squad is free during daytime. Authored relationship conversations remain a subsequent component.
- Agent: only a current agent appointment can be attended. Otherwise the panel explains the next appointment or absence of further meetings.
- Media: only an actual scheduled interview creates an available interaction and visible person. The room's existence creates no extra events or rewards for the small club.
- Training: preview the current scheduled session, with simulated/guided choice and focus retained in the calendar. Outside scheduled training, offer extra practice only when the calendar's existing eligibility check permits it. Injured players can inspect recovery/simulated attendance, but cannot start extra physical practice.

Availability is re-evaluated from the current calendar and character state. Pending guided practice blocks new interactions; the completed week still permits walking but no new activities. Previewing an interaction never reserves a slot. Calendar duplicate/stale-choice rules remain authoritative.

## Component boundaries

`ClubVenue.json` contains the immutable small-club grid and destination metadata. `.` is walkable, `#` is a wall, and `x` is furniture/parking obstruction. Loading validates dimensions, cell size, unique required destinations, open destination cells and connectivity from arrival. The grid is copied from definitions. Breadth-first paths contain adjacent open cell centres. Movement consumes presentation seconds at 4 metres/second, independent of calendar time and match randomness.

`ClubExploration` owns position, route and contextual interaction descriptions. `ClubVenueView` supplies 3D placeholders, angled overhead camera, destination picking and inspection. `LaunchScreen` retains the exploration instance for the current career, transfers activity previews to the existing calendar, and restores the club on return. The scene renders people only while their interaction is available. No new social rewards, transfer negotiations or dialogue checks are introduced here.

The map, walking speed and office-hour convention (final slot of the day is night) are prototype tuning choices. The integrated week connects calendar attendance to Sunday’s fixture. Career progress can be saved manually; the current venue position is not retained on load. Original launch-screen match demos remain independent.

## Verification

Tests cover every destination, continuous movement, redirects, blocked/disconnected points, furniture avoidance, frame splitting, unchanged career state, arrival-only interaction, calendar confirmation and exactly-once rewards, extra practice, travel, scheduled media/agent meetings, coach/squad availability, injury/pending-practice/week-end states, content copying/validation, all destination selection rays and camera restoration. Built-player smoke visits every destination through its screen ray and confirms training only after arriving and explicitly committing its calendar choice; it logs `FOOTBALL_RPG_CLUB_EXPLORATION_PASSED`.

Scheduled coach and teammate conversations now appear as calendar attendance previews at their respective destinations. The agent appointment opens its authored conversation on confirmed attendance. Pending conversations must be resolved or explicitly left before another activity; see [club conversations](club-conversations.md).
