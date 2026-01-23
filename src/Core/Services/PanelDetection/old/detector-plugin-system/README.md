# Archived: Detector Plugin System

**Archived:** January 2026

## Why Archived

The `IPanelDetector` interface and `PanelDetectorManager` plugin system was over-engineered for the actual use case:

- Only 3 detectors existed, each fundamentally different (event-driven vs polling)
- The abstraction provided no benefit - we were never going to add more detectors
- The plugin registration pattern added indirection without flexibility

## What Changed

Detectors are now owned directly by `PanelStateManager`:
- No interface - each detector is a concrete class
- Direct initialization and Update() calls
- Simpler, more maintainable code

## Files Archived

- `IPanelDetector.cs` - The interface (no longer needed)
- `PanelDetectorManager.cs` - The plugin manager (responsibilities moved to PanelStateManager)
- `PanelRegistry.cs` - Centralized panel metadata (methods moved to PanelInfo as static helpers)
