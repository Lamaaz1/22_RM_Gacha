using UnityEngine;

public sealed class GameReviewButton : MonoBehaviour
{
    [SerializeField] private GameReviewPanel review;

    // Connect to a gameplay milestone, such as the completion of a level.
    public void ShowReview()
    {
        if (review == null || !review.isActiveAndEnabled)
            review = FindFirstObjectByType<GameReviewPanel>();

        if (review != null)
            review.Open();
    }
}
