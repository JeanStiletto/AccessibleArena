# BaseNavigator.Carousel.cs
Path: src/Core/Services/BaseNavigator/BaseNavigator.Carousel.cs
Lines: 262

## Top-level comments
- Carousel, stepper, slider, and attached action handling via left/right arrows.
- Delayed announcements for value changes; spinner rescan for UI visibility changes; action cycling with position reporting.

## public partial class BaseNavigator (line 20)
### Fields
- private float _stepperAnnounceDelay (line 23)
- private const float StepperAnnounceDelaySeconds (line 24)
- private float _spinnerRescanDelay (line 27)
- private const float SpinnerRescanDelaySeconds (line 28)

### Methods
- protected virtual bool HandleCarouselArrow(bool isNext) (line 34) — route to attached actions, slider, stepper, controls
- private bool HandleSliderArrow(Slider slider, bool isNext) (line 100)
- private bool HandleAttachedActionArrow(NavigableElement element, bool isNext) (line 123) — cycle with position
- protected virtual void RescanAfterSpinnerChange() (line 168) — preserve focus if element exists
- private void AnnounceStepperValue() (line 212) — re-read label after delay
