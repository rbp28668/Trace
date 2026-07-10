# Product Requirements Document (PRD)

Automated Distance Handicap Task (DHT) Engine & Scoring System

Domain: Gliding / Sailplane Competition Management

# 1. Executive Summary & Objectives

## Background

In gliding competitions, handicapping is traditionally applied post-flight by
dividing a pilot's achieved speed or distance by their glider's handicap index.
While mathematically sound, this prevents real-time "first-across-the-line"
racing visibility, which diminishes spectator engagement and tactical racing
opportunities.  In addition, a narrow soaring window can disadvantage slower gliders as they may still be on-task when the soaring window closes.

The **Distance Handicap Task (DHT)** solves this by applying the handicap
pre-flight to the task geometry. By expanding the observation zones (barrels)
around turnpoints for lower-performance gliders, the physical distance they must
fly is reduced. If all pilots fly perfectly relative to their gliders'
performance polars, they will finish simultaneously.

## Purpose & Scope

This document outlines the functional, technical, and mathematical requirements
for a software engine capable of:

1. **Task Optimization**: Calculating customized turnpoint barrel sizes (R) for
   an arbitrary list of gliders, handicaps, turnpoints, and forecast wind
   vectors.

2. **Windicapping Integration**: Adjusting the distance discounts to account for
   the disproportionate performance penalty that wind inflicts on
   lower-performance aircraft.

3. **Scoring Engine**: Processing flight logs (IGC files) to evaluate task
   completion, determine scoring distances, and compute final competition points
   for both finishers and land-outs.

# 2. System Inputs & Data Models

The system must consume three primary data structures:

## 2.1 Glider & Handicap Fleet Profile

A collection of competing aircraft, uniquely identified and mapped to their
handicap indices and performance characteristics.

| Field      | Type   | Description                                       | Example                          |
|------------|--------|---------------------------------------------------|----------------------------------|
| Glider_ID  | String | Unique registration or contest number             | G-DDHT / NX                      |
| Class      | String | Competition class designation                     | Club / Standard                  |
| Handicap   | Float  | Performance index (H) relative to baseline (100)  | 93.0 (ASW 19), 111.5 (JS3-18m)   |

Each glider's reference cruise airspeed is **derived from its handicap**, not
supplied per glider — see the resolved question below. The only speed input is a
single engine-level parameter:

| Engine Parameter | Type | Description | Example |
|------------------|------|-------------|---------|
| V_Ref_Cru | Float | Fleet anchor: nil-wind cruise airspeed of a notional $H=100$ glider, in km/h. Each glider's $V_a(H) = V_{\text{Ref\_Cru}} \times (H/100)$. | 130.0 |

> **`V_Ref_Cru` is a calibration input, not a fixed constant.** It sets the wind
> sensitivity of the whole windicap and therefore the barrel sizes; the 130
> example is illustrative only. For a given contest it should be chosen to match
> how the day was actually set. It can be recovered from a published task sheet:
> the reference glider's air-mass ("Act") distance divided by its ground path
> gives the air/ground ratio, and solving the §3.3 wind triangle for the airspeed
> that yields that ratio gives $V_a(H_{\text{Ref}})$, hence
> $V_{\text{Ref\_Cru}} = V_a / (H_{\text{Ref}}/100)$. (For the real Cambridge Cloud
> Rally 2025 Task 1, this was ≈ 90 km/h, not 130 — using 130 undersized every
> barrel by ~10 %.)

> **Resolved — reference cruise airspeed:** Cruise speed is **not** published in
> any handicap list (BGA, DAeC, or IGC carry only a single index, plus span/MTOW
> for ballast classes). Deriving a per-glider polar and running MacCready
> speed-to-fly for every aircraft is impractical for task-setting. Instead, the
> engine derives the reference cruise airspeed **directly from the handicap**:
> the handicap index is by construction proportional to expected cross-country
> speed, so a single fleet-wide anchor speed at $H=100$ scales linearly with
> handicap.
>
> This means **`V_Ref_Cru` is a single engine parameter, not a per-glider input**
> — it is the nil-wind cruise speed of a notional $H=100$ glider (e.g. 130 km/h).
> Each glider's reference cruise speed is then $V_a(H) = V_{\text{Ref\_Cru}}
> \times (H/100)$, which is exactly the airspeed-scaling formula already in §3.3.
> No per-glider polar lookup is required; the per-glider row below is therefore
> *derived*, not supplied.
>
> **Note on handicap scheme & values:** Three numerically incompatible schemes
> exist — BGA (baseline 100), DAeC German index (~100), and IGC (baseline 1.000).
> The PRD must declare which it uses. The example values above are corrected to
> the **BGA** list (ASW 19 = 93.0, JS3-18m = 111.5); the original 98.0 / 114.0
> figures did not match any published BGA value.

## 2.2 Task Geometry (Core Turnpoints)

The sequence of waypoints defining the baseline race course.

| Sequence (i) | Waypoint ID | Latitude (DD) | Longitude (DD) | Type     | Default Radius      |
|--------------|-------------|---------------|----------------|----------|---------------------|
| 0            | START       | 52.1234       | -1.1234        | Start    | 5.0 km (Line)       |
| 1            | TP1         | 52.4567       | -0.8912        | Turnpoint| Variable            |
| 2            | TP2         | 51.9876       | -0.4567        | Checkpoint| 0.5 km (Cylinder)  |
| N-1          | FINISH      | 52.1234       | -1.1234        | Finish   | 3.0 km (Cylinder)   |

**Data format: SeeYou `.CUP` task block** (see §5.1). The waypoint table plus a
`-----Related Tasks-----` section is read directly. Note the CUP convention that a
task line begins with the takeoff and ends with the landing — these may be `???`
placeholders and are trimmed — and that `ObsZone=0` is the **Start** (the takeoff
is not numbered).

### Observation zone vs. barrel (two nested shapes)

Each turnpoint carries **two** concentric shapes, and they serve different
purposes — conflating them is the most common implementation error:

- **Observation sector** (`R1`, half-angle `A1`): the *wide* zone that decides
  whether the turnpoint was **rounded**. Typical DHT value ≈ 10 km / 90°.
- **Barrel** (`R2`): the *inner* full circle whose radius is sized per handicap and
  which drives the **distance calculation** (the corner-cut of §3.2). This is the
  only quantity the optimiser varies.

A fix **achieves** a turnpoint if it lies inside the barrel **or** inside the
sector. The barrel is what §3.2/§3.4/§4.1 mean by "$R_i$". A plain cylinder task
(no separate sector) sets only `R1` and treats it as both. A `Checkpoint` is a
fixed turnpoint whose barrel does not vary with handicap (e.g. a mandatory routing
point); the engine classifies a sub-1 km barrel as a checkpoint.

## 2.3 Atmospheric Wind Profile

The forecast wind vector applied across the task area.

- *Wind_Direction* ($\omega$): Heading from which the wind blows, in degrees
  True (0 − 359).

- *Wind_Speed* ($W$): Velocity of the airmass, in kilometers per hour (km/h or
  knots, internally normalized to km/h).

# 3. Mathematical Specifications & Core Formulae

## 3.1 Leg Geometry Calculations

For any given leg $k$ from $TP_i$ to $TP_{i+1}$, the system must compute the
baseline distance ($L_k$) and true bearing ($\alpha_k$) using Great Circle
navigation (WGS84 ellipsoid or spherical law of cosines for performance
optimization).

Let the track bearing of leg $k$ be $\alpha_k$. At turnpoint $TP_i$ (connecting
leg $k-1$ and leg $k$), the track deflection angle ($\Delta\phi_i$) is calculated
as:

$$\Delta\phi_i = \min\left(\lvert\alpha_k - \alpha_{k-1}\rvert,\ 360^\circ - \lvert\alpha_k - \alpha_{k-1}\rvert\right)$$

The internal vertex angle ($\theta_i$) is:

$$\theta_i = 180^\circ - \Delta\phi_i$$

## 3.2 Spatial Distance Saved via Barrel Radius

When a glider turns at a cylinder of radius $R_i$ centered at $TP_i$ instead of
flying to the exact center coordinates, the reduction in physical distance
($D_{\text{saved},i}$) is given by:

$$D_{\text{saved},i} = 2 R_i \sin\left(\frac{\Delta\phi_i}{2}\right) = 2 R_i \cos\left(\frac{\theta_i}{2}\right)$$

```
        Inbound Leg (k-1)
      -------------------> . (Turnpoint Center)
                          / \
                         /   \  Outbound Leg (k)
                        /     \
                       /       v
                [ Barrel Edge Intersection ]
```

## 3.3 Windicapping & Ground Speed Derivation

Wind impacts gliders non-linearly. Slower gliders spend more time in headwinds,
suffering a compounding penalty. The engine must scale the nominal cruise speed
($V_a$) based on handicap and then calculate the ground speed ($V_g$) for each
leg.

**1. Airspeed Scaling:**

$$V_a(H) = V_{\text{Ref\_Cru}} \times \left(\frac{H}{100}\right)$$

**2. Wind Angle of Attack:**
For a leg with bearing $\alpha_k$ and wind from direction $\omega$:

$$\gamma_k = \alpha_k - \omega$$

**3. Crosswind and Headwind Components:**

$$W_{X,k} = W \cdot \sin(\gamma_k)$$

$$W_{H,k} = W \cdot \cos(\gamma_k)$$

**4. Leg Ground Speed ($V_{g,k}$):**
Using the wind triangle solution:

$$V_{g,k}(H) = \sqrt{V_a(H)^2 - W_{X,k}^2} - W_{H,k}$$

> **Validation note:** This is the correct crab-into-wind ground-speed solution
> (verified by vector derivation), with $\omega$ as the meteorological FROM
> direction: head-on ($\gamma=0$) gives $V_a - W$, tailwind ($\gamma=180^\circ$)
> gives $V_a + W$. The engine must guard the domain $\lvert W_{X,k}\rvert \le V_a(H)$
> (otherwise the track is unflyable and the square root is imaginary); $V_{g,k}$
> can also go negative in a strong headwind — see the Wind Override guard in
> Section 5.2.

## 3.4 Target Distance Formulation

Let $H_{\text{Ref}}$ be the reference handicap (typically the highest performing
glider in the fleet, or a fixed baseline like 120). Let $D_{\text{Ref}}$ be the
total distance flown by the reference glider (who is assigned the minimum
allowable **barrel** radius, typically $R_{\min} = 0.5$ km). $D_{\text{Ref}}$ is
the reference glider's *effective corner-cut* distance (§3.2 applied at $R_{\min}$)
— it is **less than** the centre-to-centre task length, and it is the value the
scorer uses in §4.2. All distances here refer to the barrel radius $R_i$; the
observation sector plays no part in the distance calculation.

The target effective task distance ($D_{\text{Target}}(H)$) for a glider with
handicap $H$ in **Nil Wind** is:

$$D_{\text{Target}}(H) = D_{\text{Ref}} \times \left(\frac{H}{H_{\text{Ref}}}\right)$$

### Integrating Wind (Windicapping Factor)

To preserve identical task times under wind conditions, the engine calculates the
total task duration of the reference glider:

$$T_{\text{Ref}} = \sum_{k=1}^{M} \frac{L_{k,\text{Ref}}}{V_{g,k}(H_{\text{Ref}})}$$

The optimized leg distances $L_k(H)$ for a lower handicap glider must satisfy:

$$\sum_{k=1}^{M} \frac{L_k(H)}{V_{g,k}(H)} = T_{\text{Ref}}$$

# 4. Functional Requirements & Algorithms

## 4.1 Barrel Radius Optimization Algorithm

The system must dynamically size the **barrels** (the inner `R2` circles) for each
variable turnpoint to match the target distance or target time calculated in
Section 3. The observation sectors (`R1/A1`) are fixed by the task-setter and are
**not** touched by the optimiser.

1. **Calculate Reference Fleet Metrics:** Compute $T_{\text{Ref}}$ and
   $D_{\text{Ref}}$ based on the wind profile and the high-performance benchmark
   glider.

2. **Determine Deficit Distance:** For each glider $G$ with handicap
   $H < H_{\text{Ref}}$, calculate the absolute required path reduction:
   $\Delta D = D_{\text{Ref}} - D_{\text{Target}}(H)$.

3. **Allocate Proportional Reductions:** By default, distribute the required
   $\Delta D$ equally among all active variable turnpoints.

4. **Iterative Convergence Loop:** Because changing the radius alters the
   entry/exit points of a leg (and therefore marginally changes the leg bearing
   $\alpha_k$ and ground speed $V_{g,k}$), an iterative convergence loop (e.g.,
   Newton-Raphson or a simple bisection method) must be used to adjust $R_i$
   until the simulated flight duration matches $T_{\text{Ref}}$ within an error
   margin of $\pm 5$ seconds.

**Algorithm Rules:**

- **Geometric Weighting:** The system must reject turnpoints where the vertex
  angle $\theta_i > 150^\circ$ (shallow turns). Rearranging §3.2,
  $R = D_{\text{saved}} / (2\cos(\theta/2))$, so as $\theta \to 180^\circ$
  (a straight-through turnpoint) the denominator $2\cos(\theta/2) \to 0$ and the
  required radius diverges. The $150^\circ$ cutoff ($30^\circ$ of deflection) is
  a reasonable but **tunable heuristic**, not a fundamental limit.

- **Bounding Constraints:** The engine must enforce an absolute lower bound
  ($R_{\min} = 0.5$ km) and an absolute upper bound ($R_{\max} = 12.0$ km) to
  prevent turnpoints from overlapping or extending out of convective weather
  zones.

## 4.2 Scoring Engine Functional Requirements

Post-flight, the system must process IGC GNSS log files using the exact barrel
dimensions computed for that pilot's handicap class.

### Zone achievement rules (apply to all of Case A and B)

A turnpoint is **achieved** when a fix lies inside its **observation zone** — the
barrel circle $R_2(H)$ **or** the observation sector ($R_1$ within half-angle
$A_1$ of the zone direction). Note the achievement test uses the *sector*, which is
wider than the barrel; the barrel radius $R_i(H)$ is used only for the distance
maths above. A pilot who rounds wide (outside the small barrel but inside the
10 km sector) has still achieved the turnpoint.

- **Sector direction** follows the CUP `Style`: `Symmetric` opens **outward** —
  along the reverse of the bisector of the bearings to the two neighbouring points
  — so it faces away from both legs; `ToNext`/`ToPrev`/`ToStart` face the named
  neighbour; `Fixed` uses `A12`. The angle is a half-angle: $A_1 = 45^\circ$ is a
  $90^\circ$ sector, $A_1 = 180^\circ$ a full circle.
- **In order:** turnpoints must be achieved in sequence. A later turnpoint cannot
  be credited from a fix earlier than the one that achieved the previous point —
  essential because the wide sectors overlap.

### Case A: Task Finishers

If a pilot crosses the **start line**, achieves every turnpoint zone in order (at
their personalised radii $R_i(H)$ / sectors), and crosses the finish:

1. **Raw Duration ($T_{\text{Act}}$):** Calculate elapsed time from the start to
   the finish.
   - **Start:** the zone is a *line* (`Style=ToNext, Line=1`). Time the **line
     crossing** (heading onto course), not the exit from a notional cylinder.
     Because pilots climb in and out of the start zone before starting, take the
     **last** start crossing/exit *before* the first turnpoint is reached — not
     the first.
   - **Finish:** entry to the finish ring (its radius exists to give landing room;
     the ring boundary, not the centre, stops the clock).

2. **Scoring Speed ($V_{\text{Score}}$):**

$$V_{\text{Score}} = \frac{D_{\text{Ref}}}{T_{\text{Act}}}$$

   *Note: Because the distance handicap was built into the physical course flown,
   the scoring speed uses the fixed Reference Distance. No subsequent handicap
   division is applied.*

### Case B: Land-outs (Non-Finishers)

If a pilot fails to complete the task:

1. Identify the last successfully achieved turnpoint barrel zone $TP_j$.

2. Compute the marking point on the uncompleted leg $k$ as the closest point of
   approach (CPA) projected onto the leg line towards the next turnpoint barrel
   edge.

3. Calculate Achieved Distance ($D_{\text{Ach}}$) along the pilot's personalized
   path.

4. Scale up the distance for scoring parity using the distance handicap ratio:

$$D_{\text{Final\_Score}} = D_{\text{Ach}} \times \left(\frac{H_{\text{Ref}}}{H}\right)$$

# 5. Technical & Data Interoperability Requirements

## 5.1 File Formats & Exporting

The system must generate task configuration files compatible with standard flight
computers (LXNav LX9000, Naviter Oudie, XCSoar) using the **standard
Naviter/SeeYou `.CUP` task format**.

Because the `.CUP` format has no mechanism for per-handicap variable radii, the
engine must **emit one standard `.CUP` task file per handicap**, each containing
fixed observation-zone radii computed for that handicap. The file name should
identify the handicap (e.g. `task_h93.cup`).

- **Observation zones:** Each waypoint's zone is the standard
  `ObsZone=<n>,Style=<0-4>,R1=,A1=,R2=,A2=,A12=` line, where `Style` is a numeric
  direction code (0=Fixed, 1=Symmetric, 2=ToNext, 3=ToPrev, 4=ToStart).
  **`R1/A1` is the observation sector; `R2/A2` is the inner barrel** (see §2.2).
  The engine sizes only `R2` per handicap and leaves `R1/A1` (and every other
  field — `SpeedStyle`, `MaxAlt`, the `Options` line, the `???` takeoff/landing)
  **untouched**, re-emitting the source task verbatim with just the barrel changed.
  A plain 500 m cylinder (no separate sector) is
  `ObsZone=1,Style=1,R1=500m,A1=180`.

- **Format Structure** — a variable turnpoint with a 10 km / 90° observation
  sector and a per-handicap barrel of 6.4 km (`R2`); a fixed 0.5 km checkpoint; a
  5 km start **line** and a 3 km finish ring:

```
-----Related Tasks-----
"Example task","???","START","TP1","TP2","FINISH","???"
Options,NearAlt=300.0m
ObsZone=0,Style=2,R1=5000m,A1=90,Line=1        ; start line (half-width R1)
ObsZone=1,Style=1,R1=10000m,A1=45,R2=6400m,A2=180  ; sector R1/A1, barrel R2
ObsZone=2,Style=1,R1=10000m,A1=45,R2=500m,A2=180   ; checkpoint (small barrel)
ObsZone=3,Style=3,R1=3000m,A1=180              ; finish ring
```

Angles are half-angles (`A1=45` → 90° sector, `A1=180` → full circle). `ObsZone=0`
is the Start; the leading/trailing `???` are the (unset) takeoff/landing.

Spec: <https://github.com/naviter/seeyou_file_formats/blob/main/CUP_file_format.md>
(a copy is kept at [`CUP_file_format.md`](CUP_file_format.md)).

## 5.2 Exception Handling & System Guardrails

- **Airspace Violation Hazard:** The system must cross-reference the maximum
  possible outer boundary of any barrel ($R_{\max}$) against an active airspace
  database (OpenAIP/SUA). If a lower-handicap barrel penetrates Controlled
  Airspace (CTR/TMA), the system must flag a hard error and prevent task
  publication.

- **Wind Overrides:** If the forecast wind speed $W \geq 0.4 \times V_a(H)$ for
  the lowest handicap glider, the system must automatically downgrade the task or
  emit a warning, as the aircraft may achieve zero or negative ground speed on
  headwind legs.

> **Terminology note:** "SUA" (Special Use Airspace) is primarily a US/FAA term
> and was not confirmed in the OpenAir/OpenAIP gliding context; for UK/European
> competitions the relevant classes are CTR, TMA, and CTA. Confirm the intended
> airspace class labels against the target region's data.

# Appendix A: Domain Validation Summary

The requirements were validated against the real-world gliding competition domain
(BGA, FAI/IGC Sporting Code, Naviter/SeeYou file specs). Outcome per area:

| Area | Status | Notes |
|------|--------|-------|
| Distance Handicap Task concept | ✅ Validated | Real, trialled task type (BGA/GFA/FAI); pre-applying handicap to geometry is the recognised innovation. |
| Windicapping | ✅ Validated | Real BGA term; headwind penalty $\propto 1/(1-(W/V_a)^2)$ grows as $V_a$ falls, so slower gliders are disproportionately penalised. |
| Wind-triangle ground speed (§3.3) | ✅ Validated | Correct crab solution; add domain guard $\lvert W_X\rvert \le V_a$. |
| Distance-saved geometry (§3.2) | ✅ Validated | $2R\sin(\Delta\phi/2)=2R\cos(\theta/2)$ correct; wording on diverging $R$ tightened. |
| Observation zones (sector + barrel) | ✅ Validated | A turnpoint has a wide observation sector (`R1/A1`, achievement) and an inner barrel (`R2`, distance). The optimiser sizes only the barrel. Symmetric sectors open outward; achievement is in-order. Confirmed against real Cloud Rally 2025 traces. |
| IGC flight logs | ✅ Validated | Standard FAI GNSS log format for scoring. |
| OpenAir / OpenAIP / CTR / TMA | ✅ Validated | Real airspace formats/classes; "SUA" label unconfirmed for this context. |
| Handicap example values (§2.1) | ⚠️ Corrected | ASW 19 93.0 (was 98.0), JS3-18m 111.5 (was 114.0); declare scheme (BGA/DAeC/IGC). |
| Reference cruise airspeed source | ✅ Resolved | Not in any list and polar-per-glider is impractical — derive from handicap: $V_a(H)=V_{\text{Ref\_Cru}}\times(H/100)$ from one fleet anchor speed. |
| Shallow-turn $\theta>150^\circ$ rule | ⚠️ Heuristic | Sound logic, but the threshold is tunable, not fundamental. |
| CUP export schema (§5.1) | ✅ Corrected | Standard numeric `ObsZone` lines, one task file per handicap. R1/A1 = sector, R2 = barrel (only R2 varies); source task re-emitted verbatim. Scorer reproduces the published Cloud Rally 2025 finisher speeds to 0.01 km/h. |
