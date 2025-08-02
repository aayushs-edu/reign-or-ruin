using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

/// <summary>
/// Helper class for common UI animations used in the day/night cycle system.
/// Provides reusable animation methods with consistent easing and timing.
/// </summary>
public static class UIAnimationHelper
{
    #region Movement Animations
    
    /// <summary>
    /// Slide UI element from off-screen to target position
    /// </summary>
    public static Tween SlideIn(Transform target, Vector3 fromOffset, float duration, Ease ease = Ease.OutBack)
    {
        Vector3 originalPos = target.position;
        target.position = originalPos + fromOffset;
        
        return target.DOMove(originalPos, duration).SetEase(ease);
    }
    
    /// <summary>
    /// Slide UI element from current position to off-screen
    /// </summary>
    public static Tween SlideOut(Transform target, Vector3 toOffset, float duration, Ease ease = Ease.InBack)
    {
        Vector3 originalPos = target.position;
        return target.DOMove(originalPos + toOffset, duration).SetEase(ease);
    }
    
    /// <summary>
    /// Move element smoothly between two positions
    /// </summary>
    public static Tween MoveTo(Transform target, Vector3 destination, float duration, Ease ease = Ease.OutCubic)
    {
        return target.DOMove(destination, duration).SetEase(ease);
    }
    
    #endregion
    
    #region Scale Animations
    
    /// <summary>
    /// Pop in animation with scale
    /// </summary>
    public static Tween PopIn(Transform target, float duration = 0.5f, float overshoot = 1.2f)
    {
        target.localScale = Vector3.zero;
        return target.DOScale(1f, duration).SetEase(Ease.OutBack, overshoot);
    }
    
    /// <summary>
    /// Pop out animation with scale
    /// </summary>
    public static Tween PopOut(Transform target, float duration = 0.3f)
    {
        return target.DOScale(0f, duration).SetEase(Ease.InBack);
    }
    
    /// <summary>
    /// Pulse animation for attention
    /// </summary>
    public static Tween Pulse(Transform target, float scale = 1.1f, float duration = 0.5f, int loops = -1)
    {
        return target.DOScale(scale, duration)
                     .SetEase(Ease.InOutSine)
                     .SetLoops(loops, LoopType.Yoyo);
    }
    
    #endregion
    
    #region Fade Animations
    
    /// <summary>
    /// Fade in CanvasGroup
    /// </summary>
    public static Tween FadeIn(CanvasGroup target, float duration = 0.5f, Ease ease = Ease.OutSine)
    {
        target.alpha = 0f;
        return target.DOFade(1f, duration).SetEase(ease);
    }
    
    /// <summary>
    /// Fade out CanvasGroup
    /// </summary>
    public static Tween FadeOut(CanvasGroup target, float duration = 0.5f, Ease ease = Ease.InSine)
    {
        return target.DOFade(0f, duration).SetEase(ease);
    }
    
    /// <summary>
    /// Fade in Image
    /// </summary>
    public static Tween FadeIn(Image target, float duration = 0.5f, Ease ease = Ease.OutSine)
    {
        Color c = target.color;
        c.a = 0f;
        target.color = c;
        return target.DOFade(1f, duration).SetEase(ease);
    }
    
    /// <summary>
    /// Fade out Image
    /// </summary>
    public static Tween FadeOut(Image target, float duration = 0.5f, Ease ease = Ease.InSine)
    {
        return target.DOFade(0f, duration).SetEase(ease);
    }
    
    /// <summary>
    /// Fade in Text
    /// </summary>
    public static Tween FadeIn(TextMeshProUGUI target, float duration = 0.5f, Ease ease = Ease.OutSine)
    {
        Color c = target.color;
        c.a = 0f;
        target.color = c;
        return target.DOFade(1f, duration).SetEase(ease);
    }
    
    /// <summary>
    /// Fade out Text
    /// </summary>
    public static Tween FadeOut(TextMeshProUGUI target, float duration = 0.5f, Ease ease = Ease.InSine)
    {
        return target.DOFade(0f, duration).SetEase(ease);
    }
    
    #endregion
    
    #region Combined Animations
    
    /// <summary>
    /// Slide and fade in combination
    /// </summary>
    public static Sequence SlideAndFadeIn(Transform target, CanvasGroup canvasGroup, Vector3 fromOffset, float duration = 0.8f)
    {
        Sequence sequence = DOTween.Sequence();
        
        Vector3 originalPos = target.position;
        target.position = originalPos + fromOffset;
        canvasGroup.alpha = 0f;
        
        sequence.Join(target.DOMove(originalPos, duration).SetEase(Ease.OutBack));
        sequence.Join(canvasGroup.DOFade(1f, duration).SetEase(Ease.OutSine));
        
        return sequence;
    }
    
    /// <summary>
    /// Slide and fade out combination
    /// </summary>
    public static Sequence SlideAndFadeOut(Transform target, CanvasGroup canvasGroup, Vector3 toOffset, float duration = 0.6f)
    {
        Sequence sequence = DOTween.Sequence();
        
        Vector3 originalPos = target.position;
        
        sequence.Join(target.DOMove(originalPos + toOffset, duration).SetEase(Ease.InBack));
        sequence.Join(canvasGroup.DOFade(0f, duration).SetEase(Ease.InSine));
        
        return sequence;
    }
    
    /// <summary>
    /// Scale and fade in combination
    /// </summary>
    public static Sequence ScaleAndFadeIn(Transform target, CanvasGroup canvasGroup, float duration = 0.6f)
    {
        Sequence sequence = DOTween.Sequence();
        
        target.localScale = Vector3.zero;
        canvasGroup.alpha = 0f;
        
        sequence.Join(target.DOScale(1f, duration).SetEase(Ease.OutBack));
        sequence.Join(canvasGroup.DOFade(1f, duration).SetEase(Ease.OutSine));
        
        return sequence;
    }
    
    #endregion
    
    #region Text Animations
    
    /// <summary>
    /// Typewriter effect for text
    /// </summary>
    public static Tween TypewriterEffect(TextMeshProUGUI target, string text, float duration = 1f)
    {
        target.text = "";
        return DOTween.To(() => 0, x => { target.text = text.Substring(0, x); }, text.Length, duration).SetEase(Ease.Linear);
    }
    
    /// <summary>
    /// Counter animation for numbers
    /// </summary>
    public static Tween CountTo(TextMeshProUGUI target, int endValue, float duration = 1f, string format = "{0}")
    {
        return DOTween.To(() => 0, x => target.text = string.Format(format, x), endValue, duration);
    }
    
    /// <summary>
    /// Counter animation for float values
    /// </summary>
    public static Tween CountTo(TextMeshProUGUI target, float endValue, float duration = 1f, string format = "{0:F1}")
    {
        return DOTween.To(() => 0f, x => target.text = string.Format(format, x), endValue, duration);
    }
    
    #endregion
    
    #region Color Animations
    
    /// <summary>
    /// Flash color animation
    /// </summary>
    public static Tween FlashColor(Image target, Color flashColor, float duration = 0.5f, int loops = 1)
    {
        Color originalColor = target.color;
        return target.DOColor(flashColor, duration * 0.5f)
                     .SetLoops(loops * 2, LoopType.Yoyo)
                     .SetEase(Ease.InOutSine)
                     .OnComplete(() => target.color = originalColor);
    }
    
    /// <summary>
    /// Smooth color transition
    /// </summary>
    public static Tween TransitionColor(Image target, Color newColor, float duration = 0.5f)
    {
        return target.DOColor(newColor, duration).SetEase(Ease.InOutSine);
    }
    
    #endregion
    
    #region Fill Animations
    
    /// <summary>
    /// Animate slider fill amount
    /// </summary>
    public static Tween FillSlider(Slider target, float endValue, float duration = 1f)
    {
        return target.DOValue(endValue, duration).SetEase(Ease.OutSine);
    }
    
    /// <summary>
    /// Animate image fill amount
    /// </summary>
    public static Tween FillImage(Image target, float endValue, float duration = 1f)
    {
        return target.DOFillAmount(endValue, duration).SetEase(Ease.OutSine);
    }
    
    #endregion
    
    #region Utility Methods
    
    /// <summary>
    /// Get screen-relative offset for slide animations
    /// </summary>
    public static Vector3 GetScreenOffset(Direction direction, float distance = 1000f)
    {
        switch (direction)
        {
            case Direction.Up: return Vector3.up * distance;
            case Direction.Down: return Vector3.down * distance;
            case Direction.Left: return Vector3.left * distance;
            case Direction.Right: return Vector3.right * distance;
            default: return Vector3.zero;
        }
    }
    
    /// <summary>
    /// Create a delay tween
    /// </summary>
    public static Tween Delay(float duration)
    {
        return DOTween.To(() => 0f, x => { }, 1f, duration);
    }
    
    /// <summary>
    /// Kill all tweens on a target
    /// </summary>
    public static void KillTweens(Transform target)
    {
        target.DOKill();
    }
    
    /// <summary>
    /// Pause all tweens on a target
    /// </summary>
    public static void PauseTweens(Transform target)
    {
        DOTween.Pause(target);
    }
    
    /// <summary>
    /// Resume all tweens on a target
    /// </summary>
    public static void ResumeTweens(Transform target)
    {
        DOTween.Play(target);
    }
    
    #endregion
}

/// <summary>
/// Direction enum for slide animations
/// </summary>
public enum Direction
{
    Up,
    Down,
    Left,
    Right
}