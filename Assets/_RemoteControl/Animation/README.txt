DOTween UI Scripts

UIInteractionTween.cs
- Add to buttons, cards, toggles, level cards.
- Handles hover + press/release scale.
- Uses only its own Tween reference, so it does not kill unrelated DOTween animations.

UIShowTween.cs
- Add to a panel root.
- Requires CanvasGroup on the same GameObject.
- Plays fade + scale whenever the panel is enabled.

Recommended:
Panel Root: CanvasGroup + UIShowTween
Child Card/Button: UIInteractionTween

Avoid putting UIShowTween and UIInteractionTween on the exact same Transform,
because both animate scale.

Requirement:
DOTween installed and setup in the Unity project.
