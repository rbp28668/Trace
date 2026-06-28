# Airspace
The system needs to read in the geographical position of controlled airspace to validate distance handicapped tasks and competitors traces.  There is no need to edit the airspace definitions- the airspace data should be treated as read-only.

## File Format
For reading airspace the system shall read airspace files in the OpenAir file format.  The definition of this format can be found at https://github.com/naviter/seeyou_file_formats/blob/main/OpenAir_File_Format_Support.md.  A sample UK airspace file can be found in the data folder of this project under data\uk2026-06-11.txt

## Usage
The airspace module shall allow the following functions.

### Add airspace
It should be possible to read multiple airspace files.  This allows competition specific zones to be added to a base airspace file.

### Point in airspace
Determines whether a point lies within controlled airspace.  This is to allow checking of IGC files against airspace.  Note that in this case a sequence of points will be checked and the system should optimise where appropriate given the adjacent points will be geographically close.  It may be appropriate to encapsulate this in a checker object to manage state - e.g. the closest N pieces of airspace.  It must be possible to specify which classes of airspace should be checked.

### Zone intersects airspace
Determines whether an observation zone in a task intersects controlled airspace.  It must be possible to specify which classes of airspace should be checked.  This is needed to validate varable barrels in distance handicapped tasks.  It must be possible to specify the maximum height of the zones - airspace above this should be ignored.

## Note
There is overlap in functionality between checking to see if a point of a trace is within airspace (infringement) and whether a point of a trace is within an observation zone.