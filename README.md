# TetrisMultiplayer (Console)

A comprehensive console-based multiplayer Tetris prototype with host/client networking, synchronized piece generation, HP-based elimination, spectator mode, lobby system, and extensive testing capabilities.

## Game Modes

When you run the application, you'll be prompted to choose from 5 different modes:

### Multiplayer Modes

#### Host Mode (h)
```bash
# Navigate to the project directory
cd path/to/TetrisMultiplayer
dotnet run
# Choose "host" or "h"
```
- Start a multiplayer game server on port 5000
- Your IPv4 address will be displayed for clients to connect
- Manage the lobby and start the game when ready
- Minimum 2 players required to start

#### Client Mode (c)
```bash
# Navigate to the project directory  
cd path/to/TetrisMultiplayer
dotnet run
# Choose "client" or "c"
```
- Connect to an existing host using their IPv4 address
- Wait in lobby until host starts the game
- Synchronized gameplay with all connected players

### Single Player Mode (s)
```bash
dotnet run
# Choose "single" or "s"
```
- Play Tetris solo without networking
- Practice controls and gameplay mechanics
- Standard Tetris rules with gravity and line clearing

### Development & Testing Modes

#### Test Mode (t)
```bash
dotnet run
# Choose "test" or "t"
```
- Run manual testing for game features
- Test preview optimization and caching systems
- Interactive debugging and feature validation

#### Validate Mode (v)
```bash
dotnet run
# Choose "validate" or "v"
```
- Run comprehensive validation tests
- Check optimization systems and synchronization
- Verify deterministic piece generation
- Validate preview synchronization between clients

## Controls

### Movement
- **Left/Right arrows**: Move piece horizontally
- **Down arrow**: Soft drop (faster descent) / place when piece hits ground
- **Space**: Hard drop (instant placement at bottom)

### Rotation
- **Up arrow**: Rotate clockwise
- **Z**: Rotate counter-clockwise  
- **X**: Rotate clockwise (alternative)

### Game Controls
- **Q**: Quit game
- **S**: Start game (host only, in lobby)

### Spectator Controls (when eliminated)
- **N**: View next player's board
- **P**: View previous player's board
- **Q**: Quit spectator mode

### Lobby Controls
- **R**: Return to lobby (after game over)
- **Q**: Quit application

## Game Rules & Features

### Core Gameplay
- **Synchronized Pieces**: All players receive the same piece sequence in the same order
- **Turn-based Rounds**: Fast players wait until all players finish placing their pieces
- **Real-time Leaderboard**: Live score and HP tracking during gameplay
- **Next Piece Preview**: See your upcoming piece while playing

### Elimination System
- **Starting HP**: Each player begins with 100 HP
- **Unique Last Place**: After each round, only the sole last-place player loses 1 HP
- **Tied Scores**: No HP loss when multiple players tie for last place
- **Elimination**: Players with 0 HP become spectators
- **Win Condition**: Last remaining active player wins

### Spectator Features
- **Live Viewing**: Watch any active player's board in real-time
- **Board Switching**: Cycle through all active players with N/P keys
- **Live Leaderboard**: See current scores and HP status
- **Game Statistics**: View final game statistics when game ends

### Technical Features
- **Deterministic Piece Generation**: Consistent gameplay using synchronized random seeds
- **Network Synchronization**: Robust client-server communication with timeout handling
- **Optimized Rendering**: Performance-optimized UI with selective redrawing
- **Comprehensive Testing**: Extensive test suite for networking, synchronization, and gameplay

## System Requirements
- **.NET Runtime**: Compatible with .NET applications
- **Network**: Local network connectivity for multiplayer
- **Terminal**: Console/terminal with at least 100 columns width for optimal display
- **Platform**: Cross-platform (Windows, macOS, Linux)

## Development & Testing

### Comprehensive Test Suite
The project includes extensive testing capabilities:

- **Network Synchronization Tests**: Verify client-server communication
- **Piece Synchronization Tests**: Ensure deterministic piece generation
- **Preview Optimization Tests**: Validate UI performance improvements  
- **End-to-End Integration Tests**: Full multiplayer game scenarios
- **Performance Tests**: Leaderboard and rendering optimization validation

### Manual Testing Features
- **Interactive Test Mode**: Step-through testing with visual feedback
- **Preview Synchronization Validation**: Verify next-piece consistency
- **Network Message Debugging**: Monitor client-server communication
- **UI Optimization Testing**: Performance and rendering validation

## Architecture

### Core Components
- **Game Engine**: Tetris logic, piece generation, and field management
- **Network Manager**: Client-server communication and message handling
- **UI System**: Optimized console rendering with selective updates
- **Synchronization System**: Deterministic gameplay across all clients

### Key Features
- **Lock-step Synchronization**: All players advance together through rounds
- **Centralized Piece Generation**: Host manages piece sequence for all players
- **Real-time Updates**: Live leaderboard and player status broadcasting
- **Graceful Disconnection Handling**: Automatic spectator conversion for timeouts

## Notes
- **Lobby Sandbox**: Features a unique gravity-center field where pieces spawn from random edges
- **ASCII Rendering**: Simple text-based graphics suitable for any terminal
- **Wide Terminal Recommended**: ~100 columns for optimal display of game board and leaderboard
- **Network Configuration**: Default port 5000, configurable in source code
