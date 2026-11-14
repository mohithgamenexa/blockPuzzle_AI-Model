# blockPuzzle_AI-Model

A Unity project for a grid-based block puzzle game, with the aim of training an AI agent to play it using Unity ML-Agents.

## Requirements

- Unity **6000.0 LTS** (the project targets 6000.0.23f1; any 6000.0.x patch works)
- ML-Agents (`com.unity.ml-agents` 3.0.0) is listed in `Packages/manifest.json`

## Getting started

1. In Unity Hub, choose **Add → Add project from disk** and select this folder.
2. Open the project. Unity creates `Library/`, the `.meta` files and any missing settings.
3. Press **Play** in any scene. The board spawns itself and a random placeholder player fills it.
4. Commit the `.meta` files and settings Unity generated on first open.

## Layout

```
Assets/
  Scripts/
    Core/       Board.cs, Shape.cs – pure game logic (no rendering), usable headless for training
    Gameplay/   BoardView.cs – renders the board and runs the random placeholder player
  Scenes/  Prefabs/  Art/  ML/
Packages/manifest.json
ProjectSettings/
```

## Next steps

- Add an ML-Agents `Agent` that observes `Board` and chooses (shape, position) actions.
- Replace the random player in `BoardView` with player input and/or the trained model.
