# blockPuzzle_AI-Model

A polished 8×8 block puzzle game in Unity, built so an AI agent can later be trained on it with ML-Agents.

All art and sound are generated in code. The project contains no imported assets, so it opens and plays immediately.

## Getting started

1. Install Unity **6000.0 LTS** (any 6000.0.x patch).
2. In Unity Hub, choose **Add → Add project from disk** and select this folder.
3. On first open, the project creates `Assets/Scenes/Main.unity` and adds it to Build Settings. You can also run **Block Puzzle → Create Main Scene** from the menu.
4. Press **Play**. The game builds itself from code, so pressing Play in any scene also works.
5. Commit the `.meta` files and settings Unity generates on first open.

For mobile builds, set **Player Settings → Resolution and Presentation → Default Orientation** to Portrait. The layout also adapts to landscape and desktop screens.

## How to play

- Drag one of the three pieces from the tray onto the board.
- Fill a whole **row or column** to clear it. Rows and columns can clear at the same time.
- When all three pieces are placed, a new set is dealt.
- The game ends when none of the remaining pieces fits anywhere.

## Gameplay design

| System | Rules |
|---|---|
| **Pieces** | 13 families (dot, domino, lines 3–5, corner, 2×2, 3×3, 2×3 slab, big L, L/J, T, S/Z), with every rotation. That makes 37 orientations. Each family has its own colour. |
| **Scoring** | +1 per block placed. Clearing *n* lines at once scores 10 · n(n+1)/2 (10, 30, 60, 100, …), multiplied by the current combo. A perfect clear (empty board) adds +300. |
| **Combo** | Each placement that clears a line raises the combo. It survives two placements without a clear and resets on the third, so you can set up the next clear. |
| **Fair dealing** | The generator always deals at least one piece that fits when one exists. On a board more than half full, it deals at least two. No duplicate orientations appear in one tray. |
| **Difficulty ramp** | As the score climbs towards 5,000, 1–2 block pieces become rarer and 5+ block pieces more common. |
| **Forgiving drag** | The piece snaps to the nearest legal spot within 0.8 cells. On touch screens it is lifted above your finger. A ghost preview shows where it lands, and lines about to clear glow in the piece's colour. |
| **Feedback** | Blocks pop in on placement. Cleared blocks ripple out from the drop point, flash and burst into particles. The screen shakes on big clears. Praise appears (GREAT / EXCELLENT / AMAZING / PERFECT), plus floating score text and a COMBO counter. Clear sounds rise in pitch as the combo grows. |
| **Readability** | Pieces in the tray that can't fit anywhere are greyed out. On game over, the board greys out from top to bottom before the results card appears. |
| **Persistence** | Best score and mute setting are saved with `PlayerPrefs`. |

## Project layout

```
Assets/
  Scripts/
    Core/     Engine-free rules (no UnityEngine reference), usable headless for AI training
      Board.cs           grid, placement, line detection and clearing
      PieceCatalog.cs    piece families, rotations and colours
      PieceGenerator.cs  weighted, fair tray dealing with difficulty ramp
      ScoreKeeper.cs     points, combos, perfect-clear bonus
    Game/     Unity presentation layer
      GameController.cs  game loop, drag & drop, layout, game over / restart
      BoardView.cs       board rendering, ghost preview, line highlight
      PieceView.cs       tray pieces
      Effects.cs         particles, clear animation, line flashes, camera shake
      Hud.cs             score, best, combo, praise, game-over card (uGUI)
      SpriteFactory.cs   procedural bevelled block / slot / panel / icon sprites
      Sfx.cs             synthesised sound effects
      Palette.cs         every colour in one place
      PointerInput.cs    works with the legacy Input Manager or the Input System package
    Editor/   SceneSetup.cs  creates the Main scene and registers it in Build Settings
  Tests/EditMode/  NUnit tests for Board, PieceCatalog, PieceGenerator, ScoreKeeper
```

Run the tests from **Window → General → Test Runner → EditMode**.

## Next steps

- Add an ML-Agents `Agent` that observes `Board` and the tray and picks a (piece, x, y) action. `Core` already runs without Unity rendering.
