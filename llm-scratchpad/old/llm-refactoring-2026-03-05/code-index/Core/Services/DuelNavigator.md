# DuelNavigator.cs

Navigator for the actual duel/gameplay in DuelScene. Handles UI element discovery and Tab navigation. Delegates zone navigation to ZoneNavigator. Delegates target selection to TargetNavigator. Delegates playable card cycling to HighlightNavigator. Activates DuelAnnouncer for game event announcements.

```
public class DuelNavigator : BaseNavigator (line 22)
  // WinAPI for centering mouse cursor once when duel starts
  [DllImport("user32.dll")]
  private static extern bool SetCursorPos(int X, int Y) (line 26)

  private bool _isWatching (line 28)
  private bool _hasCenteredMouse (line 29)
  private ZoneNavigator _zoneNavigator (line 30)
  private HotHighlightNavigator _hotHighlightNavigator (line 31)
  private CombatNavigator _combatNavigator (line 32)
  private BattlefieldNavigator _battlefieldNavigator (line 33)
  private BrowserNavigator _browserNavigator (line 34)
  private ManaColorPickerNavigator _manaColorPicker (line 35)
  private PlayerPortraitNavigator _portraitNavigator (line 36)
  private PriorityController _priorityController (line 37)
  private DuelAnnouncer _duelAnnouncer (line 38)

  // DEPRECATED: Old separate navigators replaced by unified HotHighlightNavigator (line 40)

  public override string NavigatorId => "Duel" (line 49)
  public override string ScreenName => Strings.ScreenDuel (line 50)
  public override int Priority => 70 (line 51)
  protected override bool AcceptSpaceKey => false (line 54)

  public ZoneNavigator ZoneNavigator => _zoneNavigator (line 56)
  public HotHighlightNavigator HotHighlightNavigator => _hotHighlightNavigator (line 57)
  public BattlefieldNavigator BattlefieldNavigator => _battlefieldNavigator (line 58)
  public BrowserNavigator BrowserNavigator => _browserNavigator (line 59)
  public DuelAnnouncer DuelAnnouncer => _duelAnnouncer (line 60)
  public PlayerPortraitNavigator PortraitNavigator => _portraitNavigator (line 61)

  public DuelNavigator(IAnnouncementService announcer) : base(announcer) (line 65)

  // Called by AccessibleArenaMod when DuelScene loads
  public void OnDuelSceneLoaded() (line 108)

  // Called when DuelNavigator becomes active. Centers mouse once for card playing
  protected override void OnActivated() (line 128)

  public override void OnSceneChanged(string sceneName) (line 144)
  protected override bool DetectScreen() (line 161)
  protected override bool ValidateElements() (line 171)

  // Override to clear EventSystem selection instead of setting it
  protected override void UpdateEventSystemSelection() (line 188)

  protected override void DiscoverElements() (line 198)
  private void DiscoverSelectables(HashSet<GameObject> addedObjects) (line 239)
  private void DiscoverCustomButtons(HashSet<GameObject> addedObjects) (line 263)
  private void DiscoverEventTriggers(HashSet<GameObject> addedObjects) (line 288)
  private void DiscoverDuelSpecificElements(HashSet<GameObject> addedObjects) (line 316)

  protected override string GetActivationAnnouncement() (line 345)
  protected override bool OnElementActivated(int index, GameObject element) (line 353)

  // Handles zone navigation, target selection, and playable card cycling input
  // Priority: Browser > Discard > Combat > HotHighlight > Portrait > Battlefield > Zone > base
  protected override bool HandleCustomInput() (line 373)

  // ... additional methods continue from line 500 onward
```

Note: File is large (29000+ tokens), only first 500 lines indexed. Contains additional input handling, phase stop toggles, zone navigation integration, and helper methods.
