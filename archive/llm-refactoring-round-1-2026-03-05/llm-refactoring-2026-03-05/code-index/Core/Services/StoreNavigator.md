# StoreNavigator.cs

## Overview
Standalone navigator for the MTGA Store screen.
Two-level navigation: tabs (Up/Down) and items (Up/Down with Left/Right for purchase options).
Accesses ContentController_StoreCarousel via reflection for tab state, loading detection, and item data.

## Class: StoreNavigator : BaseNavigator (line 19)

### Constants
- private const int StorePriority (line 23)
- private const float TabLoadCheckInterval (line 24)

### Navigator Identity
- public override string NavigatorId (line 30)
- public override string ScreenName (line 31)
- public override int Priority (line 32)
- protected override bool SupportsCardNavigation (line 33)
- protected override bool AcceptSpaceKey (line 34)

### Navigation State
- private enum NavigationLevel { Tabs, Items } (line 40)
- private NavigationLevel _navLevel (line 46)
- private int _currentTabIndex (line 47)
- private int _currentItemIndex (line 48)
- private int _currentPurchaseOptionIndex (line 49)
- private bool _waitingForTabLoad (line 50)
- private float _loadCheckTimer (line 51)

Note: File is large (2900+ lines). Contains extensive reflection-based carousel interaction, tab management, store item navigation, purchase option handling, and payment browser integration. Key sections include:
- Screen Detection
- Reflection Setup
- Lifecycle Management
- Navigation State Management
- Input Handling
- Element Discovery
- Tab Navigation
- Item Navigation
- Purchase Options
- Activation Logic
- Announcement Building
- Payment Browser Integration
