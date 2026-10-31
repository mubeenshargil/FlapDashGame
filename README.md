# FlapDash - Learn Time Complexity by Playing


## About the game

FlapDash is a 2D educational game made with Unity 6 and C#. It introduces **algorithmic time complexity (Big-O notation)**.

The player flies a bird through pipe gaps. A code snippet is shown at the top of the screen, and every gap contains three labelled zones (for example O(1), O(n), O(n^2)). The player must fly through the zone that matches the time complexity of the snippet. A correct zone gives +1 point. A wrong zone, a pipe or the ground ends the run, and the game shows the correct answer with a short explanation.

## What is in this package

- **Windows build**: a folder (or zip file) containing `FlapDash.exe` together with its data files.
- **Source code**: `FlapDashGame.cs`, the whole game in one C# file.
- **Unity project** (if included): the project folder with `Assets`, `Packages` and `ProjectSettings`.

## How to run the game (Windows build)

Requirements: Windows 10 or 11 (64-bit). No installation, no Unity and no internet connection are needed.

1. If the build is a zip file, **extract it completely** first (right-click, "Extract All"). Do not run the game from inside the zip file.
2. Open the extracted folder and double-click **`FlapDash.exe`**. Keep all the files of the folder together (the `.exe` needs the `_Data` folder and the `.dll` files next to it).
3. The start screen appears with a short tutorial. Click the game window or press **Space** to start.

If Windows shows the message "Windows protected your PC" (the application is not digitally signed): click **More info**, then **Run anyway**.

If the game starts in full screen, press **Alt + Enter** to switch to a window. Press **Alt + F4** to quit.

## How to play

1. Read the code snippet at the top of the screen.
2. Decide its time complexity.
3. Fly through the zone whose label matches. The three labels change position for every pipe.
4. A correct zone adds one point. A new level (with a new complexity class) is unlocked every 5 points.
5. A wrong zone, a pipe, the ground or the top of the screen ends the run. The Game Over screen shows the correct answer and a one-line explanation. Click or press Space to play again.

| Input | Action |
|---|---|
| Space, mouse click or touch | Flap (also starts and restarts the game) |
| H | Show or hide the visual hint (dots = number of steps of the code for n = 8) |
| P or Esc | Pause or resume |

Quick guide to the complexity classes used in the game:

| Class | Meaning | Steps for n = 8 |
|---|---|---|
| O(1) | Constant: no loop, always the same | 1 |
| O(log n) | The problem is halved at each step | 3 |
| O(n) | One loop over n items | 8 |
| O(n^2) | A loop inside a loop | 64 |

## How to run the project in Unity (optional)

1. Install **Unity 6 (6000.6.3f1)** with Unity Hub.
2. In Unity Hub choose **Add > Add project from disk** and select the project folder (the one containing `Assets`).
3. Open the project and press **Play**. The game builds itself from `Assets/Scripts/FlapDashGame.cs`; nothing has to be attached to an object.

To try the script in another project: create a Unity 6 project (Universal 2D template), copy `FlapDashGame.cs` into `Assets`, wait for the compilation and press Play. Make sure the project contains only one copy of `FlapDashGame.cs`, otherwise Unity reports "already contains a definition" errors.




