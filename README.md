# LilyConsole
A C# library to interface with the various hardware of a WACCA cabinet.

## Hardware Support
- [X] Serial 
  - [X] Card Reader (COM1)
  - [X] Panel VFD (COM2)
  - [X] Console Touch (COM3/4)
- [X] FTDI Interface
  - [X] Console Lights
- [X] IO4
  - [X] Panel Lights
  - [X] Volume/Test/Service Buttons
  - [ ] Coin Sensor
  - [ ] Coin Blocker

## Feature Grid

|                    | .NET Framework 4.5.2 | .NET 8.0 (Godot) | .NET Standard 2.1 (Unity) |
|:------------------:|:--------------------:|:----------------:|:-------------------------:|
| **Console Touch**  |          ✅           |        ⚠         |             ⚠             |
| **Console Lights** |          ✅           |        ❓         |             ✅             |
|  **Card Reader**   |          ✅           |        ⚠         |             ⚠             |
|      **VFD**       |          ✅           |        ⚠         |             ⚠             |
| **IO4 Functions**  |          ✅           |        ❓         |             ❓             |

**Note:** *Due to changes after .NET Framework, support for serial devices is not currently implemented
for these versions. This will be resolved in the future.*

## Examples

***Coming soon!***

## Attributions

Thank you to everyone that has helped out in making this a reality. Here are people in no particular order.

- [Akasaka](https://github.com/vladkorotnev) - Explaining and providing documentation for the VFD protocol
- [cg505](https://github.com/cg505) - Providing code and assistance with lighting
- [Yosh](https://github.com/yoshakami) - Testing the library and reporting issues
- [FizzyApple12](https://github.com/FizzyApple12), [BlackDragon-B](https://github.com/BlackDragon-B) - Reversing and documenting the touch protocol
- [mv1f](https://github.com/mv1f), [Sucareto](https://github.com/Sucareto), [whowechina](https://github.com/whowechina) - Documenting and implementing the Aime reader protocol, in various directions
- [Bottersnike](https://github.com/Bottersnike) - Documenting the AmuseIC code generation process