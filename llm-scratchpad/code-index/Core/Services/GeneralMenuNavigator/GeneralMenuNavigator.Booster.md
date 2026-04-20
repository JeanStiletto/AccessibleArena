# GeneralMenuNavigator.Booster.cs
Path: src/Core/Services/GeneralMenuNavigator/GeneralMenuNavigator.Booster.cs
Lines: 262

## Top-level comments
- Partial class hosting booster pack carousel navigation: treats all packs as a single navigable carousel element with Left/Right stepping, sorted by on-screen X position.

## public partial class GeneralMenuNavigator (line 22)
### Fields
- private List<GameObject> _boosterPackHitboxes (line 25)
- private int _boosterCarouselIndex (line 26)
- private bool _isBoosterCarouselActive (line 27)

### Methods
- private bool IsInsideCarouselBooster(GameObject obj) (line 32)
- private void AddBoosterCarouselElement() (line 54) — Note: sorts packs by X, builds label with position info, registers as carousel element
- private bool HandleBoosterCarouselNavigation(bool isNext) (line 97) — Note: sends PointerExit to old pack (stops music), clicks new pack to let game center it, triggers rescan so pack-specific buttons update
- private void UpdateBoosterCarouselElement() (line 155)
- private bool OpenSelectedBoosterPack() (line 185)
- protected override bool HandleCarouselArrow(bool isNext) (line 204) — Note: syncs _currentIndex from GroupedNavigator before delegating to base so carousel info is read from the correct element
