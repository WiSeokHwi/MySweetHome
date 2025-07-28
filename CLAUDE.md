# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

MySweetHome is a Unity VR project focused on object placement mechanics in virtual reality. The project enables users to grab, manipulate, and place objects on a grid-based system using Meta XR SDK and Unity's XR Interaction Toolkit.

**Unity Version**: 6000.0.53f1 (Unity 6)

## Key Dependencies

- Meta XR All-in-One SDK (77.0.0) - For Quest/Meta headset support
- Meta XR Movement SDK - For body tracking capabilities
- Unity XR Interaction Toolkit (3.2.0) - Core VR interaction system
- Universal Render Pipeline (17.0.4) - Rendering pipeline
- Unity Input System (1.14.0) - Modern input handling

## Architecture Overview

The project follows a modular VR interaction architecture with three core components:

### Core Systems

1. **GridManager** (`Assets/Scripts/GridManager.cs`)
   - Manages a 2D grid system for object placement
   - Handles grid visualization and material changes
   - Provides coordinate conversion between world and grid space
   - Tracks occupied cells and validates placement positions
   - Configurable grid size and cell dimensions

2. **PlacableItem** (`Assets/Scripts/PlacableItem.cs`)
   - Component for objects that can be placed on the grid
   - Manages object states (grabbed/placed)
   - Handles Rigidbody physics state transitions
   - Integrates with XRGrabInteractable for VR interactions

3. **VRPlacementController** (`Assets/Scripts/VRPlacementController.cs`)
   - Main controller for VR placement interactions
   - Manages placement mode toggle and preview system
   - Handles raycast-based placement targeting
   - Coordinates between grabbing and placement systems
   - Includes 3D collision detection for realistic placement validation

4. **PreviewCollisionDetector** (`Assets/Scripts/PreviewCollisionDetector.cs`)
   - Handles collision detection for preview objects during placement
   - Uses trigger-based collision system to detect overlaps
   - Ignores collisions with the original item being placed
   - Provides real-time collision feedback to VRPlacementController

### Interaction Flow

1. **Grabbing**: User grabs objects with VR controllers using NearFarInteractor
2. **Placement Mode**: Toggle placement mode to enter grid-based positioning
3. **Preview**: Real-time visual feedback shows placement validity and collision detection
4. **Collision Detection**: Preview object detects collisions with other objects in real-time
5. **Placement**: Objects snap to grid positions with comprehensive validation (grid bounds, cell occupancy, and 3D collisions)

### State Management

The `PlacementState` enum defines object states:
- `None`: No interaction
- `Grabbing`: Object is being held
- `Placing`: In placement mode with preview
- `Placed`: Successfully placed on grid

## Common Development Commands

### Building the Project
```bash
# Unity builds are typically done through the Unity Editor
# File -> Build Settings -> Build
# Or use Unity Cloud Build for automated builds
```

### Running Tests
```bash
# Unity tests are run through the Test Runner window
# Window -> General -> Test Runner
# Or via command line:
Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode
```

### Code Analysis
```bash
# No specific linting tools configured
# Consider using Unity's built-in Code Analysis
# Or external tools like SonarQube for C# analysis
```

## Materials and Visual System

The project uses different materials for placement feedback:
- **CanPlace_Material**: Green indicator for valid placement
- **CannotPlace_Material**: Red indicator for invalid placement  
- **Default Grid Visual**: Normal grid appearance

Materials are located in `Assets/Materials/` and automatically switch based on placement validity.

## VR Setup Requirements

- Compatible with Meta Quest headsets via OpenXR
- Requires XR Device Simulator for testing without hardware
- Uses hand tracking and controller input through Meta XR SDK

## Key Configuration Areas

- **Grid Settings**: Configurable in GridManager component (cell size, grid dimensions)
- **Input Actions**: VR input mapping in InputSystem_Actions.inputactions
- **XR Settings**: Located in `Assets/XR/Settings/` for OpenXR configuration
- **Render Pipeline**: URP settings in `Assets/Settings/`

## Scene Structure

Main scene is `Assets/Scenes/MainScene.unity` which contains:
- XR Origin with controllers and hand tracking
- GridManager for placement system
- Sample placeable objects
- Environment and lighting setup

## Debugging and Development Tips

- Use Unity's XR Device Simulator when VR hardware is unavailable
- Grid visualization shows occupied cells in red when selected in Scene view
- Console logs provide detailed placement validation feedback
- Preview objects show real-time placement validity during interactions
- Collision detection provides specific failure reasons (grid bounds, cell occupancy, or 3D collisions)
- PreviewCollisionDetector logs collision enter/exit events for debugging

## Collision Detection System

The placement system includes 3D collision detection through:

1. **Preview Object Colliders**: Automatically copied from original objects during preview creation
2. **Trigger-Based Detection**: Uses OnTriggerEnter/Exit for non-physical collision detection
3. **Smart Filtering**: Ignores collisions with the original item being placed
4. **Real-Time Feedback**: Updates placement validity immediately when collisions occur/end
5. **Multiple Collider Support**: Handles BoxCollider, SphereCollider, CapsuleCollider, and MeshCollider types

## Performance Optimizations

- **Placement Ray Interactor**: Only activated during placement mode to avoid unnecessary raycasting
- **Local Grid Rendering**: Shows only nearby grid cells during placement instead of the entire grid
- **Conditional Updates**: Grid updates triggered only when player moves beyond threshold distance
- **Efficient Collision Detection**: Uses trigger-based system for collision detection without physics simulation

## Crafting System (Phase 2 Complete)

The project now includes a complete Minecraft-style crafting system foundation:

### Core Components

1. **CraftingMaterial** (`Assets/Scripts/Crafting/CraftingMaterial.cs`)
   - ScriptableObject-based material definition system
   - Configurable stack properties (stackable, max stack size)
   - Icon and 3D prefab references for UI and world representation
   - Validation and debugging support

2. **ItemStack** (`Assets/Scripts/Crafting/ItemStack.cs`)
   - Manages material quantities and stack operations
   - Supports combining, splitting, and transferring between stacks
   - Built-in validation and error handling
   - Serializable for save/load functionality

3. **CraftingRecipe** (`Assets/Scripts/Crafting/CraftingRecipe.cs`)
   - 3x3 grid pattern-based recipe system
   - Supports both shaped (exact pattern) and shapeless recipes
   - Flexible material consumption and result generation
   - Pattern validation and execution logic

4. **CraftingManager** (`Assets/Scripts/Crafting/CraftingManager.cs`)
   - Singleton pattern recipe database manager
   - Efficient recipe matching with caching system
   - Recipe search by ingredient, result, or name
   - Crafting execution and validation

5. **RecipePresets** (`Assets/Scripts/Crafting/RecipePresets.cs`)
   - Helper class for creating common Minecraft-style recipes
   - Context menu tools for quick recipe generation
   - Examples: stick, wooden sword, stone sword, crafting table

### Creating Crafting Materials

To create new crafting materials in Unity:

1. **Right-click in Project window**
2. **Create → Crafting System → Material**
3. **Configure the material properties:**
   - Material Name: Display name
   - Icon: 2D sprite for UI
   - World Prefab: 3D model for world placement
   - Stack Properties: Stackable flag and max stack size
   - Description: Tooltip text

### Creating Crafting Recipes

To create new crafting recipes:

1. **Right-click in Project window**
2. **Create → Crafting System → Recipe**
3. **Configure the recipe:**
   - Recipe Name: Display name
   - Input Pattern: 3x3 grid of required materials
   - Result Material: What gets crafted
   - Result Quantity: How many items are created
   - Recipe Type: Shaped (exact pattern) or Shapeless

### Setting Up Recipe Testing

1. **Create a GameObject with RecipePresets script**
2. **Assign required CraftingMaterial references**
3. **Use Context Menu buttons to generate test recipes:**
   - Create Stick Recipe
   - Create Wooden Sword Recipe
   - Create All Basic Recipes
4. **Add CraftingManager to scene and assign generated recipes**

### Example Materials to Create

- **Wood**: Stackable (64), basic building material
- **Stone**: Stackable (64), mining resource  
- **Iron Ore**: Stackable (64), crafting component
- **Stick**: Stackable (64), crafting component
- **Wooden Sword**: Non-stackable (1), crafted tool
- **Stone Sword**: Non-stackable (1), crafted tool
- **Crafting Table**: Non-stackable (1), crafted furniture