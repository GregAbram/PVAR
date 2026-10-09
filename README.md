# PVAR

ParaView in shared AR. This is the [Locator](https://github.com/GregAbram/Locator)
with [PVLink](https://github.com/GregAbram/PVLink): geometry streamed from
ParaView through a PVLink DataManager appears above the room's magenta
reference post, in the same place for every viewer on Quest 3 and iPhone.

Unity 6000.3.10f1, with [edu.tacc.apriltags](https://github.com/GregAbram/AprilTags)
and PVLink's Unity package.

## Using it

1. Find the room as with Locator: scan the room code, then look at two tags.
2. Run a DataManager on the same network. PVAR connects by itself when exactly
   one DataManager is announcing, including one started after the app. A room
   code that names a DataManager ("host" or "host:port") is used instead;
   that's the choice when several are running or the network blocks broadcast.
3. Move or resize the data with the `SimContainer` (under
   `Room/Reference Marker`) in the scenes.

## Building

Menu **Tools > PVAR**, or batchmode `PVARApp.BuildIOS` / `PVARApp.BuildQuest`
(see the Locator README). Bundle ID `edu.utexas.tacc.pvar`. New scenes get
their PVLink objects from `Assets/Editor/PVARContent.cs`.
