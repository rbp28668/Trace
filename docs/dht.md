# Product Requirements Document (PRD)

Automated Distance Handicap Task (DHT) Engine & Scoring System

Domain: Gliding / Sailplane Competition Management

# Executive Summary & Objectives

## Background

In gliding competitions, handicapping is traditionally applied post-flight by dividing a pilot's achieved speed or distance by their glider's handicap index. While mathematically sound, this prevents real-time "first-across-the-line" racing visibility, which diminishes spectator engagement and tactical racing opportunities.

The **Distance Handicap Task (DHT)** solves this by applying the handicap pre-flight to the task geometry. By expanding the observation zones (barrels) around turnpoints for lower-performance gliders, the physical distance they must fly is reduced. If all pilots fly perfectly relative to their gliders' performance polars, they will finish simultaneously.

## Purpose & Scope

This document outlines the functional, technical, and mathematical requirements for a software engine capable of:

1. **Task Optimization**: Calculating customized turnpoint barrel sizes (R) for an arbitrary list of gliders, handicaps, turnpoints, and forecast wind vectors.

2. **Windicapping Integration**: Adjusting the distance discounts to account for the disproportionate performance penalty that wind inflicts on lower-performance aircraft.

3. **Scoring Engine**: Processing flight logs (IGC files) to evaluate task completion, determine scoring distances, and compute final competition points for both finishers and land-outs.


# System Inputs & Data Models
The system must consume three primary data structures:

## Glider & Handicap Fleet Profile
A collection of competing aircraft, uniquely identified and mapped to their handicap indices and performance characteristics.


|Field	| Type	| Description	| Example |
|-------|-------|---------------|---------|
|Glider_ID	| String |	Unique registration or contest number | 	G-DDHT / NX |
|Class	|String	| Competition class designation	| Club / Standard |
|Handicap	| Float	| Performance index (H) relative to baseline (100)| 	98.0 (ASW 19), 114.0 (JS3-18m)|
|V_Ref_Cru |	Float |	Reference cruise airspeed (V 
cruise ) in km/h at nil wind |	130.0 |

Question:  where is the reference cruise airspeed found?

## Task Geometry (Core Turnpoints)
The sequence of waypoints defining the baseline race course.

| Sequence (i)	| Waypoint ID	| Latitude (DD)	| Longitude (DD)	| Type	| Default Radius |
|---------------|---------------|---------------|-------------------|-------|----------------|
|0	| START	|52.1234	|-1.1234	|Start| Line	5.0 km (Line)|
|1	| TP1	|52.4567	|-0.8912	|Turnpoint|	Variable|
|2	|TP2	|51.9876	|-0.4567	|Checkpoint| Cylinder	0.5 km (Cylinder)	|
|N-1 |	FINISH	|52.1234|	-1.1234	|Finish| Cylinder	3.0 km (Cylinder)|

Question:  Appropriate data format?  CUP file?

## Atmospheric Wind Profile
The forecast wind vector applied across the task area.

* *Wind_Direction* (ω): Heading from which the wind blows, in degrees True (0 − 359).
 
* *Wind_Speed* (W): Velocity of the airmass, in kilometers per hour (km/h or knots, internally normalized to km/h).


# Mathematical Specifications & Core Formulae

## Leg Geometry Calculations
For any given leg k from TP[i] to TP[i+1] , the system must compute the baseline distance (Lk) and true bearing (αk ) using Great Circle navigation (WGS84 ellipsoid or spherical law of cosines for performance optimization).


Let the track bearing of leg k be αk. At turnpoint TP[i]  (connecting leg k−1 and leg k), the track deflection angle (Δϕi)
​ is calculated as:

Δϕi = min(∣αk - αk−1
​
 ∣,360 
∘
 −∣α 
k
​
 −α 
k−1
​
 ∣)
The internal vertex angle (θ 
i
​
 ) is:

θ 
i
​
 =180 
∘
 −Δϕ 
i
​
 
## Spatial Distance Saved via Barrel Radius
When a glider turns at a cylinder of radius R 
i
​
  centered at TP 
i
​
  instead of flying to the exact center coordinates, the reduction in physical distance (D 
saved,i
​
 ) is given by:

D 
saved,i
​
 =2R 
i
​
 sin( 
2
Δϕ 
i
​
 
​
 )=2R 
i
​
 cos( 
2
θ 
i
​
 
​
 )
        Inbound Leg (k-1)
      -------------------> . (Turnpoint Center)
                        /  \
                       /    \  Outbound Leg (k)
                      /      \
                     /        v
              [ Barrel Edge Intersection ]
## Windicapping & Ground Speed Derivation
Wind impacts gliders non-linearly. Slower gliders spend more time in headwinds, suffering a compounding penalty. The engine must scale the nominal cruise speed (V 
a
​
 ) based on handicap and then calculate the ground speed (V 
g
​
 ) for each leg.

Airspeed Scaling:

V 
a
​
 (H)=V 
Ref_Cru
​
 ×( 
100
H
​
 )
Wind Angle of Attack:
For a leg with bearing α 
k
​
  and wind from direction ω:

γ 
k
​
 =α 
k
​
 −ω
Crosswind and Headwind Components:

W 
X,k
​
 =W⋅sin(γ 
k
​
 )
W 
H,k
​
 =W⋅cos(γ 
k
​
 )
Leg Ground Speed (V 
g,k
​
 ):
Using the wind triangle solution:

V 
g,k
​
 (H)= 
V 
a
​
 (H) 
2
 −W 
X,k
2
​
 

​
 −W 
H,k
​
 
3.4 Target Distance Formulation
Let H 
Ref
​
  be the reference handicap (typically the highest performing glider in the fleet, or a fixed baseline like 120). Let D 
Ref
​
  be the total distance flown by the reference glider (who is assigned the minimum allowable barrel radius, typically R 
min
​
 =0.5 km).

The target effective task distance (D 
Target
​
 (H)) for a glider with handicap H in Nil Wind is:

D 
Target
​
 (H)=D 
Ref
​
 ×( 
H 
Ref
​
 
H
​
 )
Integrating Wind (Windicapping Factor)
To preserve identical task times under wind conditions, the engine calculates the total task duration of the reference glider:

T 
Ref
​
 = 
k=1
∑
M
​
  
V 
g,k
​
 (H 
Ref
​
 )
L 
k,Ref
​
 
​
 
The optimized leg distances L 
k
​
 (H) for a lower handicap glider must satisfy:

k=1
∑
M
​
  
V 
g,k
​
 (H)
L 
k
​
 (H)
​
 =T 
Ref
​
 
4. Functional Requirements & Algorithms
4.1 Barrel Radius Optimization Algorithm
The system must dynamically size the barrels for each variable turnpoint to match the target distance or target time calculated in Section 3.

Calculate Reference Fleet Metrics: Compute T 
Ref
​
  and D 
Ref
​
  based on the wind profile and the high-performance benchmark glider.

Determine Deficit Distance: For each glider G with handicap H<H 
Ref
​
 , calculate the absolute required path reduction: ΔD=D 
Ref
​
 −D 
Target
​
 (H).

Allocate Proportional Reductions: By default, distribute the required ΔD equally among all active variable turnpoints.

Iterative Convergence Loop: Because changing the radius alters the entry/exit points of a leg (and therefore marginally changes the leg bearing α 
k
​
  and ground speed V 
g,k
​
 ), an iterative convergence loop (e.g., Newton-Raphson or a simple bisection method) must be used to adjust R 
i
​
  until the simulated flight duration matches T 
Ref
​
  within an error margin of ±5 seconds.

Algorithm Rules:
Geometric Weighting: The system must reject turnpoints where the vertex angle θ 
i
​
 >150 
∘
  (shallow turns), as the radius required to save distance approaches infinity (2cos(θ/2)→0).

Bounding Constraints: The engine must enforce an absolute lower bound (R 
min
​
 =0.5 km) and an absolute upper bound (R 
max
​
 =12.0 km) to prevent turnpoints from overlapping or extending out of convective weather zones.

4.2 Scoring Engine Functional Requirements
Post-flight, the system must process IGC GNSS log files using the exact barrel dimensions computed for that pilot’s handicap class.

Case A: Task Finishers
If a pilot successfully navigates through the start line, intercepts all variable turnpoint cylinders (at their designated personalized radii R 
i
​
 (H)), and crosses the finish line:

Raw Duration (T 
Act
​
 ): Calculate elapsed time from the exact timestamp of start exit to finish entry.

Scoring Speed (V 
Score
​
 ):

V 
Score
​
 = 
T 
Act
​
 
D 
Ref
​
 
​
 
Note: Because the distance handicap was built into the physical course flown, the scoring speed uses the fixed Reference Distance. No subsequent handicap division is applied.

Case B: Land-outs (Non-Finishers)
If a pilot fails to complete the task:

Identify the last successfully achieved turnpoint barrel zone TP 
j
​
 .

Compute the marking point on the uncompleted leg k as the closest point of approach (CPA) projected onto the leg line towards the next turnpoint barrel edge.

Calculate Achieved Distance (D 
Ach
​
 ) along the pilot’s personalized path.

Scale up the distance for scoring parity using the distance handicap ratio:

D 
Final_Score
​
 =D 
Ach
​
 ×( 
H
H 
Ref
​
 
​
 )
5. Technical & Data Interoperability Requirements
5.1 File Formats & Exporting
The system must generate unified task configuration files compatible with standard flight computers (LXNav LX9000, Naviter Oudie, XCSoar).

SeeYou .CUP Format Expansion: The engine must output a standard task block containing inline schema parameters for ObservationZone variants.

Format Structure:

Plaintext
[Task]
Options=...,VariableBlends=True,HandicapReference=114
TaskPoint=START,Style=Line,Radius=5000
TaskPoint=TP1,Style=Cylinder,VariableRadiusTable=(98:6400,100:5800,114:500)
TaskPoint=TP2,Style=Cylinder,VariableRadiusTable=(98:4200,100:3800,114:500)
TaskPoint=FINISH,Style=Cylinder,Radius=500
5.2 Exception Handling & System Guardrails
Airspace Violation Hazard: The system must cross-reference the maximum possible outer boundary of any barrel (R 
max
​
 ) against an active airspace database (OpenAIP/SUA). If a lower-handicap barrel penetrates Controlled Airspace (CTR/TMA), the system must flag a hard error and prevent task publication.

Wind Overrides: If the forecast wind speed W≥0.4×V 
a
​
 (H) for the lowest handicap glider, the system must automatically downgrade the task or emit a warning, as the aircraft may achieve zero or negative ground speed on headwind legs.