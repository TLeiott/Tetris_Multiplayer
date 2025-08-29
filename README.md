# TetrisMultiplayer (Console)

A simple console-based multiplayer Tetris prototype with host/client, synchronized pieces, HP elimination, spectator mode, and a lobby sandbox.

## How to run

Open two terminals/windows:

1) Host

```bash
# Navigate to the project directory
cd path/to/TetrisMultiplayer
dotnet run
```
- Choose H
- Share your IPv4 shown (port 5000)

2) Client(s)

```bash
# Navigate to the project directory  
cd path/to/TetrisMultiplayer
dotnet run
```
- Choose C
- Enter Host IPv4 shown on host

When all are connected, host presses S to start.

## Controls
- Left/Right arrows: move
- Up arrow: rotate
- Down arrow: soft drop / place when hits ground
- Space: hard drop and place

## Rules
- All players receive the same next piece and must place it; fast players wait until all finish.
- After each round, unique last place loses 1 HP (start at 20 HP). At 0 HP you become a spectator.
- Spectators rotate viewing other boards and see the live leaderboard.
- Last remaining player wins.

## Notes
- Lobby sandbox: large square field where gravity pulls toward center; pieces spawn from a random edge.
- Simple ASCII rendering; window should be wide enough (~100 cols).
